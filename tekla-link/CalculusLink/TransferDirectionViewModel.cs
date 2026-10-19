using System.Collections.ObjectModel;

using CalculusModel;

using CalculusLink.Bridge;

using CommunityToolkit.Mvvm.ComponentModel;

using TSM = Tekla.Structures.Model;

namespace CalculusLink;

/// <summary>
/// One preview row: an object a transfer would add, update, overwrite or
/// remove. <see cref="Include"/> is the item's own flag, so Apply reads what
/// the table shows.
/// </summary>
public partial class PreviewRow : ObservableObject
{
    public PreviewRow(SyncItem item)
    {
        Item = item;
        (Name, Profile) = Describe(item);
    }

    public SyncItem Item { get; }
    public SyncState State => Item.State;
    public string Type => Item.Type;
    public string Name { get; }
    public string Profile { get; }
    public string CalculusId => Item.CalculusId;

    /// <summary>Raised when the user (or a cascade) flips <see cref="Include"/>.</summary>
    internal event Action<PreviewRow>? IncludeChanged;

    public bool Include
    {
        get => Item.Include;
        set
        {
            if (Item.Include == value) return;
            Item.Include = value;
            OnPropertyChanged();
            IncludeChanged?.Invoke(this);
        }
    }

    private static (string name, string profile) Describe(SyncItem item) => item.Object switch
    {
        Beam b => (b.Name, b.Profile),
        Plate p => (p.Name, p.Profile),
        CutPart { Cutter: CutBeam c } => (c.Name, c.Profile),
        CutPart { Cutter: CutPlate c } => (c.Name, c.Profile),
        Bolt b => (string.Empty, $"{b.Standard} {b.Size:0.#}"),
        Rebar r => (r.Name, $"Ø{r.Size:0.#}"),
        // A Tekla twin whose Calculus object is gone has no Calculus form.
        null when item.TeklaObject is TSM.Part p => (p.Name, p.Profile.ProfileString),
        _ => (string.Empty, string.Empty),
    };
}

/// <summary>
/// One transfer direction's card: scope, coordinate system, the defaults for
/// conflicts and gone twins, and the preview — the rows Apply will write.
/// The commands live on <see cref="MainWindowViewModel"/>, which owns the
/// Tekla and Calculus connections; this holds what the card shows.
/// </summary>
/// <summary>A scope as the dropdown shows it.</summary>
public class ScopeOption(TransferScope value, string label)
{
    public TransferScope Value { get; } = value;
    public string Label { get; } = label;
}

public partial class TransferDirectionViewModel(SyncDirection direction) : ObservableObject
{
    public SyncDirection Direction { get; } = direction;
    public bool ToCalculus => Direction == SyncDirection.TeklaToCalculus;

    [ObservableProperty]
    public partial TransferScope Scope { get; set; } = TransferScope.Selected;

    [ObservableProperty]
    public partial CoordinateSystemType CoordinateSystem { get; set; } = CoordinateSystemType.Global;

    [ObservableProperty]
    public partial bool OverwriteConflicts { get; set; }

    [ObservableProperty]
    public partial bool RemoveGone { get; set; }

    /// <summary>A changed scope or coordinate system makes the preview describe something else.</summary>
    internal event Action? SourceChanged;

    partial void OnScopeChanged(TransferScope value) => SourceChanged?.Invoke();
    partial void OnCoordinateSystemChanged(CoordinateSystemType value) => SourceChanged?.Invoke();

    // The options are the defaults of the rows' checkboxes: flipping one flips its rows.
    partial void OnOverwriteConflictsChanged(bool value) => SetIncluded(SyncState.Conflict, value);
    partial void OnRemoveGoneChanged(bool value) => SetIncluded(SyncState.Gone, value);

    public SyncOptions Options => new() { OverwriteConflicts = OverwriteConflicts, RemoveGone = RemoveGone };

    // --- The preview ---------------------------------------------------------

    [ObservableProperty]
    public partial bool HasPreview { get; set; }

    [ObservableProperty]
    public partial string PreviewText { get; set; } = string.Empty;

    /// <summary>"Apply 12 changes", or "Nothing to apply".</summary>
    [ObservableProperty]
    public partial string ApplyText { get; set; } = "Apply";

    public ObservableCollection<PreviewRow> Rows { get; } = [];

    /// <summary>The rows selected in the table (set by the view).</summary>
    public IReadOnlyList<PreviewRow> SelectedRows { get; private set; } = [];

    internal event Action<TransferDirectionViewModel>? SelectionChanged;

    /// <summary>Include / Leave out act on the selection, so they need one.</summary>
    [ObservableProperty]
    public partial bool HasSelectedRows { get; set; }

    public void SetSelectedRows(IEnumerable<PreviewRow> rows)
    {
        SelectedRows = [.. rows];
        HasSelectedRows = SelectedRows.Count > 0;
        SelectionChanged?.Invoke(this);
    }

    /// <summary>The previewed plan, and the source it was read from — what Apply re-reads.</summary>
    internal SyncPlan? Plan { get; private set; }
    internal PreviewSource? Source { get; private set; }

    internal void ShowPreview(SyncPlan plan, PreviewSource source, string text)
    {
        foreach (var row in Rows) row.IncludeChanged -= OnRowIncludeChanged;
        Rows.Clear();
        foreach (var item in plan.Items.Where(i => i.State != SyncState.Unchanged))
        {
            var row = new PreviewRow(item);
            row.IncludeChanged += OnRowIncludeChanged;
            Rows.Add(row);
        }
        Plan = plan;
        Source = source;
        PreviewText = text;
        HasPreview = true;
        SelectedRows = [];
        HasSelectedRows = false;
        UpdateApplyText();
    }

    internal void ClearPreview()
    {
        foreach (var row in Rows) row.IncludeChanged -= OnRowIncludeChanged;
        Rows.Clear();
        Plan = null;
        Source = null;
        SelectedRows = [];
        HasSelectedRows = false;
        HasPreview = false;
        UpdateApplyText();
    }

    public void SetIncluded(IEnumerable<PreviewRow> rows, bool include)
    {
        foreach (var row in rows.ToList()) row.Include = include;
    }

    private void SetIncluded(SyncState state, bool include) =>
        SetIncluded(Rows.Where(r => r.State == state), include);

    /// <summary>
    /// Keep the included rows writable: an object that is not on the target yet
    /// (New) is the host its dependents need — a bolt cannot be added without its
    /// parts. Leaving a new part out leaves its new dependents out; taking a
    /// dependent in takes its new hosts in.
    /// </summary>
    private void OnRowIncludeChanged(PreviewRow row)
    {
        var byId = Rows.GroupBy(r => r.CalculusId).ToDictionary(g => g.Key, g => g.First());
        if (!row.Include && row.State == SyncState.New)
        {
            foreach (var other in Rows.Where(r => r.Include && r.Item.Object is { } o && o.GetReferenceIds().Contains(row.CalculusId)))
                other.Include = false;
        }
        else if (row.Include && row.Item.Object is { } o)
        {
            foreach (var id in o.GetReferenceIds())
            {
                if (byId.TryGetValue(id, out var host) && host.State == SyncState.New) host.Include = true;
            }
        }
        UpdateApplyText();
    }

    private void UpdateApplyText()
    {
        var count = Rows.Count(r => r.Include);
        ApplyText = count switch
        {
            0 => "Apply",
            1 => "Apply 1 change",
            _ => $"Apply {count} changes",
        };
    }
}

/// <summary>
/// What a preview read: its scope and coordinate system, and for a selection
/// the ids of the objects it held — Apply reads those again, not the
/// selection, which selecting preview rows moves.
/// </summary>
internal sealed class PreviewSource
{
    public TransferScope Scope { get; set; }
    public CoordinateSystemType CoordinateSystem { get; set; }
    /// <summary>The Tekla objects read (Tekla → Calculus), or searched for gone twins (Calculus → Tekla); null = the whole model.</summary>
    public List<string>? TeklaIds { get; set; }
    /// <summary>The Calculus objects read (Calculus → Tekla); null = the whole model.</summary>
    public List<string>? CalculusIds { get; set; }
}
