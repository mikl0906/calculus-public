namespace CalculusModel;

/// <summary>
/// Helpers for treating the polymorphic <see cref="ModelObject"/> hierarchy
/// uniformly. <c>ModelObject</c> is an empty abstract base — every concrete type
/// declares its own <c>Id</c> and <c>Userdata</c> — so generic access (needed by
/// the Hub transfer logic for twin matching and reference remapping) goes through
/// these switch-based accessors.
/// </summary>
public static class ModelObjectExtensions
{
    public static string GetId(this ModelObject o) => o switch
    {
        Beam b => b.Id,
        Plate p => p.Id,
        CutPart c => c.Id,
        CutPlane c => c.Id,
        Bolt b => b.Id,
        Weld w => w.Id,
        Rebar r => r.Id,
        Grid g => g.Id,
        Line l => l.Id,
        _ => throw new NotSupportedException($"GetId: unsupported type {o.GetType().Name}"),
    };

    public static void SetId(this ModelObject o, string id)
    {
        switch (o)
        {
            case Beam b: b.Id = id; break;
            case Plate p: p.Id = id; break;
            case CutPart c: c.Id = id; break;
            case CutPlane c: c.Id = id; break;
            case Bolt b: b.Id = id; break;
            case Weld w: w.Id = id; break;
            case Rebar r: r.Id = id; break;
            case Grid g: g.Id = id; break;
            case Line l: l.Id = id; break;
            default: throw new NotSupportedException($"SetId: unsupported type {o.GetType().Name}");
        }
    }

    public static Dictionary<string, string> GetUserdata(this ModelObject o) => o switch
    {
        Beam b => b.Userdata,
        Plate p => p.Userdata,
        CutPart c => c.Userdata,
        CutPlane c => c.Userdata,
        Bolt b => b.Userdata,
        Weld w => w.Userdata,
        Rebar r => r.Userdata,
        Grid g => g.Userdata,
        Line l => l.Userdata,
        _ => throw new NotSupportedException($"GetUserdata: unsupported type {o.GetType().Name}"),
    };

    /// <summary>The Tekla GUID this object is twinned with, or null.</summary>
    public static string? GetTeklaId(this ModelObject o) =>
        o.GetUserdata().TryGetValue("tekla_id", out var id) && !string.IsNullOrEmpty(id) ? id : null;

    /// <summary>
    /// Rewrite the object's id references (a parent's id may have changed when it
    /// was matched to an existing Calculus twin) so children still point at them.
    /// </summary>
    public static void RemapReferences(this ModelObject o, IReadOnlyDictionary<string, string> remap)
    {
        if (remap.Count == 0) return;
        string M(string id) => remap.TryGetValue(id, out var v) ? v : id;
        switch (o)
        {
            case CutPart c: c.PartIds = [.. c.PartIds.Select(M)]; break;
            case CutPlane c: c.PartIds = [.. c.PartIds.Select(M)]; break;
            case Bolt b: b.PartIds = [.. b.PartIds.Select(M)]; break;
            case Weld w: w.PartIds = [.. w.PartIds.Select(M)]; break;
            case Rebar r: r.FatherId = M(r.FatherId); break;
        }
    }
}
