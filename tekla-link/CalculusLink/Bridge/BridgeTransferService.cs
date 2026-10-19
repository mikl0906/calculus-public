using CalculusModel;

using Serilog;

using Tekla.Structures;

using TSM = Tekla.Structures.Model;

namespace CalculusLink.Bridge;

/// <summary>
/// Direct (file-less) transfer between Tekla and a running Calculus editor over
/// the bridge, in two halves: <b>classify</b> (what would change — a
/// <see cref="SyncPlan"/>, written nowhere) and <b>apply</b> (do it, per
/// <see cref="SyncOptions"/>). Reuses the file mappers
/// (<see cref="CalculusModelBuilder"/> / <see cref="CalculusModelInserter"/> /
/// <c>CalculusTeklaMapper</c>); twins are matched by the cross-reference ids
/// (<c>userdata.tekla_id</c> on the Calculus side, <c>CALCULUS_ID</c> UDA on the
/// Tekla side) and compared through <see cref="SyncHash"/>, whose baseline on
/// both twins tells an update from a conflict (https://docs.calculus.build/reference/tekla-plugin/sync-behavior).
/// What the source holds is the caller's (<see cref="TransferSource"/>); Apply
/// writes the items whose <see cref="SyncItem.Include"/> is set.
///
/// Caller is responsible for the work-plane (coordinate-system) setup/restore and
/// for running this off the UI thread, mirroring the file import/export commands.
/// </summary>
public static class BridgeTransferService
{
    // --- Tekla -> Calculus ---------------------------------------------------

    /// <summary>What sending these Tekla objects to Calculus would do.</summary>
    public static async Task<SyncPlan> ClassifyToCalculusAsync(
        CalculusBridgeClient client,
        List<TSM.ModelObject> selectedTeklaObjects,
        TSM.Model teklaModel)
    {
        var model = CalculusModelBuilder.FromTeklaObjects(selectedTeklaObjects);
        var plan = new SyncPlan { Direction = SyncDirection.TeklaToCalculus };
        if (model.Objects.Count == 0)
        {
            plan.NotTransferable = selectedTeklaObjects.Count;
            return plan;
        }

        // Index the selected Tekla objects so a built Calculus object can find the
        // Tekla object it came from (linked by the GUID stored in userdata.tekla_id).
        var teklaByGuid = new Dictionary<string, TSM.ModelObject>();
        foreach (var o in selectedTeklaObjects)
        {
            teklaByGuid[o.Identifier.GUID.ToString()] = o;
        }

        // What already lives in the Calculus model — for twin lookup by tekla_id and
        // to decide add (new id) vs update (id already present).
        var existing = await client.GetAllObjectsAsync();
        var existingById = existing.ToDictionary(e => e.GetId());
        var existingByTeklaId = new Dictionary<string, string>();
        foreach (var e in existing)
        {
            var tid = e.GetTeklaId();
            if (tid is not null) existingByTeklaId[tid] = e.GetId();
        }

        // Pass 1: resolve each object's Calculus id to its twin's id when one exists.
        // A Calculus id may be claimed by ONE incoming object: Tekla objects copied
        // inside Tekla carry their original's CALCULUS_ID UDA, and two objects
        // with one id would make the add fail ("already exists"). The first
        // keeps the id, the rest stay new and get the UDA rewritten on apply.
        var remap = new Dictionary<string, string>();
        var claimed = new HashSet<string>();
        foreach (var o in model.Objects)
        {
            var teklaId = o.GetTeklaId();
            if (teklaId is null) continue;

            string? desired = null;
            if (teklaByGuid.TryGetValue(teklaId, out var src))
            {
                desired = TeklaUda.GetCalculusId(src);
            }
            if (desired is null && existingByTeklaId.TryGetValue(teklaId, out var byTwin))
            {
                desired = byTwin;
            }
            if (desired is not null && !claimed.Add(desired))
            {
                Log.Warning("Tekla object {Guid} carries CALCULUS_ID {Id} already claimed by another selected object (a copy?); it will be added as a new object", teklaId, desired);
                desired = null;
            }

            if (desired is not null && desired != o.GetId())
            {
                remap[o.GetId()] = desired;
                o.SetId(desired);
            }
        }

        // Pass 2: repoint child references at any re-ided parents, then classify.
        var incomingIds = new HashSet<string>();
        foreach (var o in model.Objects)
        {
            o.RemapReferences(remap);
            var id = o.GetId();
            incomingIds.Add(id);
            var teklaId = o.GetTeklaId();
            teklaByGuid.TryGetValue(teklaId ?? "", out var teklaObject);
            var hash = SyncHash.Of(o);

            if (!existingById.TryGetValue(id, out var twin))
            {
                plan.Items.Add(new SyncItem
                {
                    State = SyncState.New, Type = o.GetType().Name, CalculusId = id,
                    TeklaId = teklaId, Incoming = o, TeklaObject = teklaObject, Hash = hash,
                });
                continue;
            }

            var current = SyncHash.Of(twin);
            var baseline = SyncHash.Baseline(twin);
            var state = hash == current ? SyncState.Unchanged
                : baseline is null || baseline == current ? SyncState.Updated
                : SyncState.Conflict;
            plan.Items.Add(new SyncItem
            {
                State = state, Type = o.GetType().Name, CalculusId = id,
                TeklaId = teklaId, Incoming = o, Existing = twin, TeklaObject = teklaObject, Hash = hash,
            });
        }

        // Gone: Calculus objects linked to a Tekla object that no longer exists —
        // asked of the Tekla MODEL, never of the selection.
        foreach (var e in existing)
        {
            var tid = e.GetTeklaId();
            if (tid is null || incomingIds.Contains(e.GetId())) continue;
            if (teklaModel.SelectModelObject(new Identifier(tid)) is not null) continue;
            plan.Items.Add(new SyncItem
            {
                State = SyncState.Gone, Type = e.GetType().Name, CalculusId = e.GetId(),
                TeklaId = tid, Existing = e,
            });
        }

        return plan;
    }

    /// <summary>Do what the plan says for its included items.</summary>
    public static async Task<SyncResult> ApplyToCalculusAsync(
        CalculusBridgeClient client,
        SyncPlan plan,
        TSM.Model teklaModel)
    {
        var result = new SyncResult();
        var adds = new List<ModelObject>();
        var updates = new List<ModelObject>();
        var removes = new List<string>();
        var baselinePatches = new List<(string id, Dictionary<string, string> userdata)>();
        var written = new List<SyncItem>();

        foreach (var item in plan.Items)
        {
            if (!item.Include && item.State != SyncState.Unchanged)
            {
                if (item.State == SyncState.Conflict) result.Conflicts++;
                else result.Excluded++;
                continue;
            }
            switch (item.State)
            {
                case SyncState.New:
                    Stamp(item.Incoming!, item.Hash!);
                    adds.Add(item.Incoming!);
                    written.Add(item);
                    result.Added++;
                    break;
                case SyncState.Updated:
                case SyncState.Conflict:
                    Stamp(item.Incoming!, item.Hash!);
                    updates.Add(item.Incoming!);
                    written.Add(item);
                    result.Updated++;
                    break;
                case SyncState.Unchanged:
                    // Up to date, but a twin from before the baseline existed gets one
                    // now so the next transfer can tell a conflict.
                    if (SyncHash.Baseline(item.Existing!) != item.Hash)
                    {
                        var ud = new Dictionary<string, string>(item.Existing!.GetUserdata()) { [SyncHash.UserdataKey] = item.Hash! };
                        baselinePatches.Add((item.CalculusId, ud));
                    }
                    written.Add(item);
                    result.Skipped++;
                    break;
                case SyncState.Gone:
                    removes.Add(item.CalculusId);
                    result.Removed++;
                    break;
            }
        }

        await client.UpdateObjectsAsync(updates);
        await client.AddObjectsAsync(adds);
        await client.PatchUserdataAsync(baselinePatches);
        await client.RemoveObjectsAsync(removes);

        // Stamp CALCULUS_ID and the baseline onto the source Tekla objects so they
        // self-identify their twin next time (best-effort; matching also works
        // without it).
        var udaChanged = false;
        foreach (var item in written)
        {
            if (item.TeklaObject is null) continue;
            var changed = false;
            if (TeklaUda.GetCalculusId(item.TeklaObject) != item.CalculusId)
                changed |= TeklaUda.SetCalculusId(item.TeklaObject, item.CalculusId);
            if (TeklaUda.GetSyncHash(item.TeklaObject) != item.Hash)
                changed |= TeklaUda.SetSyncHash(item.TeklaObject, item.Hash!);
            if (changed)
            {
                item.TeklaObject.Modify();
                udaChanged = true;
            }
        }
        if (udaChanged) teklaModel.CommitChanges();

        Log.Information("Direct transfer to Calculus: {Result}", result);
        return result;
    }

    // --- Calculus -> Tekla ---------------------------------------------------

    /// <summary>
    /// What bringing these Calculus objects into Tekla would do. Gone twins are
    /// looked for among <paramref name="teklaObjects"/> only: the selection, or
    /// the whole model when that is the scope.
    /// </summary>
    public static async Task<SyncPlan> ClassifyToTeklaAsync(
        CalculusBridgeClient client,
        List<ModelObject> selected,
        List<TSM.ModelObject> teklaObjects,
        TSM.Model teklaModel)
    {
        var plan = new SyncPlan { Direction = SyncDirection.CalculusToTekla };
        if (selected.Count == 0)
        {
            return plan;
        }

        // Pull in referenced parents (e.g. a selected bolt's beams) so dependents can
        // resolve their host parts during insertion.
        var byId = new Dictionary<string, ModelObject>();
        foreach (var o in selected) byId[o.GetId()] = o;
        var referenced = await client.GetReferencedObjectsAsync(byId.Keys);
        foreach (var o in referenced)
        {
            if (!byId.ContainsKey(o.GetId())) byId[o.GetId()] = o;
        }

        foreach (var o in byId.Values)
        {
            var id = o.GetId();
            var teklaId = o.GetTeklaId();
            var twin = teklaId is null ? null : teklaModel.SelectModelObject(new Identifier(teklaId));
            var hash = SyncHash.Of(o);
            if (twin is null)
            {
                plan.Items.Add(new SyncItem
                {
                    State = SyncState.New, Type = o.GetType().Name, CalculusId = id,
                    TeklaId = teklaId, Incoming = o, Hash = hash,
                });
                continue;
            }

            // The twin in Calculus form, through the same builder a transfer uses.
            var twinModel = CalculusModelBuilder.FromTeklaObjects([twin]);
            var current = twinModel.Objects.FirstOrDefault(x => x.GetType() == o.GetType());
            var currentHash = current is null ? null : SyncHash.Of(current);
            var baseline = TeklaUda.GetSyncHash(twin);
            var state = currentHash == hash ? SyncState.Unchanged
                : currentHash is null || baseline is null || baseline == currentHash ? SyncState.Updated
                : SyncState.Conflict;
            plan.Items.Add(new SyncItem
            {
                State = state, Type = o.GetType().Name, CalculusId = id,
                TeklaId = teklaId, Incoming = o, Existing = current, TeklaObject = twin, Hash = hash,
            });
        }

        // Gone: Tekla objects among the given ones linked to a Calculus object that
        // no longer exists. Asking every Tekla object for the UDA is slow, so the
        // whole model is scanned only when it is the scope.
        if (teklaObjects.Count > 0)
        {
            var calculusIds = new HashSet<string>((await client.GetAllObjectsAsync()).Select(e => e.GetId()));
            foreach (var t in teklaObjects)
            {
                var calcId = TeklaUda.GetCalculusId(t);
                if (calcId is null || calculusIds.Contains(calcId) || byId.ContainsKey(calcId)) continue;
                plan.Items.Add(new SyncItem
                {
                    State = SyncState.Gone, Type = t.GetType().Name, CalculusId = calcId,
                    TeklaId = t.Identifier.GUID.ToString(), TeklaObject = t,
                });
            }
        }

        return plan;
    }

    /// <summary>Do what the plan says for its included items.</summary>
    public static async Task<SyncResult> ApplyToTeklaAsync(
        CalculusBridgeClient client,
        SyncPlan plan,
        TSM.Model teklaModel)
    {
        var result = new SyncResult();
        var model = new Model { Objects = [] };
        var skipModify = new HashSet<string>();
        var leftOut = new HashSet<string>();
        var hashes = new Dictionary<string, string>();

        foreach (var item in plan.Items)
        {
            if (!item.Include && item.State != SyncState.Unchanged)
            {
                if (item.State == SyncState.Conflict) result.Conflicts++;
                else result.Excluded++;
                // A twin left as it is can still be the host a dependent needs
                // (a bolt's part): pass it on, resolved but not modified.
                if (item.State is SyncState.Updated or SyncState.Conflict)
                {
                    model.Objects.Add(item.Incoming!);
                    skipModify.Add(item.CalculusId);
                    leftOut.Add(item.CalculusId);
                }
                continue;
            }
            switch (item.State)
            {
                case SyncState.New:
                case SyncState.Updated:
                case SyncState.Conflict:
                    model.Objects.Add(item.Incoming!);
                    hashes[item.CalculusId] = item.Hash!;
                    break;
                case SyncState.Unchanged:
                    // Still passed to the inserter — a dependent may need it as a
                    // host — but it is resolved, not modified.
                    model.Objects.Add(item.Incoming!);
                    skipModify.Add(item.CalculusId);
                    hashes[item.CalculusId] = item.Hash!;
                    result.Skipped++;
                    break;
                case SyncState.Gone:
                    if (item.TeklaObject is not null)
                    {
                        if (item.TeklaObject.Delete()) result.Removed++;
                        else Log.Error("Failed to delete Tekla object {Guid}", item.TeklaId);
                    }
                    break;
            }
        }

        var inserted = CalculusModelInserter.UpdateTeklaModel(model, teklaModel, skipModify);

        // Write the link both ways: CALCULUS_ID + baseline on every resulting Tekla
        // object, and on the Calculus side the fresh Tekla GUID for new objects plus
        // the same baseline for everything written.
        var patches = new List<(string id, Dictionary<string, string> userdata)>();
        var udaChanged = false;
        var byId = model.Objects.ToDictionary(o => o.GetId());
        foreach (var kv in inserted.Objects)
        {
            var calcId = kv.Key;
            var teklaObj = kv.Value;
            // Left out of this transfer: its link and baseline stay as they were.
            if (leftOut.Contains(calcId)) continue;
            var hash = hashes.TryGetValue(calcId, out var h) ? h : null;

            var changed = false;
            if (TeklaUda.GetCalculusId(teklaObj) != calcId) changed |= TeklaUda.SetCalculusId(teklaObj, calcId);
            if (hash is not null && TeklaUda.GetSyncHash(teklaObj) != hash) changed |= TeklaUda.SetSyncHash(teklaObj, hash);
            if (changed)
            {
                teklaObj.Modify();
                udaChanged = true;
            }

            if (!byId.TryGetValue(calcId, out var calcObj)) continue;
            var isNew = inserted.NewlyInserted.Contains(calcId);
            if (isNew) result.Added++;
            else if (!skipModify.Contains(calcId)) result.Updated++;

            var ud = new Dictionary<string, string>(calcObj.GetUserdata());
            var udChanged = false;
            if (isNew)
            {
                ud["tekla_id"] = teklaObj.Identifier.GUID.ToString();
                udChanged = true;
            }
            if (hash is not null && (!ud.TryGetValue(SyncHash.UserdataKey, out var had) || had != hash))
            {
                ud[SyncHash.UserdataKey] = hash;
                udChanged = true;
            }
            if (udChanged) patches.Add((calcId, ud));
        }
        if (udaChanged || result.Removed > 0) teklaModel.CommitChanges();
        await client.PatchUserdataAsync(patches);

        Log.Information("Direct transfer to Tekla: {Result}", result);
        return result;
    }

    private static void Stamp(ModelObject o, string hash)
    {
        o.GetUserdata()[SyncHash.UserdataKey] = hash;
    }
}
