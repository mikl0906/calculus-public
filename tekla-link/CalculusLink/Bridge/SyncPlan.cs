using CalculusModel;

using TSM = Tekla.Structures.Model;

namespace CalculusLink.Bridge;

/// <summary>What a transfer would do with one object (https://docs.calculus.build/reference/tekla-plugin/sync-behavior).</summary>
public enum SyncState
{
    /// <summary>No twin on the target: will be added.</summary>
    New,
    /// <summary>Twin equals the incoming object: nothing to do.</summary>
    Unchanged,
    /// <summary>Twin differs and the target side did not change since the last transfer.</summary>
    Updated,
    /// <summary>Twin differs and the target side changed too since the last transfer.</summary>
    Conflict,
    /// <summary>The target holds a twin whose source object no longer exists.</summary>
    Gone,
}

public enum SyncDirection
{
    TeklaToCalculus,
    CalculusToTekla,
}

/// <summary>One object in a plan.</summary>
public class SyncItem
{
    public SyncState State { get; set; }
    /// <summary>Calculus object type, e.g. <c>Beam</c>.</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>The Calculus id (the twin's, once matched).</summary>
    public string CalculusId { get; set; } = string.Empty;
    public string? TeklaId { get; set; }
    /// <summary>The object as it would be written to the target (null for Gone).</summary>
    public ModelObject? Incoming { get; set; }
    /// <summary>The target's current twin, Calculus form (null for New).</summary>
    public ModelObject? Existing { get; set; }
    /// <summary>The Tekla object involved, when there is one on this machine.</summary>
    public TSM.ModelObject? TeklaObject { get; set; }
    /// <summary>The hash the target will carry after an apply.</summary>
    public string? Hash { get; set; }

    public override string ToString() => $"{State} {Type} {CalculusId.Substring(0, Math.Min(8, CalculusId.Length))}";
}

/// <summary>A classified transfer: what Apply will do per object.</summary>
public class SyncPlan
{
    public SyncDirection Direction { get; set; }
    public List<SyncItem> Items { get; } = [];
    /// <summary>Objects the selection held that the mappers cannot transfer.</summary>
    public int NotTransferable { get; set; }

    public int Count(SyncState state) => Items.Count(i => i.State == state);

    public IEnumerable<SyncItem> Of(SyncState state) => Items.Where(i => i.State == state);

    /// <summary>"3 new, 2 updated, 5 unchanged, 1 conflict, 1 gone" — only the non-zero parts.</summary>
    public string Summary()
    {
        var parts = new List<string>();
        void Add(SyncState s, string word)
        {
            var n = Count(s);
            if (n > 0) parts.Add($"{n} {word}");
        }
        Add(SyncState.New, "new");
        Add(SyncState.Updated, "updated");
        Add(SyncState.Unchanged, "unchanged");
        Add(SyncState.Conflict, "in conflict");
        Add(SyncState.Gone, "gone on the other side");
        if (NotTransferable > 0) parts.Add($"{NotTransferable} not transferable");
        return parts.Count == 0 ? "nothing to transfer" : string.Join(", ", parts);
    }
}

/// <summary>What Apply may do beyond add and update.</summary>
public class SyncOptions
{
    /// <summary>Write conflicting objects too, overwriting the target's own changes.</summary>
    public bool OverwriteConflicts { get; set; }
    /// <summary>Remove twins whose source object no longer exists.</summary>
    public bool RemoveGone { get; set; }
}

/// <summary>What Apply did, for the status line.</summary>
public class SyncResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Conflicts { get; set; }
    public int Removed { get; set; }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Added > 0) parts.Add($"{Added} added");
        if (Updated > 0) parts.Add($"{Updated} updated");
        if (Removed > 0) parts.Add($"{Removed} removed");
        if (Skipped > 0) parts.Add($"{Skipped} already up to date");
        if (Conflicts > 0) parts.Add($"{Conflicts} left in conflict");
        return parts.Count == 0 ? "Nothing to do — everything is up to date" : string.Join(", ", parts);
    }
}
