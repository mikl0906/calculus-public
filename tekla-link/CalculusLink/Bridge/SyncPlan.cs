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
    /// <summary>
    /// Whether Apply writes this item: adds New, updates Updated, overwrites
    /// Conflict, removes Gone. Unchanged items are never written either way.
    /// Starts per <see cref="SyncPlan.ApplyDefaults"/>; the preview's checkboxes change it.
    /// </summary>
    public bool Include { get; set; } = true;

    /// <summary>The object this item is about, in Calculus form: what is sent, else the twin.</summary>
    public ModelObject? Object => Incoming ?? Existing;

    /// <summary>
    /// Identifies the same change across two classifications of the same source:
    /// a preview and the Apply that re-reads the models before writing. A New
    /// object from Tekla gets a fresh Calculus id per classification, so it is
    /// known by its Tekla GUID (a control line carries none: by its content).
    /// </summary>
    public string Key => State == SyncState.New
        ? $"New|{TeklaId ?? Type}|{Hash}"
        : $"{State}|{CalculusId}|{TeklaId}|{Hash}";

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

    /// <summary>
    /// What Apply writes unless the preview says otherwise: new and updated
    /// objects always, conflicts and gone twins only when the options allow.
    /// </summary>
    public void ApplyDefaults(SyncOptions options)
    {
        foreach (var item in Items) item.Include = IncludedByDefault(item.State, options);
    }

    public static bool IncludedByDefault(SyncState state, SyncOptions options) => state switch
    {
        SyncState.Conflict => options.OverwriteConflicts,
        SyncState.Gone => options.RemoveGone,
        _ => true,
    };

    /// <summary>The keys of the items that would change something, for telling whether the models moved under a preview.</summary>
    public HashSet<string> ChangeKeys() =>
        [.. Items.Where(i => i.State != SyncState.Unchanged).Select(i => i.Key)];

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

/// <summary>What Apply may do beyond add and update — the defaults of a preview's checkboxes.</summary>
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
    /// <summary>Changes left out of the transfer (unchecked in the preview, conflicts not overwritten, gone twins kept).</summary>
    public int Excluded { get; set; }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Added > 0) parts.Add($"{Added} added");
        if (Updated > 0) parts.Add($"{Updated} updated");
        if (Removed > 0) parts.Add($"{Removed} removed");
        if (Skipped > 0) parts.Add($"{Skipped} already up to date");
        if (Conflicts > 0) parts.Add($"{Conflicts} left in conflict");
        if (Excluded > 0) parts.Add($"{Excluded} left out");
        return parts.Count == 0 ? "Nothing to do — everything is up to date" : string.Join(", ", parts);
    }
}
