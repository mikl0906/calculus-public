using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CalculusModel;

/// <summary>
/// A content hash of a model object, the same on both sides of a transfer:
/// what the object IS, not what it is called or linked to. The canonical form
/// is the object's JSON with <c>id</c>, <c>userdata</c> and every id
/// reference (<c>*Id</c>, <c>*Ids</c>) dropped, keys sorted, and numbers
/// rounded to four decimals so Tekla's floating-point noise does not read as
/// a change (0.0001 mm, 0.0001 rad).
///
/// Twins keep the hash they were last transferred with as a baseline —
/// <c>userdata.tekla_sync</c> on the Calculus object, the <c>CALCULUS_SYNC</c>
/// UDA on the Tekla object — which is what tells an update from a conflict
/// (https://docs.calculus.build/reference/tekla-plugin/sync-behavior).
/// </summary>
public static class SyncHash
{
    /// <summary>The Calculus userdata key holding the baseline hash.</summary>
    public const string UserdataKey = "tekla_sync";

    public static string Of(ModelObject o)
    {
        var json = CalculusModelSerializer.SerializeObjects([o]);
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        Canonical(doc.RootElement[0], sb);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        var hex = new StringBuilder(16);
        for (var i = 0; i < 8; i++) hex.Append(bytes[i].ToString("x2"));
        return hex.ToString();
    }

    /// <summary>The baseline stored on a Calculus object, or null.</summary>
    public static string? Baseline(ModelObject o) =>
        o.GetUserdata().TryGetValue(UserdataKey, out var h) && !string.IsNullOrEmpty(h) ? h : null;

    private static bool IsReference(string key) =>
        key == "id" || key == "userdata" || key.EndsWith("Id", StringComparison.Ordinal) || key.EndsWith("Ids", StringComparison.Ordinal);

    private static void Canonical(JsonElement e, StringBuilder sb)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var first = true;
                foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (IsReference(p.Name)) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(p.Name).Append(':');
                    Canonical(p.Value, sb);
                }
                sb.Append('}');
                break;
            case JsonValueKind.Array:
                sb.Append('[');
                var firstItem = true;
                foreach (var item in e.EnumerateArray())
                {
                    if (!firstItem) sb.Append(',');
                    firstItem = false;
                    Canonical(item, sb);
                }
                sb.Append(']');
                break;
            case JsonValueKind.Number:
                var d = e.GetDouble();
                var r = Math.Round(d, 4);
                if (r == 0) r = 0; // no "-0"
                sb.Append(r.ToString("0.####", CultureInfo.InvariantCulture));
                break;
            case JsonValueKind.String:
                sb.Append('"').Append(e.GetString()).Append('"');
                break;
            case JsonValueKind.True:
                sb.Append("true");
                break;
            case JsonValueKind.False:
                sb.Append("false");
                break;
            default:
                sb.Append("null");
                break;
        }
    }
}
