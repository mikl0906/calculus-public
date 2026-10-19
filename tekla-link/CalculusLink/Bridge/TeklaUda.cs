using CalculusModel;

using TSM = Tekla.Structures.Model;

namespace CalculusLink.Bridge;

/// <summary>
/// Reads/writes the cross-reference link on the Tekla side: the Calculus object id
/// is stored as the <c>CALCULUS_ID</c> user-defined attribute. This is best-effort
/// — <c>SetUserProperty</c> works at runtime even when the UDA is not declared in
/// the environment, but declaring it (objects.inp / *.uda) is recommended so it
/// survives model save/reload and is visible in dialogs. Twin matching does not
/// depend on it (the Calculus side's <c>userdata.tekla_id</c> is the reliable
/// anchor); the UDA just lets a Tekla object self-identify its twin.
/// </summary>
public static class TeklaUda
{
    public const string CalculusId = "CALCULUS_ID";
    /// <summary>The <see cref="SyncHash"/> the twin was last transferred with.</summary>
    public const string CalculusSync = "CALCULUS_SYNC";

    public static string? GetSyncHash(TSM.ModelObject obj)
    {
        string value = "";
        return obj.GetUserProperty(CalculusSync, ref value) && !string.IsNullOrEmpty(value)
            ? value
            : null;
    }

    public static bool SetSyncHash(TSM.ModelObject obj, string hash)
    {
        return obj.SetUserProperty(CalculusSync, hash);
    }

    public static string? GetCalculusId(TSM.ModelObject obj)
    {
        string value = "";
        return obj.GetUserProperty(CalculusId, ref value) && !string.IsNullOrEmpty(value)
            ? value
            : null;
    }

    public static bool SetCalculusId(TSM.ModelObject obj, string id)
    {
        return obj.SetUserProperty(CalculusId, id);
    }
}
