using CalculusModel;

using Serilog;

using TSM = Tekla.Structures.Model;

namespace CalculusLink;

public static class CalculusModelBuilder
{
    public static Model FromTeklaObjects(List<TSM.ModelObject> teklaObjects)
    {
        var model = new Model
        {
            Objects = []
        };

        // Map of Tekla GUIDs to Calculus IDs
        Dictionary<Guid, string> teklaToCalculusIdMap = [];

        // First, map primary objects (that do not depend on other objects)
        foreach (var teklaObject in teklaObjects)
        {
            if (teklaObject is TSM.Beam teklaBeam)
            {
                var beam = new Beam();
                CalculusTeklaMapper.ToCalculusBeam(teklaBeam, beam);

                teklaToCalculusIdMap[teklaBeam.Identifier.GUID] = beam.Id;

                model.Objects.Add(beam);
                model.Objects.AddRange(GetBooleans(teklaBeam, [beam.Id]));
            }
            else if (teklaObject is TSM.ContourPlate teklaPlate)
            {
                var plate = new Plate();
                CalculusTeklaMapper.ToCalculusPlate(teklaPlate, plate);

                teklaToCalculusIdMap[teklaPlate.Identifier.GUID] = plate.Id;

                model.Objects.Add(plate);
                model.Objects.AddRange(GetBooleans(teklaPlate, [plate.Id]));
            }
            else if (teklaObject is TSM.Grid teklaGrid)
            {
                var grid = new Grid();
                CalculusTeklaMapper.ToCalculusGrid(teklaGrid, grid);
                model.Objects.Add(grid);
            }
            else if (teklaObject is TSM.ControlLine teklaLine)
            {
                var line = new Line();
                CalculusTeklaMapper.ToCalculusLine(teklaLine, line);
                model.Objects.Add(line);
            }
        }

        // Then, map secondary objects (that depend on primary objects)
        foreach (var teklaObject in teklaObjects)
        {
            if (teklaObject is TSM.BoltGroup teklaBolt)
            {
                List<Guid> teklaPartIds = [
                    teklaBolt.PartToBoltTo.Identifier.GUID,
                    teklaBolt.PartToBeBolted.Identifier.GUID,
                    ..teklaBolt.GetOtherPartsToBolt().OfType<TSM.Part>().Select(p => p.Identifier.GUID)
                ];
                string[] partIds = [.. teklaPartIds
                    .Select(guid => teklaToCalculusIdMap.TryGetValue(guid, out var v) ? v : guid.ToString())];

                var bolt = new Bolt();
                CalculusTeklaMapper.ToCalculusBolt(teklaBolt, bolt, partIds);
                model.Objects.Add(bolt);
            }
            else if (teklaObject is TSM.BaseWeld teklaWeld)
            {
                List<Guid> teklaPartIds = [
                    teklaWeld.MainObject.Identifier.GUID,
                    teklaWeld.SecondaryObject.Identifier.GUID,
                ];
                string[] partIds = [.. teklaPartIds
                    .Select(guid => teklaToCalculusIdMap.TryGetValue(guid, out var v) ? v : guid.ToString())];

                var weld = new Weld();
                CalculusTeklaMapper.ToCalculusWeld(teklaWeld, weld, partIds);
                model.Objects.Add(weld);
            }
            else if (teklaObject is TSM.Reinforcement teklaRebar)
            {
                if (teklaRebar is not TSM.SingleRebar and not TSM.RebarGroup)
                {
                    Log.Warning("Skipping unsupported Reinforcement type: {TypeName}", teklaRebar.GetType().Name);
                    continue;
                }
                Guid teklaFatherId = teklaRebar.Father.Identifier.GUID;
                string fatherId = teklaToCalculusIdMap.TryGetValue(teklaFatherId, out var v) ? v : teklaFatherId.ToString();

                var rebar = new Rebar();
                CalculusTeklaMapper.ToCalculusRebar(teklaRebar, rebar, fatherId);
                model.Objects.Add(rebar);
            }
        }

        Log.Information("Exported {Count} objects.", model.Objects.Count);

        return model;
    }

    static IEnumerable<ModelObject> GetBooleans(TSM.Part modelObject, string[] partIds)
    {
        var booleans = modelObject.GetBooleans();
        foreach (var boolean in booleans)
        {
            if (boolean is TSM.BooleanPart teklaBooleanPart)
            {
                if (teklaBooleanPart.OperativePart is not TSM.Beam and not TSM.ContourPlate)
                {
                    Log.Warning("Skipping unsupported BooleanPart operative part type: {TypeName}", teklaBooleanPart.OperativePart.GetType().Name);
                    continue;
                }
                var cutPart = new CutPart();
                CalculusTeklaMapper.ToCaclulusCutPart(teklaBooleanPart, cutPart, partIds);
                yield return cutPart;

            }
            else if (boolean is TSM.CutPlane teklaCutPlane)
            {
                var cutPlane = new CutPlane();
                CalculusTeklaMapper.ToCalculusCutPlane(teklaCutPlane, cutPlane, partIds);
                yield return cutPlane;
            }
            else if (boolean is TSM.Fitting teklaFitting)
            {
                var cutPlane = new CutPlane();
                CalculusTeklaMapper.ToCalculusFitting(teklaFitting, cutPlane, partIds);
                yield return cutPlane;
            }
        }
    }
}