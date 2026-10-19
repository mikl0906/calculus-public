using Serilog;

using TSM = Tekla.Structures.Model;
using TSUI = Tekla.Structures.Model.UI;

namespace CalculusLink.Bridge;

/// <summary>
/// Shows a <see cref="SyncPlan"/> in both models before anything is written
/// (docs/tekla-link-sync.md): in Calculus through the document's colour-by-rule
/// list (rules named <c>tekla-sync:*</c>, id lists, placed first so they win),
/// in Tekla through temporary states on the twins. Both models are ghosted
/// meanwhile so the coloured objects stand out: Calculus switches to the
/// "ghost" structural representation (rule-lit parts stay solid), Tekla turns
/// everything transparent but the coloured twins. New objects can only be
/// shown on the side that already has them.
/// </summary>
public static class SyncPreview
{
    public const string Scope = "tekla-sync:";
    /// <summary>Past this many objects the colouring is skipped; the table still lists them.</summary>
    public const int MaxColoured = 2000;

    private static bool _teklaGhosted;
    /// <summary>The Calculus representation before the preview ghosted it; null while none is shown.</summary>
    private static string? _previousRepresentation;

    private sealed class Swatch(SyncState state, string hex, double r, double g, double b)
    {
        public SyncState State { get; } = state;
        public string Hex { get; } = hex;
        public double R { get; } = r;
        public double G { get; } = g;
        public double B { get; } = b;
    }

    private static readonly Swatch[] Palette =
    [
        new(SyncState.New, "#3fb950", 0.25, 0.73, 0.31),
        new(SyncState.Updated, "#d8a630", 0.85, 0.65, 0.19),
        new(SyncState.Conflict, "#f0883e", 0.94, 0.53, 0.24),
        new(SyncState.Gone, "#f85149", 0.97, 0.32, 0.29),
    ];

    /// <summary>Colour the plan in both models. Returns true when the colouring was applied, false when the plan was too large.</summary>
    public static async Task<bool> ShowAsync(CalculusBridgeClient client, SyncPlan plan)
    {
        await ClearAsync(client);
        if (plan.Items.Count > MaxColoured) return false;

        // Calculus: the objects that exist there. Tekla -> Calculus: twins (Updated,
        // Conflict, Gone); Calculus -> Tekla: the selected objects too, New included.
        var rules = new List<(string name, string query, string color)>();
        foreach (var sw in Palette)
        {
            var ids = plan.Of(sw.State)
                .Where(i => sw.State != SyncState.New || plan.Direction == SyncDirection.CalculusToTekla)
                .Where(i => sw.State != SyncState.Gone || plan.Direction == SyncDirection.TeklaToCalculus)
                .Select(i => i.CalculusId)
                .Distinct()
                .ToList();
            if (ids.Count == 0) continue;
            var list = string.Join(", ", ids.Select(id => $"\"{id}\""));
            rules.Add(($"{Scope}{sw.State.ToString().ToLowerInvariant()}", $"id in ({list})", sw.Hex));
        }
        await client.SetColorRulesAsync(Scope, rules);
        if (rules.Count > 0)
        {
            _previousRepresentation = await client.SetStructuralRepresentationAsync("ghost");
        }

        // Tekla: the twins on this side. Tekla -> Calculus: the selected objects (New
        // included), Calculus -> Tekla: existing twins and the Gone ones.
        var coloured = new List<TSM.ModelObject>();
        foreach (var sw in Palette)
        {
            var objects = plan.Of(sw.State)
                .Where(i => sw.State != SyncState.New || plan.Direction == SyncDirection.TeklaToCalculus)
                .Where(i => sw.State != SyncState.Gone || plan.Direction == SyncDirection.CalculusToTekla)
                .Select(i => i.TeklaObject)
                .OfType<TSM.ModelObject>()
                .ToList();
            if (objects.Count == 0) continue;
            TSUI.ModelObjectVisualization.SetTemporaryState(objects, new TSUI.Color(sw.R, sw.G, sw.B));
            coloured.AddRange(objects);
        }
        if (coloured.Count > 0)
        {
            TSUI.ModelObjectVisualization.SetTransparencyForAll(TSUI.TemporaryTransparency.TRANSPARENT);
            TSUI.ModelObjectVisualization.SetTransparency(coloured, TSUI.TemporaryTransparency.VISIBLE);
            _teklaGhosted = true;
        }
        return true;
    }

    /// <summary>Remove the preview colours and ghosting from both models; the user's own rules stay.</summary>
    public static async Task ClearAsync(CalculusBridgeClient client)
    {
        if (_teklaGhosted)
        {
            // Drops every temporary colour and transparency in the view, the
            // preview's and any set before it.
            try { TSUI.ModelObjectVisualization.ClearAllTemporaryStates(); }
            catch (Exception ex) { Log.Warning(ex, "Clearing the Tekla preview failed"); }
            _teklaGhosted = false;
        }
        try { await client.SetColorRulesAsync(Scope, []); }
        catch { /* the editor may have closed since the preview */ }
        if (_previousRepresentation is not null)
        {
            try { await client.SetStructuralRepresentationAsync(_previousRepresentation); }
            catch { /* the editor may have closed since the preview */ }
            _previousRepresentation = null;
        }
    }
}
