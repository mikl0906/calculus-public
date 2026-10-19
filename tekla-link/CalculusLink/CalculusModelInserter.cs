using System.Diagnostics;

using CalculusModel;

using Serilog;

using Tekla.Structures;

using TSM = Tekla.Structures.Model;

namespace CalculusLink;

/// <summary>
/// Maps each Calculus object id to the Tekla object it was inserted/modified into,
/// and tracks which ones were newly inserted (so the direct-transfer flow can write
/// the <c>CALCULUS_ID</c> UDA back and, for new objects, push the resulting Tekla
/// GUID to Calculus as <c>userdata.tekla_id</c>). The file-import path ignores it.
/// </summary>
public class InsertResult
{
    public Dictionary<string, TSM.ModelObject> Objects { get; } = [];
    public HashSet<string> NewlyInserted { get; } = [];

    public void Record(string calculusId, TSM.ModelObject teklaObject, bool isNew)
    {
        Objects[calculusId] = teklaObject;
        if (isNew) NewlyInserted.Add(calculusId);
    }
}

public static class CalculusModelInserter
{
    /// <param name="skipModify">Ids of objects known to be unchanged: their Tekla twin
    /// is resolved (a dependent may need it as a host) but not modified, and an
    /// unchanged bolt or weld is left alone instead of being re-inserted.</param>
    public static InsertResult UpdateTeklaModel(Model calculusModel, TSM.Model teklaModel, ISet<string>? skipModify = null)
    {
        Dictionary<string, TSM.Part> partIdMap = [];
        var result = new InsertResult();
        skipModify ??= new HashSet<string>();

        // Insert parts first
        foreach (var o in calculusModel.Objects)
        {
            if (o is Beam beam)
            {
                var teklaBeam = beam.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                    ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.Beam : null;

                if (teklaBeam is not null)
                {
                    CalculusTeklaMapper.ToTeklaBeam(beam, teklaBeam);
                    if (skipModify.Contains(beam.Id) || teklaBeam.Modify())
                    {
                        partIdMap[beam.Id] = teklaBeam;
                        result.Record(beam.Id, teklaBeam, isNew: false);
                    }
                    else
                    {
                        Log.Error("Failed to modify beam {BeamId}", beam.Id);
                    }
                }
                else
                {
                    teklaBeam = new TSM.Beam();
                    CalculusTeklaMapper.ToTeklaBeam(beam, teklaBeam);
                    if (teklaBeam.Insert())
                    {
                        partIdMap[beam.Id] = teklaBeam;
                        result.Record(beam.Id, teklaBeam, isNew: true);
                    }
                    else
                    {
                        Log.Error("Failed to insert beam {BeamId}", beam.Id);
                    }
                }
            }
            else if (o is Plate plate)
            {
                var teklaPlate = plate.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                    ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.ContourPlate : null;

                if (teklaPlate is not null)
                {
                    CalculusTeklaMapper.ToTeklaPlate(plate, teklaPlate);
                    if (skipModify.Contains(plate.Id) || teklaPlate.Modify())
                    {
                        partIdMap[plate.Id] = teklaPlate;
                        result.Record(plate.Id, teklaPlate, isNew: false);
                    }
                    else
                    {
                        Log.Error("Failed to modify plate {plateId}", plate.Id);
                    }
                }
                else
                {
                    teklaPlate = new TSM.ContourPlate();
                    CalculusTeklaMapper.ToTeklaPlate(plate, teklaPlate);
                    if (teklaPlate.Insert())
                    {
                        partIdMap[plate.Id] = teklaPlate;
                        result.Record(plate.Id, teklaPlate, isNew: true);
                    }
                    else
                    {
                        Log.Error("Failed to insert plate {plateId}", plate.Id);
                    }
                }
            }
            else if (o is Grid grid)
            {
                var teklaGrid = grid.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                    ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.Grid : null;

                if (teklaGrid is not null)
                {
                    CalculusTeklaMapper.ToTeklaGrid(grid, teklaGrid);
                    if (skipModify.Contains(grid.Id) || teklaGrid.Modify())
                    {
                        result.Record(grid.Id, teklaGrid, isNew: false);
                    }
                    else
                    {
                        Log.Error("Failed to modify grid {GridId}", grid.Id);
                    }
                }
                else
                {
                    teklaGrid = new TSM.Grid();
                    CalculusTeklaMapper.ToTeklaGrid(grid, teklaGrid);
                    if (teklaGrid.Insert())
                    {
                        result.Record(grid.Id, teklaGrid, isNew: true);
                    }
                    else
                    {
                        Log.Error("Failed to insert grid {GridId}", grid.Id);
                    }
                }
            }
            else if (o is Line line)
            {
                var teklaLine = line.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                    ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.ControlLine : null;

                if (teklaLine is not null)
                {
                    CalculusTeklaMapper.ToTeklaLine(line, teklaLine);
                    if (skipModify.Contains(line.Id) || teklaLine.Modify())
                    {
                        result.Record(line.Id, teklaLine, isNew: false);
                    }
                    else
                    {
                        Log.Error("Failed to modify line {LineId}", line.Id);
                    }
                }
                else
                {
                    teklaLine = new TSM.ControlLine();
                    CalculusTeklaMapper.ToTeklaLine(line, teklaLine);
                    if (teklaLine.Insert())
                    {
                        result.Record(line.Id, teklaLine, isNew: true);
                    }
                    else
                    {
                        Log.Error("Failed to insert line {LineId}", line.Id);
                    }
                }
            }
        }

        // Then insert objects that might depent on parts
        foreach (var o in calculusModel.Objects)
        {
            if (o is CutPart cutPart)
            {
                var teklaCutPart = cutPart.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                    ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.BooleanPart : null;

                if (teklaCutPart is not null)
                {
                    CalculusTeklaMapper.ToTeklaCutOperativePart(cutPart.Cutter, teklaCutPart.OperativePart);
                    if (skipModify.Contains(cutPart.Id) || teklaCutPart.Modify())
                    {
                        result.Record(cutPart.Id, teklaCutPart, isNew: false);
                    }
                    else
                    {
                        Log.Error("Failed to modify cut part {cutPartId}", cutPart.Id);
                    }
                }
                else
                {
                    foreach (var partId in cutPart.PartIds)
                    {
                        if (!partIdMap.TryGetValue(partId, out var part))
                        {
                            Log.Error("Father part with ID {PartId} not found for cut part {CutPartId}. Cut part skipped.", partId, cutPart.Id);
                            continue;
                        }

                        TSM.Part teklaOperativePart = cutPart.Cutter switch
                        {
                            CutBeam => new TSM.Beam(),
                            CutPlate => new TSM.ContourPlate(),
                            _ => throw new NotSupportedException($"Unsupported CutOperativePart type: {cutPart.Cutter.GetType().Name}"),
                        };
                        CalculusTeklaMapper.ToTeklaCutOperativePart(cutPart.Cutter, teklaOperativePart);
                        if (!teklaOperativePart.Insert())
                        {
                            Log.Error("Failed to insert operative part for cut part {cutPartId}", cutPart.Id);
                            continue;
                        }

                        teklaCutPart = new TSM.BooleanPart
                        {
                            Father = part,
                            OperativePart = teklaOperativePart
                        };

                        if (teklaCutPart.Insert())
                        {
                            result.Record(cutPart.Id, teklaCutPart, isNew: true);
                        }
                        else
                        {
                            Log.Error("Failed to insert cut part {cutPartId}", cutPart.Id);
                        }

                        if (!teklaOperativePart.Delete())
                        {
                            Log.Error("Failed to delete operative part for cut part {cutPartId}", cutPart.Id);
                        }
                    }
                }
            }
            else if (o is CutPlane cutPlane)
            {
                foreach (var partId in cutPlane.PartIds)
                {
                    if (!partIdMap.TryGetValue(partId, out var part))
                    {
                        Log.Error("Father part with ID {partId} not found for cut plane {cutPlaneId}. Cut plane skipped.", partId, cutPlane.Id);
                        continue;
                    }

                    if (cutPlane.Fit)
                    {
                        var teklaFitting = cutPlane.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                            ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.Fitting : null;

                        if (teklaFitting is not null)
                        {
                            CalculusTeklaMapper.ToTeklaFitting(cutPlane, teklaFitting);
                            if (skipModify.Contains(cutPlane.Id) || teklaFitting.Modify())
                            {
                                result.Record(cutPlane.Id, teklaFitting, isNew: false);
                            }
                            else
                            {
                                Log.Error("Failed to modify cut plane {cutPlaneId}", cutPlane.Id);
                            }
                        }
                        else
                        {
                            teklaFitting = new TSM.Fitting()
                            {
                                Father = part
                            };
                            CalculusTeklaMapper.ToTeklaFitting(cutPlane, teklaFitting);
                            if (teklaFitting.Insert())
                            {
                                result.Record(cutPlane.Id, teklaFitting, isNew: true);
                            }
                            else
                            {
                                Log.Error("Failed to insert cut plane {cutPlaneId}", cutPlane.Id);
                            }
                        }
                    }
                    else
                    {
                        var teklaCutPlane = cutPlane.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                            ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.CutPlane : null;

                        if (teklaCutPlane is not null)
                        {
                            CalculusTeklaMapper.ToTeklaCutPlane(cutPlane, teklaCutPlane);
                            if (skipModify.Contains(cutPlane.Id) || teklaCutPlane.Modify())
                            {
                                result.Record(cutPlane.Id, teklaCutPlane, isNew: false);
                            }
                            else
                            {
                                Log.Error("Failed to modify cut plane {cutPlaneId}", cutPlane.Id);
                            }
                        }
                        else
                        {
                            teklaCutPlane = new TSM.CutPlane()
                            {
                                Father = part
                            };
                            CalculusTeklaMapper.ToTeklaCutPlane(cutPlane, teklaCutPlane);
                            if (teklaCutPlane.Insert())
                            {
                                result.Record(cutPlane.Id, teklaCutPlane, isNew: true);
                            }
                            else
                            {
                                Log.Error("Failed to insert cut plane {cutPlaneId}", cutPlane.Id);
                            }
                        }
                    }
                }
            }
            else if (o is Bolt bolt)
            {
                if (skipModify.Contains(bolt.Id)) continue;
                var boltParts = bolt.PartIds
                    .Select(partId => partIdMap.TryGetValue(partId, out var part) ? part : null)
                    .Where(part => part is not null)
                    .ToList();

                if (boltParts.Count == 0)
                {
                    Log.Error("No valid parts found for bolt {BoltId}. Bolt skipped.", bolt.Id);
                    continue;
                }

                TSM.BoltGroup teklaBolt = bolt.GroupType switch
                {
                    BoltGroupType.Array => new TSM.BoltArray(),
                    BoltGroupType.List => new TSM.BoltXYList(),
                    BoltGroupType.Circle => new TSM.BoltCircle(),
                    _ => new TSM.BoltArray()
                };
                teklaBolt.PartToBoltTo = boltParts[0];
                teklaBolt.PartToBeBolted = boltParts.Count > 1 ? boltParts[1] : boltParts[0];
                if (boltParts.Count > 2)
                {
                    for (int i = 2; i < boltParts.Count; i++)
                    {
                        teklaBolt.AddOtherPartToBolt(boltParts[i]);
                    }
                }

                CalculusTeklaMapper.ToTeklaBolt(bolt, teklaBolt);

                if (!teklaBolt.Insert())
                {
                    Log.Error("Failed to insert bolt {BoltId}", bolt.Id);
                }
                else
                {
                    var oldTeklaBolt = bolt.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                        ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.BoltGroup : null;
                    oldTeklaBolt?.Delete();
                    // The old group (if any) is replaced by a fresh one, so this is always
                    // a new Tekla object — its GUID must be pushed back to Calculus.
                    result.Record(bolt.Id, teklaBolt, isNew: true);
                }
            }
            else if (o is Weld weld)
            {
                if (skipModify.Contains(weld.Id)) continue;
                var weldParts = weld.PartIds
                    .Select(partId => partIdMap.TryGetValue(partId, out var part) ? part : null)
                    .Where(part => part is not null)
                    .ToList();

                if (weldParts.Count == 0)
                {
                    Log.Error("No valid parts found for weld {weldId}. Weld skipped.", weld.Id);
                    continue;
                }

                TSM.BaseWeld teklaWeld = weld.Polygon.Length switch
                {
                    <2 => new TSM.Weld(),
                    _ => new TSM.PolygonWeld()
                };
                teklaWeld.MainObject = weldParts[0];
                teklaWeld.SecondaryObject = weldParts.Count > 1 ? weldParts[1] : weldParts[0];
                CalculusTeklaMapper.ToTeklaWeld(weld, teklaWeld);
                if (!teklaWeld.Insert())
                {
                    Log.Error("Failed to insert weld {weldId}", weld.Id);
                }
                else
                {
                    var oldTeklaWeld = weld.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                        ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.BaseWeld : null;
                    oldTeklaWeld?.Delete();
                    result.Record(weld.Id, teklaWeld, isNew: true);
                }
            }
            else if (o is Rebar rebar)
            {
                if (!partIdMap.TryGetValue(rebar.FatherId, out var father))
                {
                    Log.Error("Father part with ID {fatherId} not found for rebar {rebarId}. Rebar skipped.", rebar.FatherId, rebar.Id);
                    continue;
                }

                var teklaRebar = rebar.Userdata.TryGetValue("tekla_id", out var teklaIdStr)
                    ? teklaModel.SelectModelObject(new Identifier(teklaIdStr)) as TSM.Reinforcement : null;
            }
        }

        teklaModel.CommitChanges();
        return result;
    }
}
