using CalculusModel;

using Tekla.Structures;

using TSM = Tekla.Structures.Model;
using TSUI = Tekla.Structures.Model.UI;

namespace CalculusLink.Bridge;

/// <summary>What a transfer reads from its source model.</summary>
public enum TransferScope
{
    Selected,
    WholeModel,
}

/// <summary>
/// The objects a transfer reads, per side. A component stands for its
/// children on both sides — a Tekla component (connection, detail, custom
/// part…) is resolved to the parts, bolts and welds it made, a Calculus
/// component instance to its child objects — since neither side mirrors the
/// other's components. A preview remembers the ids it read, so the Apply that
/// follows reads the same objects even after the selection moved (selecting a
/// preview row selects in both models).
/// </summary>
public static class TransferSource
{
    // --- Tekla -------------------------------------------------------------

    /// <summary>The selection or every object of the model, components resolved to their children.</summary>
    public static List<TSM.ModelObject> TeklaObjects(TSM.Model teklaModel, TransferScope scope)
    {
        var objects = scope == TransferScope.WholeModel
            ? teklaModel.GetModelObjectSelector().GetAllObjects()
            : new TSUI.ModelObjectSelector().GetSelectedObjects();
        return Resolve(Enumerate(objects));
    }

    /// <summary>The objects with these GUIDs that still exist.</summary>
    public static List<TSM.ModelObject> TeklaObjects(TSM.Model teklaModel, IEnumerable<string> guids) =>
        Resolve(guids
            .Select(g => teklaModel.SelectModelObject(new Identifier(g)))
            .OfType<TSM.ModelObject>());

    private static IEnumerable<TSM.ModelObject> Enumerate(TSM.ModelObjectEnumerator e)
    {
        while (e.MoveNext())
        {
            if (e.Current is TSM.ModelObject o) yield return o;
        }
    }

    private static List<TSM.ModelObject> Resolve(IEnumerable<TSM.ModelObject> objects)
    {
        var result = new List<TSM.ModelObject>();
        var seen = new HashSet<Guid>();
        void Add(TSM.ModelObject o)
        {
            if (!seen.Add(o.Identifier.GUID)) return;
            switch (o)
            {
                case TSM.BaseComponent component:
                    foreach (var child in Enumerate(component.GetChildren())) Add(child);
                    return;
                case TSM.Assembly assembly:
                    if (assembly.GetMainPart() is TSM.ModelObject main) Add(main);
                    foreach (var part in assembly.GetSecondaries().OfType<TSM.ModelObject>()) Add(part);
                    foreach (var sub in assembly.GetSubAssemblies().OfType<TSM.ModelObject>()) Add(sub);
                    return;
                // Cuts and fittings travel with their part (CalculusModelBuilder.GetBooleans),
                // and a cut's operative part is not a part of the model.
                case TSM.Boolean:
                    return;
                case TSM.Part part when part.Class == TSM.BooleanPart.BooleanOperativeClassName:
                    return;
            }
            result.Add(o);
        }
        foreach (var o in objects) Add(o);
        return result;
    }

    // --- Calculus ----------------------------------------------------------

    /// <summary>The selection or every object of the model, component instances resolved to their children.</summary>
    public static async Task<List<ModelObject>> CalculusObjects(CalculusBridgeClient client, TransferScope scope)
    {
        // The whole model lists the children themselves (they are ordinary objects).
        if (scope == TransferScope.WholeModel) return await client.GetAllObjectsAsync();

        var (ids, objects) = await client.GetSelectionAsync();
        var byId = new Dictionary<string, ModelObject>();
        foreach (var o in objects) byId[o.GetId()] = o;

        // What did not come through is a component instance or an object this
        // side does not mirror; only instances have children.
        var pending = ids.Where(id => !byId.ContainsKey(id)).ToList();
        var visited = new HashSet<string>(pending);
        while (pending.Count > 0)
        {
            var (children, nested) = await client.GetComponentChildrenAsync(pending);
            foreach (var child in children) byId[child.GetId()] = child;
            pending = [.. nested.Where(visited.Add)];
        }
        return [.. byId.Values];
    }

    /// <summary>The objects with these ids that still exist.</summary>
    public static async Task<List<ModelObject>> CalculusObjects(CalculusBridgeClient client, IEnumerable<string> ids)
    {
        var wanted = new HashSet<string>(ids);
        return [.. (await client.GetAllObjectsAsync()).Where(o => wanted.Contains(o.GetId()))];
    }
}
