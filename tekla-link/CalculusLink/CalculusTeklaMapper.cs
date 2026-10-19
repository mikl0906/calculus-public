using System.Diagnostics;
using System.Numerics;

using CalculusModel;

using CalculusLink.Extensions;

using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace CalculusLink;

// TODO:
// 1) Weld line type mapping is currently ignored
// 2) Check Rebar mapping

public static class CalculusTeklaMapper
{
    // Beam

    // Tekla Position             | Calculus Position
    // Rotation | Plane  | Depth  | Horizontal | Vertical
    // TOP      | LEFT   | BEHIND | Left       | Bottom
    // TOP      | LEFT   | MIDDLE | Left       | Center
    // TOP      | LEFT   | FRONT  | Left       | Top
    // TOP      | MIDDLE | BEHIND | Center     | Bottom
    // TOP      | MIDDLE | MIDDLE | Center     | Center
    // TOP      | MIDDLE | FRONT  | Center     | Top
    // TOP      | RIGHT  | BEHIND | Right      | Bottom
    // TOP      | RIGHT  | MIDDLE | Right      | Center
    // TOP      | RIGHT  | FRONT  | Right      | Top
    // BACK     | LEFT   | BEHIND | Right      | Bottom
    // BACK     | LEFT   | MIDDLE | Center     | Bottom
    // BACK     | LEFT   | FRONT  | Left       | Bottom
    // BACK     | MIDDLE | BEHIND | Right      | Center
    // BACK     | MIDDLE | MIDDLE | Center     | Center
    // BACK     | MIDDLE | FRONT  | Left       | Center
    // BACK     | RIGHT  | BEHIND | Right      | Top
    // BACK     | RIGHT  | MIDDLE | Center     | Top
    // BACK     | RIGHT  | FRONT  | Left       | Top
    // BELOW    | LEFT   | BEHIND | Right      | Top
    // BELOW    | LEFT   | MIDDLE | Right      | Center
    // BELOW    | LEFT   | FRONT  | Right      | Bottom
    // BELOW    | MIDDLE | BEHIND | Center     | Top
    // BELOW    | MIDDLE | MIDDLE | Center     | Center
    // BELOW    | MIDDLE | FRONT  | Center     | Bottom
    // BELOW    | RIGHT  | BEHIND | Left       | Top
    // BELOW    | RIGHT  | MIDDLE | Left       | Center
    // BELOW    | RIGHT  | FRONT  | Left       | Bottom
    // FRONT    | LEFT   | BEHIND | Left       | Top
    // FRONT    | LEFT   | MIDDLE | Center     | Top
    // FRONT    | LEFT   | FRONT  | Right      | Top
    // FRONT    | MIDDLE | BEHIND | Left       | Center
    // FRONT    | MIDDLE | MIDDLE | Center     | Center
    // FRONT    | MIDDLE | FRONT  | Right      | Center
    // FRONT    | RIGHT  | BEHIND | Left       | Bottom
    // FRONT    | RIGHT  | MIDDLE | Center     | Bottom
    // FRONT    | RIGHT  | FRONT  | Right      | Bottom

    static void ToTeklaBeamPosition(IBeamPosition beam, TSM.Beam teklaBeam)
    {
        var angleDeg = beam.Angle * 180 / Math.PI;
        angleDeg += 180;
        while (angleDeg < 0) angleDeg += 360;
        angleDeg %= 360;
        angleDeg -= 180;

        teklaBeam.Position.Rotation = angleDeg switch
        {
            >= -45 and < 45 => TSM.Position.RotationEnum.TOP,
            >= 45 and < 135 => TSM.Position.RotationEnum.BACK,
            >= -135 and < -45 => TSM.Position.RotationEnum.FRONT,
            _ => TSM.Position.RotationEnum.BELOW
        };
        teklaBeam.Position.RotationOffset = angleDeg switch
        {
            >= -45 and < 45 => angleDeg,
            >= 45 and < 135 => angleDeg - 90,
            >= -135 and < -45 => angleDeg + 90,
            _ => angleDeg >= 0 ? angleDeg - 180 : angleDeg + 180
        };

        if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.TOP)
        {
            teklaBeam.Position.Plane = beam.YPosition switch
            {
                BeamHorizontalPosition.Left => TSM.Position.PlaneEnum.LEFT,
                BeamHorizontalPosition.Center => TSM.Position.PlaneEnum.MIDDLE,
                BeamHorizontalPosition.Right => TSM.Position.PlaneEnum.RIGHT,
                _ => throw new Exception("Invalid beam horizontal position")
            };
            teklaBeam.Position.Depth = beam.ZPosition switch
            {
                BeamVerticalPosition.Bottom => TSM.Position.DepthEnum.BEHIND,
                BeamVerticalPosition.Center => TSM.Position.DepthEnum.MIDDLE,
                BeamVerticalPosition.Top => TSM.Position.DepthEnum.FRONT,
                _ => throw new Exception("Invalid beam horizontal position")
            };

            teklaBeam.Position.PlaneOffset = beam.YOffset;
            teklaBeam.Position.DepthOffset = beam.ZOffset;
        }
        else if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.BACK)
        {
            teklaBeam.Position.Plane = beam.ZPosition switch
            {
                BeamVerticalPosition.Bottom => TSM.Position.PlaneEnum.LEFT,
                BeamVerticalPosition.Center => TSM.Position.PlaneEnum.MIDDLE,
                BeamVerticalPosition.Top => TSM.Position.PlaneEnum.RIGHT,
                _ => throw new Exception("Invalid beam horizontal position")
            };
            teklaBeam.Position.Depth = beam.YPosition switch
            {
                BeamHorizontalPosition.Left => TSM.Position.DepthEnum.FRONT,
                BeamHorizontalPosition.Center => TSM.Position.DepthEnum.MIDDLE,
                BeamHorizontalPosition.Right => TSM.Position.DepthEnum.BEHIND,
                _ => throw new Exception("Invalid beam horizontal position")
            };

            teklaBeam.Position.PlaneOffset = (beam.ZPosition is BeamVerticalPosition.Center ? -1 : 1) * beam.ZOffset;
            teklaBeam.Position.DepthOffset = beam.YOffset;
        }
        else if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.BELOW)
        {
            teklaBeam.Position.Plane = beam.YPosition switch
            {
                BeamHorizontalPosition.Left => TSM.Position.PlaneEnum.RIGHT,
                BeamHorizontalPosition.Center => TSM.Position.PlaneEnum.MIDDLE,
                BeamHorizontalPosition.Right => TSM.Position.PlaneEnum.LEFT,
                _ => throw new Exception("Invalid beam horizontal position")
            };
            teklaBeam.Position.Depth = beam.ZPosition switch
            {
                BeamVerticalPosition.Bottom => TSM.Position.DepthEnum.FRONT,
                BeamVerticalPosition.Center => TSM.Position.DepthEnum.MIDDLE,
                BeamVerticalPosition.Top => TSM.Position.DepthEnum.BEHIND,
                _ => throw new Exception("Invalid beam horizontal position")
            };

            teklaBeam.Position.PlaneOffset = (beam.YPosition is BeamHorizontalPosition.Center ? -1 : 1) * beam.YOffset;
            teklaBeam.Position.DepthOffset = (beam.ZPosition is BeamVerticalPosition.Center ? -1 : 1) * beam.ZOffset;
        }
        else if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.FRONT)
        {
            teklaBeam.Position.Plane = beam.ZPosition switch
            {
                BeamVerticalPosition.Bottom => TSM.Position.PlaneEnum.RIGHT,
                BeamVerticalPosition.Center => TSM.Position.PlaneEnum.MIDDLE,
                BeamVerticalPosition.Top => TSM.Position.PlaneEnum.LEFT,
                _ => throw new Exception("Invalid beam horizontal position")
            };
            teklaBeam.Position.Depth = beam.YPosition switch
            {
                BeamHorizontalPosition.Left => TSM.Position.DepthEnum.BEHIND,
                BeamHorizontalPosition.Center => TSM.Position.DepthEnum.MIDDLE,
                BeamHorizontalPosition.Right => TSM.Position.DepthEnum.FRONT,
                _ => throw new Exception("Invalid beam horizontal position")
            };

            teklaBeam.Position.PlaneOffset = beam.ZOffset;
            teklaBeam.Position.DepthOffset = (beam.YPosition is BeamHorizontalPosition.Center ? -1 : 1) * beam.YOffset;
        }
    }

    static void ToCalculusBeamPosition(TSM.Beam teklaBeam, IBeamPosition beam)
    {
        var angleDeg = teklaBeam.Position.Rotation switch
        {
            TSM.Position.RotationEnum.TOP => 0,
            TSM.Position.RotationEnum.BACK => 90,
            TSM.Position.RotationEnum.BELOW => teklaBeam.Position.RotationOffset <= 0 ? 180 : -180,
            TSM.Position.RotationEnum.FRONT => -90,
            _ => throw new Exception("Invalid Tekla beam rotation enum")
        } + teklaBeam.Position.RotationOffset;
        beam.Angle = (float)(angleDeg / 180 * Math.PI);

        if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.TOP)
        {
            beam.YPosition = teklaBeam.Position.Plane switch
            {
                TSM.Position.PlaneEnum.LEFT => BeamHorizontalPosition.Left,
                TSM.Position.PlaneEnum.MIDDLE => BeamHorizontalPosition.Center,
                TSM.Position.PlaneEnum.RIGHT => BeamHorizontalPosition.Right,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };
            beam.ZPosition = teklaBeam.Position.Depth switch
            {
                TSM.Position.DepthEnum.BEHIND => BeamVerticalPosition.Bottom,
                TSM.Position.DepthEnum.MIDDLE => BeamVerticalPosition.Center,
                TSM.Position.DepthEnum.FRONT => BeamVerticalPosition.Top,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };

            beam.YOffset = (float)teklaBeam.Position.PlaneOffset;
            beam.ZOffset = (float)teklaBeam.Position.DepthOffset;
        }
        else if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.BACK)
        {
            beam.YPosition = teklaBeam.Position.Depth switch
            {
                TSM.Position.DepthEnum.BEHIND => BeamHorizontalPosition.Right,
                TSM.Position.DepthEnum.MIDDLE => BeamHorizontalPosition.Center,
                TSM.Position.DepthEnum.FRONT => BeamHorizontalPosition.Left,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };
            beam.ZPosition = teklaBeam.Position.Plane switch
            {
                TSM.Position.PlaneEnum.LEFT => BeamVerticalPosition.Bottom,
                TSM.Position.PlaneEnum.MIDDLE => BeamVerticalPosition.Center,
                TSM.Position.PlaneEnum.RIGHT => BeamVerticalPosition.Top,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };

            beam.YOffset = teklaBeam.Position.DepthOffset;
            beam.ZOffset = (teklaBeam.Position.Plane is TSM.Position.PlaneEnum.MIDDLE ? -1 : 1) * teklaBeam.Position.PlaneOffset;
        }
        else if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.BELOW)
        {
            beam.YPosition = teklaBeam.Position.Plane switch
            {
                TSM.Position.PlaneEnum.LEFT => BeamHorizontalPosition.Right,
                TSM.Position.PlaneEnum.MIDDLE => BeamHorizontalPosition.Center,
                TSM.Position.PlaneEnum.RIGHT => BeamHorizontalPosition.Left,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };
            beam.ZPosition = teklaBeam.Position.Depth switch
            {
                TSM.Position.DepthEnum.FRONT => BeamVerticalPosition.Bottom,
                TSM.Position.DepthEnum.MIDDLE => BeamVerticalPosition.Center,
                TSM.Position.DepthEnum.BEHIND => BeamVerticalPosition.Top,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };

            beam.YOffset = (teklaBeam.Position.Plane is TSM.Position.PlaneEnum.MIDDLE ? -1 : 1) * teklaBeam.Position.PlaneOffset;
            beam.ZOffset = (teklaBeam.Position.Depth is TSM.Position.DepthEnum.MIDDLE ? -1 : 1) * teklaBeam.Position.DepthOffset;
        }
        else if (teklaBeam.Position.Rotation is TSM.Position.RotationEnum.FRONT)
        {
            beam.YPosition = teklaBeam.Position.Depth switch
            {
                TSM.Position.DepthEnum.BEHIND => BeamHorizontalPosition.Left,
                TSM.Position.DepthEnum.MIDDLE => BeamHorizontalPosition.Center,
                TSM.Position.DepthEnum.FRONT => BeamHorizontalPosition.Right,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };
            beam.ZPosition = teklaBeam.Position.Plane switch
            {
                TSM.Position.PlaneEnum.LEFT => BeamVerticalPosition.Top,
                TSM.Position.PlaneEnum.MIDDLE => BeamVerticalPosition.Center,
                TSM.Position.PlaneEnum.RIGHT => BeamVerticalPosition.Bottom,
                _ => throw new Exception("Invalid Tekla beam rotation enum")
            };

            beam.YOffset = (teklaBeam.Position.Depth is TSM.Position.DepthEnum.MIDDLE ? -1 : 1) * teklaBeam.Position.DepthOffset;
            beam.ZOffset = teklaBeam.Position.PlaneOffset;
        }
    }

    // Beam end offsets. Tekla's Dx runs along the part (start -> end), Dy and Dz
    // across it in a frame that IGNORES the profile rotation: y = Z × x (for a
    // vertical part +Y), z = x × y — measured on Tekla 2025, Oct 19 2026. Calculus
    // builds the same unrotated frame (core objects/lcs.ts beamRefDirection) but
    // turns y and z by the beam's Angle before applying the offset, so the two
    // cross components rotate by the angle on the way through.

    /// <summary>Tekla stores offsets to ~0.001 mm of noise (100 reads back as 99.9992).</summary>
    const int OffsetDecimals = 2;

    static TSM.Offset ToTeklaOffset(Vector3 offset, double angle)
    {
        var (sin, cos) = (Math.Sin(angle), Math.Cos(angle));
        return new TSM.Offset
        {
            Dx = offset.X,
            Dy = offset.Y * cos - offset.Z * sin,
            Dz = offset.Y * sin + offset.Z * cos,
        };
    }

    static Vector3 ToCalculusOffset(TSM.Offset offset, double angle)
    {
        var (sin, cos) = (Math.Sin(angle), Math.Cos(angle));
        return new Vector3(
            (float)Math.Round(offset.Dx, OffsetDecimals),
            (float)Math.Round(offset.Dy * cos + offset.Dz * sin, OffsetDecimals),
            (float)Math.Round(-offset.Dy * sin + offset.Dz * cos, OffsetDecimals));
    }

    public static void ToTeklaBeam(Beam beam, TSM.Beam teklaBeam)
    {
        // TODO: Smarter profile mapping
        var teklaProfile = beam.Profile;

        teklaBeam.StartPoint = new TSG.Point(beam.Start.X, beam.Start.Y, beam.Start.Z);
        teklaBeam.EndPoint = new TSG.Point(beam.End.X, beam.End.Y, beam.End.Z);
        teklaBeam.Name = beam.Name;
        teklaBeam.Profile.ProfileString = teklaProfile;
        teklaBeam.Material.MaterialString = beam.Material;
        teklaBeam.Class = beam.Class.ToString();
        teklaBeam.PartNumber = new TSM.NumberingSeries
        {
            Prefix = beam.PartNumber.Prefix,
            StartNumber = beam.PartNumber.StartNumber,
        };
        teklaBeam.AssemblyNumber = new TSM.NumberingSeries
        {
            Prefix = beam.AssemblyNumber.Prefix,
            StartNumber = beam.AssemblyNumber.StartNumber,
        };
        teklaBeam.StartPointOffset = ToTeklaOffset(beam.StartOffset, beam.Angle);
        teklaBeam.EndPointOffset = ToTeklaOffset(beam.EndOffset, beam.Angle);

        ToTeklaBeamPosition(beam, teklaBeam);
    }

    public static void ToCalculusBeam(TSM.Beam teklaBeam, Beam beam)
    {
        beam.Start = new Vector3(
            (float)teklaBeam.StartPoint.X,
            (float)teklaBeam.StartPoint.Y,
            (float)teklaBeam.StartPoint.Z);
        beam.End = new Vector3(
            (float)teklaBeam.EndPoint.X,
            (float)teklaBeam.EndPoint.Y,
            (float)teklaBeam.EndPoint.Z);
        beam.Name = teklaBeam.Name;
        beam.Profile = teklaBeam.Profile.ProfileString;
        beam.Material = teklaBeam.Material.MaterialString;
        beam.Class = int.TryParse(teklaBeam.Class, out var cls) ? cls : 0;
        beam.PartNumber = new NumberingSeries
        {
            Prefix = teklaBeam.PartNumber.Prefix,
            StartNumber = teklaBeam.PartNumber.StartNumber
        };
        beam.AssemblyNumber = new NumberingSeries
        {
            Prefix = teklaBeam.AssemblyNumber.Prefix,
            StartNumber = teklaBeam.AssemblyNumber.StartNumber
        };

        // The position first: the offsets are expressed through its angle.
        ToCalculusBeamPosition(teklaBeam, beam);
        beam.StartOffset = ToCalculusOffset(teklaBeam.StartPointOffset, beam.Angle);
        beam.EndOffset = ToCalculusOffset(teklaBeam.EndPointOffset, beam.Angle);

        beam.Userdata.Add("tekla_id", teklaBeam.Identifier.GUID.ToString());
    }

    // Plate
    static TSM.Chamfer ToTeklaChamfer(Chamfer? chamfer)
    {
        return chamfer switch
        {
            null => new TSM.Chamfer(),
            LineChamfer c => new TSM.Chamfer(c.X, c.Y, TSM.Chamfer.ChamferTypeEnum.CHAMFER_LINE),
            RoundChamfer c => new TSM.Chamfer(c.R, 0, TSM.Chamfer.ChamferTypeEnum.CHAMFER_ROUNDING),
            ArcChamfer c => new TSM.Chamfer(c.R, 0, TSM.Chamfer.ChamferTypeEnum.CHAMFER_ARC),
            ArcPointChamfer => new TSM.Chamfer(0, 0, TSM.Chamfer.ChamferTypeEnum.CHAMFER_ARC_POINT),
            SquareChamfer c => new TSM.Chamfer(c.X, c.Y, TSM.Chamfer.ChamferTypeEnum.CHAMFER_SQUARE),
            SquareParallelChamfer c => new TSM.Chamfer(c.X, c.Y, TSM.Chamfer.ChamferTypeEnum.CHAMFER_SQUARE_PARALLEL),
            LineAndArcChamfer c => new TSM.Chamfer(c.X, c.Y, TSM.Chamfer.ChamferTypeEnum.CHAMFER_LINE_AND_ARC),
            _ => new TSM.Chamfer()
        };
    }

    static Chamfer? ToCalculusChamfer(TSM.Chamfer chamfer)
    {
        return chamfer.Type switch
        {
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_NONE => null,
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_LINE => new LineChamfer(chamfer.X, chamfer.Y),
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_ROUNDING => new RoundChamfer(chamfer.X),
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_ARC => new ArcChamfer(chamfer.X),
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_ARC_POINT => new ArcPointChamfer(),
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_SQUARE => new SquareChamfer(chamfer.X, chamfer.Y),
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_SQUARE_PARALLEL => new SquareParallelChamfer(chamfer.X, chamfer.Y),
            TSM.Chamfer.ChamferTypeEnum.CHAMFER_LINE_AND_ARC => new LineAndArcChamfer(chamfer.X, chamfer.Y),
            _ => null
        };
    }

    static Vector3 GetPlateNormal(Vec3WithChamfer[] polygon)
    {
        var uniquePolygon = polygon
            .Select(v => new Vector3((float)v.X, (float)v.Y, (float)v.Z))
            .Distinct()
            .ToArray();

        if (uniquePolygon.Length < 3) return Vector3.UnitZ;

        var first = uniquePolygon[0];
        var second = uniquePolygon[1];
        var x = Vector3.Normalize(Vector3.Subtract(second, first));
        foreach (var point in uniquePolygon.Skip(2))
        {
            var y = Vector3.Normalize(Vector3.Subtract(point, first));
            if (Math.Abs(Vector3.Dot(y, x)) < 0.999)
            {
                var normal = Vector3.Cross(x, y);
                return Vector3.Normalize(normal);
            }
        }

        return Vector3.UnitZ;
    }

    public static void ToTeklaPlate(Plate plate, TSM.ContourPlate teklaPlate)
    {
        teklaPlate.Contour = new TSM.Contour()
        {
            ContourPoints = [.. plate.Polygon
                .Select(p => new TSM.ContourPoint(new TSG.Point(p.X, p.Y, p.Z), ToTeklaChamfer(p.Chamfer)))]
        };
        teklaPlate.Name = plate.Name;
        teklaPlate.Profile.ProfileString = plate.Profile;
        teklaPlate.Material.MaterialString = plate.Material;
        teklaPlate.Class = plate.Class.ToString();
        teklaPlate.PartNumber = new TSM.NumberingSeries()
        {
            Prefix = plate.PartNumber.Prefix,
            StartNumber = plate.PartNumber.StartNumber,
        };
        teklaPlate.AssemblyNumber = new TSM.NumberingSeries()
        {
            Prefix = plate.AssemblyNumber.Prefix,
            StartNumber = plate.AssemblyNumber.StartNumber,
        };

        var normal = GetPlateNormal(plate.Polygon);
        var flipped = Vector3.Dot(normal, Vector3.UnitZ) < 0;

        if (!flipped)
        {
            teklaPlate.Position = new TSM.Position
            {
                Depth = plate.ZPosition switch
                {
                    PlatePosition.Top => TSM.Position.DepthEnum.FRONT,
                    PlatePosition.Center => TSM.Position.DepthEnum.MIDDLE,
                    PlatePosition.Bottom => TSM.Position.DepthEnum.BEHIND,
                    _ => TSM.Position.DepthEnum.MIDDLE
                },
                DepthOffset = plate.ZOffset
            };
        }
        else
        {
            teklaPlate.Position = new TSM.Position
            {
                Depth = plate.ZPosition switch
                {
                    PlatePosition.Top => TSM.Position.DepthEnum.BEHIND,
                    PlatePosition.Center => TSM.Position.DepthEnum.MIDDLE,
                    PlatePosition.Bottom => TSM.Position.DepthEnum.FRONT,
                    _ => TSM.Position.DepthEnum.MIDDLE
                },
                DepthOffset = (plate.ZPosition is PlatePosition.Center ? -1 : 1) * plate.ZOffset
            };
        }
    }

    public static void ToCalculusPlate(TSM.ContourPlate teklaPlate, Plate plate)
    {
        var polygon = teklaPlate.Contour.ContourPoints
            .OfType<TSM.ContourPoint>()
            .Select(p => new Vec3WithChamfer()
            {
                X = p.X,
                Y = p.Y,
                Z = p.Z,
                Chamfer = ToCalculusChamfer(p.Chamfer)
            })
            .ToArray();

        plate.Polygon = polygon;
        plate.Name = teklaPlate.Name;
        plate.Profile = teklaPlate.Profile.ProfileString;
        plate.Material = teklaPlate.Material.MaterialString;
        plate.Class = int.TryParse(teklaPlate.Class, out var cls) ? cls : 0;
        plate.PartNumber = new NumberingSeries
        {
            Prefix = teklaPlate.PartNumber.Prefix,
            StartNumber = teklaPlate.PartNumber.StartNumber
        };
        plate.AssemblyNumber = new NumberingSeries
        {
            Prefix = teklaPlate.AssemblyNumber.Prefix,
            StartNumber = teklaPlate.AssemblyNumber.StartNumber
        };
        plate.ZPosition = teklaPlate.Position.Depth switch
        {
            TSM.Position.DepthEnum.FRONT => PlatePosition.Top,
            TSM.Position.DepthEnum.MIDDLE => PlatePosition.Center,
            TSM.Position.DepthEnum.BEHIND => PlatePosition.Bottom,
            _ => PlatePosition.Center
        };
        plate.ZOffset = teklaPlate.Position.DepthOffset;
        plate.Userdata.Add("tekla_id", teklaPlate.Identifier.GUID.ToString());
    }

    // CutPart
    static void ToTeklaCutBeam(CutBeam beam, TSM.Beam teklaBeam)
    {
        // TODO: Smarter profile mapping
        var teklaProfile = beam.Profile;

        teklaBeam.StartPoint = new TSG.Point(beam.Start.X, beam.Start.Y, beam.Start.Z);
        teklaBeam.EndPoint = new TSG.Point(beam.End.X, beam.End.Y, beam.End.Z);
        teklaBeam.Name = beam.Name;
        teklaBeam.Profile.ProfileString = teklaProfile;
        teklaBeam.Material.MaterialString = beam.Material;
        teklaBeam.Class = TSM.BooleanPart.BooleanOperativeClassName;
        teklaBeam.StartPointOffset = ToTeklaOffset(beam.StartOffset, beam.Angle);
        teklaBeam.EndPointOffset = ToTeklaOffset(beam.EndOffset, beam.Angle);

        ToTeklaBeamPosition(beam, teklaBeam);
    }

    static void ToTeklaCutPlate(CutPlate plate, TSM.ContourPlate teklaPlate)
    {
        teklaPlate.Contour = new TSM.Contour()
        {
            ContourPoints = [.. plate.Polygon
                .Select(p => new TSM.ContourPoint(new TSG.Point(p.X, p.Y, p.Z), ToTeklaChamfer(p.Chamfer)))]
        };
        teklaPlate.Name = plate.Name;
        teklaPlate.Profile.ProfileString = plate.Profile;
        teklaPlate.Material.MaterialString = plate.Material;
        teklaPlate.Class = TSM.BooleanPart.BooleanOperativeClassName;
        teklaPlate.Position = new TSM.Position
        {
            Depth = plate.ZPosition switch
            {
                PlatePosition.Top => TSM.Position.DepthEnum.FRONT,
                PlatePosition.Center => TSM.Position.DepthEnum.MIDDLE,
                PlatePosition.Bottom => TSM.Position.DepthEnum.BEHIND,
                _ => TSM.Position.DepthEnum.MIDDLE
            },
            DepthOffset = plate.ZOffset
        };
    }

    public static void ToTeklaCutOperativePart(Cutter operativePart, TSM.Part teklaOperativePart)
    {
        if (operativePart is CutBeam cutBeam && teklaOperativePart is TSM.Beam teklaCutBeam)
        {
            ToTeklaCutBeam(cutBeam, teklaCutBeam);
        }
        else if (operativePart is CutPlate cutPlate && teklaOperativePart is TSM.ContourPlate teklaCutPlate)
        {
            ToTeklaCutPlate(cutPlate, teklaCutPlate);
        }
        else
        {
            Debug.WriteLine("Incomatible types of operative parts", operativePart.GetType().Name, teklaOperativePart.GetType().Name);
        }
    }

    static CutBeam ToCalculusCutBeam(TSM.Beam teklaBeam)
    {
        var beam = new CutBeam
        {
            Start = new Vector3(
                (float)teklaBeam.StartPoint.X,
                (float)teklaBeam.StartPoint.Y,
                (float)teklaBeam.StartPoint.Z),
            End = new Vector3(
                (float)teklaBeam.EndPoint.X,
                (float)teklaBeam.EndPoint.Y,
                (float)teklaBeam.EndPoint.Z),
            Name = teklaBeam.Name,
            Profile = teklaBeam.Profile.ProfileString,
            Material = teklaBeam.Material.MaterialString,
            Class = int.TryParse(teklaBeam.Class, out var cls) ? cls : 0,
        };

        ToCalculusBeamPosition(teklaBeam, beam);
        beam.StartOffset = ToCalculusOffset(teklaBeam.StartPointOffset, beam.Angle);
        beam.EndOffset = ToCalculusOffset(teklaBeam.EndPointOffset, beam.Angle);

        return beam;
    }

    static CutPlate ToCalculusCutPlate(TSM.ContourPlate teklaPlate)
    {
        var plate = new CutPlate
        {
            Polygon = [.. teklaPlate.Contour.ContourPoints
                .OfType<TSM.ContourPoint>()
                .Select(p => new Vec3WithChamfer()
                {
                    X = (float)p.X,
                    Y = (float)p.Y,
                    Z = (float)p.Z,
                    Chamfer = ToCalculusChamfer(p.Chamfer)
                })],
            Name = teklaPlate.Name,
            Profile = teklaPlate.Profile.ProfileString,
            Material = teklaPlate.Material.MaterialString,
            Class = int.TryParse(teklaPlate.Class, out var cls) ? cls : 0,
            ZPosition = teklaPlate.Position.Depth switch
            {
                TSM.Position.DepthEnum.FRONT => PlatePosition.Top,
                TSM.Position.DepthEnum.MIDDLE => PlatePosition.Center,
                TSM.Position.DepthEnum.BEHIND => PlatePosition.Bottom,
                _ => PlatePosition.Center
            },
            ZOffset = (float)teklaPlate.Position.DepthOffset,
        };

        return plate;
    }

    public static void ToCaclulusCutPart(TSM.BooleanPart teklaCutPart, CutPart cutPart, string[] partIds)
    {
        cutPart.PartIds = partIds;
        cutPart.Cutter = teklaCutPart.OperativePart switch
        {
            TSM.Beam teklaBeam => ToCalculusCutBeam(teklaBeam),
            TSM.ContourPlate teklaPlate => ToCalculusCutPlate(teklaPlate),
            _ => throw new Exception($"Unsupported boolean operative part type: {teklaCutPart.OperativePart.GetType().Name}")
        };
        cutPart.Userdata.Add("tekla_id", teklaCutPart.Identifier.GUID.ToString());
    }

    // CutPlane
    public static void ToTeklaCutPlane(CutPlane cutPlane, TSM.CutPlane teklaCutPlane)
    {
        teklaCutPlane.Plane = new TSM.Plane
        {
            Origin = new TSG.Point(cutPlane.P1.X, cutPlane.P1.Y, cutPlane.P1.Z),
            AxisX = new TSG.Vector(
                cutPlane.P2.X - cutPlane.P1.X,
                cutPlane.P2.Y - cutPlane.P1.Y,
                cutPlane.P2.Z - cutPlane.P1.Z),
            AxisY = new TSG.Vector(
                cutPlane.P3.X - cutPlane.P1.X,
                cutPlane.P3.Y - cutPlane.P1.Y,
                cutPlane.P3.Z - cutPlane.P1.Z),
        };
    }

    public static void ToCalculusCutPlane(TSM.CutPlane teklaCutPlane, CutPlane cutPlane, string[] partIds)
    {
        cutPlane.PartIds = partIds;
        cutPlane.P1 = new Vector3(
            (float)teklaCutPlane.Plane.Origin.X,
            (float)teklaCutPlane.Plane.Origin.Y,
            (float)teklaCutPlane.Plane.Origin.Z);
        cutPlane.P2 = new Vector3(
            (float)(teklaCutPlane.Plane.Origin.X + teklaCutPlane.Plane.AxisX.X),
            (float)(teklaCutPlane.Plane.Origin.Y + teklaCutPlane.Plane.AxisX.Y),
            (float)(teklaCutPlane.Plane.Origin.Z + teklaCutPlane.Plane.AxisX.Z));
        cutPlane.P3 = new Vector3(
            (float)(teklaCutPlane.Plane.Origin.X + teklaCutPlane.Plane.AxisY.X),
            (float)(teklaCutPlane.Plane.Origin.Y + teklaCutPlane.Plane.AxisY.Y),
            (float)(teklaCutPlane.Plane.Origin.Z + teklaCutPlane.Plane.AxisY.Z));
        cutPlane.Fit = false;
        cutPlane.Userdata.Add("tekla_id", teklaCutPlane.Identifier.GUID.ToString());
    }

    // Fitting
    public static void ToTeklaFitting(CutPlane cutPlane, TSM.Fitting teklaCutPlane)
    {
        teklaCutPlane.Plane = new TSM.Plane
        {
            Origin = new TSG.Point(cutPlane.P1.X, cutPlane.P1.Y, cutPlane.P1.Z),
            AxisX = new TSG.Vector(
                cutPlane.P2.X - cutPlane.P1.X,
                cutPlane.P2.Y - cutPlane.P1.Y,
                cutPlane.P2.Z - cutPlane.P1.Z),
            AxisY = new TSG.Vector(
                cutPlane.P3.X - cutPlane.P1.X,
                cutPlane.P3.Y - cutPlane.P1.Y,
                cutPlane.P3.Z - cutPlane.P1.Z),
        };
    }

    public static void ToCalculusFitting(TSM.Fitting teklaFitting, CutPlane cutPlane, string[] partIds)
    {
        cutPlane.PartIds = partIds;
        cutPlane.P1 = new Vector3(
            (float)teklaFitting.Plane.Origin.X,
            (float)teklaFitting.Plane.Origin.Y,
            (float)teklaFitting.Plane.Origin.Z);
        cutPlane.P2 = new Vector3(
            (float)(teklaFitting.Plane.Origin.X + teklaFitting.Plane.AxisX.X),
            (float)(teklaFitting.Plane.Origin.Y + teklaFitting.Plane.AxisX.Y),
            (float)(teklaFitting.Plane.Origin.Z + teklaFitting.Plane.AxisX.Z));
        cutPlane.P3 = new Vector3(
            (float)(teklaFitting.Plane.Origin.X + teklaFitting.Plane.AxisY.X),
            (float)(teklaFitting.Plane.Origin.Y + teklaFitting.Plane.AxisY.Y),
            (float)(teklaFitting.Plane.Origin.Z + teklaFitting.Plane.AxisY.Z));
        cutPlane.Fit = true;
        cutPlane.Userdata.Add("tekla_id", teklaFitting.Identifier.GUID.ToString());
    }

    // Bolt
    public static void ToTeklaBolt(Bolt bolt, TSM.BoltGroup teklaBolt)
    {
        // Definition
        teklaBolt.FirstPosition = new TSG.Point(bolt.P1.X, bolt.P1.Y, bolt.P1.Z);
        teklaBolt.SecondPosition = new TSG.Point(bolt.P2.X, bolt.P2.Y, bolt.P2.Z);

        // Position
        teklaBolt.Position.Plane = TSM.Position.PlaneEnum.MIDDLE;
        teklaBolt.Position.PlaneOffset = 0;
        teklaBolt.Position.Depth = TSM.Position.DepthEnum.MIDDLE;
        teklaBolt.Position.DepthOffset = 0;

        var x = bolt.P2 - bolt.P1;
        var projectionSq = x.X * x.X + x.Y * x.Y;
        x = Vector3.Normalize(x);
        var z = Vector3.Normalize(Vector3.Cross(x, Vector3.Subtract(bolt.P3, bolt.P1)));

        var teklaUp = projectionSq > 1 ? new Vector3(0, 0, 1) : x.Z > 0 ? new Vector3(-1, 0, 0) : new Vector3(1, 0, 0);
        var teklaY = Vector3.Normalize(Vector3.Cross(teklaUp, x));
        var teklaZ = Vector3.Normalize(Vector3.Cross(x, teklaY));
        var angleRad = Math.Atan2(Vector3.Dot(z, teklaY), Vector3.Dot(z, teklaZ));
        var angleDeg = -angleRad * 180 / Math.PI;
        teklaBolt.Position.Rotation = angleDeg switch
        {
            >= -45 and < 45 => TSM.Position.RotationEnum.FRONT,
            >= 45 and < 135 => TSM.Position.RotationEnum.TOP,
            >= -135 and < -45 => TSM.Position.RotationEnum.BELOW,
            _ => TSM.Position.RotationEnum.BACK
        };
        teklaBolt.Position.RotationOffset = angleDeg switch
        {
            >= -45 and < 45 => angleDeg,
            >= 45 and < 135 => angleDeg - 90,
            >= -135 and < -45 => angleDeg + 90,
            _ => angleDeg >= 0 ? angleDeg - 180 : angleDeg + 180
        };

        // Basic properties
        teklaBolt.BoltSize = bolt.Size;
        teklaBolt.BoltStandard = bolt.Standard;
        teklaBolt.BoltType = bolt.SiteShop switch
        {
            BoltSiteShop.Site => TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_SITE,
            BoltSiteShop.Shop => TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_WORKSHOP,
            _ => TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_SITE
        };
        teklaBolt.ConnectAssemblies = bolt.ConnectAs switch
        {
            BoltConnectAs.Subassembly => true,
            _ => false
        };
        teklaBolt.ThreadInMaterial = bolt.ThreadInMaterial switch
        {
            false => TSM.BoltGroup.BoltThreadInMaterialEnum.THREAD_IN_MATERIAL_NO,
            true => TSM.BoltGroup.BoltThreadInMaterialEnum.THREAD_IN_MATERIAL_YES,
        };
        teklaBolt.CutLength = bolt.CutLength;
        teklaBolt.ExtraLength = bolt.ExtraLength;

        // Bolt assembly
        teklaBolt.Bolt = bolt.BoltCheck;
        teklaBolt.Washer1 = bolt.Washer1;
        teklaBolt.Washer2 = bolt.Washer2;
        teklaBolt.Washer3 = bolt.Washer3;
        teklaBolt.Nut1 = bolt.Nut1;
        teklaBolt.Nut2 = bolt.Nut2;

        teklaBolt.Hole1 = true;
        teklaBolt.Hole2 = true;
        teklaBolt.Hole3 = true;
        teklaBolt.Hole4 = true;
        teklaBolt.Hole5 = true;

        // Bolt group
        if (bolt.GroupType == BoltGroupType.Array && teklaBolt is TSM.BoltArray teklaBoltArray)
        {
            if (bolt.DistX.Length > 0)
            {
                var firstX = bolt.DistX[0];
                teklaBoltArray.StartPointOffset.Dx += firstX;
                teklaBoltArray.EndPointOffset.Dx += firstX;
                if (bolt.DistX.Length > 1)
                {
                    for (int i = 1; i < bolt.DistX.Length; i++)
                    {
                        teklaBoltArray.AddBoltDistX(bolt.DistX[i]);
                    }
                }
                else
                {
                    teklaBoltArray.AddBoltDistX(bolt.DistX[0]);
                }
            }
            else
            {
                teklaBoltArray.AddBoltDistX(0);
            }

            if (bolt.DistY.Length > 0)
            {
                List<double> positionsY = [bolt.DistY[0]];
                foreach (var distY in bolt.DistY.Skip(1))
                {
                    positionsY.Add(positionsY.Last() + distY);
                }
                var average = positionsY.Average();
                teklaBoltArray.StartPointOffset.Dy -= average;
                teklaBoltArray.EndPointOffset.Dy -= average;

                if (bolt.DistY.Length > 1)
                {
                    for (int i = 1; i < bolt.DistY.Length; i++)
                    {
                        teklaBoltArray.AddBoltDistY(bolt.DistY[i]);
                    }
                }
                else
                {
                    teklaBoltArray.AddBoltDistY(bolt.DistY[0]);
                }
            }
            else
            {
                teklaBoltArray.AddBoltDistY(0);
            }
        }
        else if (bolt.GroupType == BoltGroupType.List && teklaBolt is TSM.BoltXYList teklaBoltList)
        {
            var numOfBolts = Math.Min(bolt.DistX.Length, bolt.DistY.Length);
            for (int i = 0; i < numOfBolts; i++)
            {
                teklaBoltList.AddBoltDistX(bolt.DistX[i]);
                teklaBoltList.AddBoltDistY(bolt.DistY[i]);
            }
        }
        else if (bolt.GroupType == BoltGroupType.Circle && teklaBolt is TSM.BoltCircle teklaBoltCircle)
        {
            teklaBoltCircle.NumberOfBolts = bolt.CircleNumber;
            teklaBoltCircle.Diameter = bolt.CircleRadius.FirstOrDefault() * 2;
        }

        // Offset
        teklaBolt.StartPointOffset.Dx += bolt.Offset.X;
        teklaBolt.StartPointOffset.Dy += bolt.Offset.Y;
        teklaBolt.StartPointOffset.Dz += bolt.Offset.Z;
        teklaBolt.EndPointOffset.Dx += bolt.Offset.X;
        teklaBolt.EndPointOffset.Dy += bolt.Offset.Y;
        teklaBolt.EndPointOffset.Dz += bolt.Offset.Z;

        // Hole
        teklaBolt.Tolerance = bolt.Tolerance;
    }

    public static void ToCalculusBolt(TSM.BoltGroup teklaBolt, Bolt bolt, string[] partIds)
    {
        // Definition
        bolt.PartIds = partIds;
        var teklaBoltCs = teklaBolt.GetCoordinateSystem();
        var boltOrigin = teklaBoltCs.Origin;
        var boltX = teklaBoltCs.AxisX.GetNormal();
        var boltY = teklaBoltCs.AxisY.GetNormal();
        bolt.P1 = new Vector3(
            (float)boltOrigin.X,
            (float)boltOrigin.Y,
            (float)boltOrigin.Z);
        bolt.P2 = bolt.P1 + new Vector3((float)boltX.X * 100, (float)boltX.Y * 100, (float)boltX.Z * 100);
        bolt.P3 = bolt.P1 + new Vector3((float)boltY.X * 100, (float)boltY.Y * 100, (float)boltY.Z * 100);

        // Basic properties
        bolt.Size = teklaBolt.BoltSize;
        bolt.Standard = teklaBolt.BoltStandard;
        bolt.SiteShop = teklaBolt.BoltType switch
        {
            TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_SITE => BoltSiteShop.Site,
            TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_WORKSHOP => BoltSiteShop.Shop,
            _ => BoltSiteShop.Site
        };
        bolt.ConnectAs = teklaBolt.ConnectAssemblies switch
        {
            true => BoltConnectAs.Subassembly,
            false => BoltConnectAs.Secondary,
        };
        bolt.ThreadInMaterial = teklaBolt.ThreadInMaterial switch
        {
            TSM.BoltGroup.BoltThreadInMaterialEnum.THREAD_IN_MATERIAL_YES => true,
            TSM.BoltGroup.BoltThreadInMaterialEnum.THREAD_IN_MATERIAL_NO => false,
            _ => true
        };
        bolt.CutLength = teklaBolt.CutLength;
        bolt.ExtraLength = teklaBolt.ExtraLength;

        // Bolt assembly
        bolt.BoltCheck = teklaBolt.Bolt;
        bolt.Washer1 = teklaBolt.Washer1;
        bolt.Washer2 = teklaBolt.Washer2;
        bolt.Washer3 = teklaBolt.Washer3;
        bolt.Nut1 = teklaBolt.Nut1;
        bolt.Nut2 = teklaBolt.Nut2;

        // Bolt group
        if (teklaBolt is TSM.BoltArray teklaBoltArray)
        {
            bolt.GroupType = BoltGroupType.Array;

            var matrixToLocal = TSG.MatrixFactory.ToCoordinateSystem(teklaBoltCs);
            var boltPositions = teklaBoltArray.BoltPositions
                .OfType<TSG.Point>()
                .Select(p => matrixToLocal.Transform(p))
                .ToArray();

            // DistX
            List<double> positionsX = [.. boltPositions
                .Select(p => Math.Round(p.X))
                .Distinct()
                .OrderBy(x => x)];
            List<double> spacingsX = [];
            if (positionsX.Count > 0)
            {
                spacingsX.Add(positionsX.First());
            }
            if (positionsX.Count > 1)
            {
                for (int i = 1; i < positionsX.Count; i++)
                {
                    spacingsX.Add(positionsX[i] - positionsX[i - 1]);
                }
            }
            bolt.DistX = [.. spacingsX];

            // DistY
            List<double> positionsY = [.. boltPositions
                .Select(p => Math.Round(p.Y))
                .Distinct()
                .OrderBy(y => y)];
            List<double> spacingsY = [];
            if (positionsY.Count > 0)
            {
                spacingsY.Add(positionsY.First());
            }
            if (positionsY.Count > 1)
            {
                for (int i = 1; i < positionsY.Count; i++)
                {
                    spacingsY.Add(positionsY[i] - positionsY[i - 1]);
                }
            }
            bolt.DistY = [.. spacingsY];
        }
        else if (teklaBolt is TSM.BoltXYList teklaBoltXYList)
        {
            bolt.GroupType = BoltGroupType.List;

            var matrixToLocal = TSG.MatrixFactory.ToCoordinateSystem(teklaBoltCs);
            var boltPositions = teklaBoltXYList.BoltPositions
                .OfType<TSG.Point>()
                .Select(p => matrixToLocal.Transform(p))
                .ToArray();

            bolt.DistX = [.. boltPositions.Select(p => p.X)];
            bolt.DistY = [.. boltPositions.Select(p => p.Y)];
        }
        else if (teklaBolt is TSM.BoltCircle teklaBoltCircle)
        {
            bolt.GroupType = BoltGroupType.Circle;
            bolt.CircleRadius = [teklaBoltCircle.Diameter / 2];
            bolt.CircleNumber = (int)teklaBoltCircle.NumberOfBolts;
        }

        // Hole
        bolt.Tolerance = teklaBolt.Tolerance;

        bolt.Userdata.Add("tekla_id", teklaBolt.Identifier.GUID.ToString());
    }

    // Weld
    public static void ToTeklaWeld(Weld weld, TSM.BaseWeld teklaWeld)
    {
        if (weld.Polygon.Length > 1 && teklaWeld is TSM.PolygonWeld polygonWeld)
        {
            foreach (var p in weld.Polygon)
            {
                polygonWeld.Polygon.Points.Add(new TSG.Point(p.X, p.Y, p.Z));
            }
        }

        // Basic properties
        teklaWeld.AroundWeld = weld.EdgeAround switch
        {
            WeldEdgeAround.Edge => false,
            WeldEdgeAround.Around => true,
            _ => false
        };
        teklaWeld.ShopWeld = weld.SiteShop switch
        {
            WeldSiteShop.Shop => true,
            WeldSiteShop.Site => false,
            _ => true
        };
        if (teklaWeld is TSM.Weld w)
        {
            w.Position = weld.Position switch
            {
                WeldPosition.XPlus => TSM.Weld.WeldPositionEnum.WELD_POSITION_PLUS_X,
                WeldPosition.XMinus => TSM.Weld.WeldPositionEnum.WELD_POSITION_MINUS_X,
                WeldPosition.YPlus => TSM.Weld.WeldPositionEnum.WELD_POSITION_PLUS_Y,
                WeldPosition.YMinus => TSM.Weld.WeldPositionEnum.WELD_POSITION_MINUS_Y,
                WeldPosition.ZPlus => TSM.Weld.WeldPositionEnum.WELD_POSITION_PLUS_Z,
                WeldPosition.ZMinus => TSM.Weld.WeldPositionEnum.WELD_POSITION_MINUS_Z,
                _ => TSM.Weld.WeldPositionEnum.WELD_POSITION_PLUS_X
            };
        }
        teklaWeld.IntermittentType = weld.Shape switch
        {
            WeldShape.Continuous => TSM.BaseWeld.WeldIntermittentTypeEnum.CONTINUOUS,
            WeldShape.Chain => TSM.BaseWeld.WeldIntermittentTypeEnum.CHAIN_INTERMITTENT,
            WeldShape.Staggered => TSM.BaseWeld.WeldIntermittentTypeEnum.STAGGERED_INTERMITTENT,
            _ => TSM.BaseWeld.WeldIntermittentTypeEnum.CONTINUOUS
        };
        teklaWeld.ConnectAssemblies = weld.ConnectAs switch
        {
            WeldConnectAs.Subassembly => true,
            _ => false
        };

        // Above line
        teklaWeld.PrefixAboveLine = weld.AboveLine.Prefix;
        teklaWeld.TypeAbove = weld.AboveLine.Type switch
        {
            WeldType.None => TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_NONE,
            WeldType.Fillet => TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET,
            _ => TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET
        };
        teklaWeld.SizeAbove = weld.AboveLine.Size;
        teklaWeld.AngleAbove = weld.AboveLine.Angle;
        teklaWeld.ContourAbove = weld.AboveLine.Contour switch
        {
            WeldContour.None => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_NONE,
            WeldContour.Flush => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_FLUSH,
            WeldContour.Convex => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONVEX,
            WeldContour.Concave => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONCAVE,
            _ => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_NONE
        };
        teklaWeld.FinishAbove = weld.AboveLine.Finish switch
        {
            WeldFinish.None => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_NONE,
            WeldFinish.Grind => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_GRIND,
            WeldFinish.Machine => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_MACHINE,
            WeldFinish.Chip => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_CHIP,
            WeldFinish.Flush => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_FINISHED_WELD,
            WeldFinish.Smooth => TSM.BaseWeld.WeldFinishEnum.WELS_FINISH_SMOOTH_TRANSITION,
            _ => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_NONE
        };
        teklaWeld.RootFaceAbove = weld.AboveLine.RootFace;
        teklaWeld.RootOpeningAbove = weld.AboveLine.Gap;
        teklaWeld.IncrementAmountAbove = weld.AboveLine.NInrc;
        teklaWeld.LengthAbove = weld.AboveLine.Length;
        teklaWeld.PitchAbove = weld.AboveLine.Pitch;

        // Below line
        teklaWeld.PrefixBelowLine = weld.BelowLine.Prefix;
        teklaWeld.TypeBelow = weld.BelowLine.Type switch
        {
            WeldType.None => TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_NONE,
            WeldType.Fillet => TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET,
            _ => TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET
        };
        teklaWeld.SizeBelow = weld.BelowLine.Size;
        teklaWeld.AngleBelow = weld.BelowLine.Angle;
        teklaWeld.ContourBelow = weld.BelowLine.Contour switch
        {
            WeldContour.None => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_NONE,
            WeldContour.Flush => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_FLUSH,
            WeldContour.Convex => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONVEX,
            WeldContour.Concave => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONCAVE,
            _ => TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_NONE
        };
        teklaWeld.FinishBelow = weld.BelowLine.Finish switch
        {
            WeldFinish.None => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_NONE,
            WeldFinish.Grind => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_GRIND,
            WeldFinish.Machine => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_MACHINE,
            WeldFinish.Chip => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_CHIP,
            WeldFinish.Flush => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_FINISHED_WELD,
            WeldFinish.Smooth => TSM.BaseWeld.WeldFinishEnum.WELS_FINISH_SMOOTH_TRANSITION,
            _ => TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_NONE
        };
        teklaWeld.RootFaceBelow = weld.BelowLine.RootFace;
        teklaWeld.RootOpeningBelow = weld.BelowLine.Gap;
        teklaWeld.IncrementAmountBelow = weld.BelowLine.NInrc;
        teklaWeld.LengthBelow = weld.BelowLine.Length;
        teklaWeld.PitchBelow = weld.BelowLine.Pitch;
    }

    public static void ToCalculusWeld(TSM.BaseWeld teklaWeld, Weld weld, string[] partIds)
    {
        weld.PartIds = partIds;

        if (teklaWeld is TSM.PolygonWeld teklaPolygonWeld)
        {
            weld.Polygon = [.. teklaPolygonWeld.Polygon.Points
                .OfType<TSG.Point>()
                .Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z))];
        }

        // Basic properties
        weld.EdgeAround = teklaWeld.AroundWeld switch
        {
            false => WeldEdgeAround.Edge,
            true => WeldEdgeAround.Around,
        };
        weld.SiteShop = teklaWeld.ShopWeld switch
        {
            true => WeldSiteShop.Shop,
            false => WeldSiteShop.Site,
        };
        if (teklaWeld is TSM.Weld w)
        {
            weld.Position = w.Position switch
            {
                TSM.Weld.WeldPositionEnum.WELD_POSITION_PLUS_X => WeldPosition.XPlus,
                TSM.Weld.WeldPositionEnum.WELD_POSITION_MINUS_X => WeldPosition.XMinus,
                TSM.Weld.WeldPositionEnum.WELD_POSITION_PLUS_Y => WeldPosition.YPlus,
                TSM.Weld.WeldPositionEnum.WELD_POSITION_MINUS_Y => WeldPosition.YMinus,
                TSM.Weld.WeldPositionEnum.WELD_POSITION_PLUS_Z => WeldPosition.ZPlus,
                TSM.Weld.WeldPositionEnum.WELD_POSITION_MINUS_Z => WeldPosition.ZMinus,
                _ => WeldPosition.XPlus
            };
        }
        weld.Shape = teklaWeld.IntermittentType switch
        {
            TSM.BaseWeld.WeldIntermittentTypeEnum.CONTINUOUS => WeldShape.Continuous,
            TSM.BaseWeld.WeldIntermittentTypeEnum.CHAIN_INTERMITTENT => WeldShape.Chain,
            TSM.BaseWeld.WeldIntermittentTypeEnum.STAGGERED_INTERMITTENT => WeldShape.Staggered,
            _ => WeldShape.Continuous
        };
        weld.ConnectAs = teklaWeld.ConnectAssemblies switch
        {
            true => WeldConnectAs.Subassembly,
            false => WeldConnectAs.Secondary,
        };

        // Above line
        weld.AboveLine = new WeldLine
        {
            Prefix = teklaWeld.PrefixAboveLine,
            Type = teklaWeld.TypeAbove switch
            {
                TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_NONE => WeldType.None,
                TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET => WeldType.Fillet,
                _ => WeldType.Fillet
            },
            Size = teklaWeld.SizeAbove,
            Angle = teklaWeld.AngleAbove,
            Contour = teklaWeld.ContourAbove switch
            {
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_NONE => WeldContour.None,
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_FLUSH => WeldContour.Flush,
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONVEX => WeldContour.Convex,
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONCAVE => WeldContour.Concave,
                _ => WeldContour.None
            },
            Finish = teklaWeld.FinishAbove switch
            {
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_NONE => WeldFinish.None,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_GRIND => WeldFinish.Grind,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_MACHINE => WeldFinish.Machine,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_CHIP => WeldFinish.Chip,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_FINISHED_WELD => WeldFinish.Flush,
                TSM.BaseWeld.WeldFinishEnum.WELS_FINISH_SMOOTH_TRANSITION => WeldFinish.Smooth,
                _ => WeldFinish.None
            },
            RootFace = teklaWeld.RootFaceAbove,
            Gap = teklaWeld.RootOpeningAbove,
            NInrc = teklaWeld.IncrementAmountAbove,
            Length = teklaWeld.LengthAbove,
            Pitch = teklaWeld.PitchAbove
        };

        // Below line
        weld.BelowLine = new WeldLine
        {
            Prefix = teklaWeld.PrefixBelowLine,
            Type = teklaWeld.TypeBelow switch
            {
                TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_NONE => WeldType.None,
                TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET => WeldType.Fillet,
                _ => WeldType.Fillet
            },
            Size = teklaWeld.SizeBelow,
            Angle = teklaWeld.AngleBelow,
            Contour = teklaWeld.ContourBelow switch
            {
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_NONE => WeldContour.None,
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_FLUSH => WeldContour.Flush,
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONVEX => WeldContour.Convex,
                TSM.BaseWeld.WeldContourEnum.WELD_CONTOUR_CONCAVE => WeldContour.Concave,
                _ => WeldContour.None
            },
            Finish = teklaWeld.FinishBelow switch
            {
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_NONE => WeldFinish.None,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_GRIND => WeldFinish.Grind,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_MACHINE => WeldFinish.Machine,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_CHIP => WeldFinish.Chip,
                TSM.BaseWeld.WeldFinishEnum.WELD_FINISH_FINISHED_WELD => WeldFinish.Flush,
                TSM.BaseWeld.WeldFinishEnum.WELS_FINISH_SMOOTH_TRANSITION => WeldFinish.Smooth,
                _ => WeldFinish.None
            },
            RootFace = teklaWeld.RootFaceBelow,
            Gap = teklaWeld.RootOpeningBelow,
            NInrc = teklaWeld.IncrementAmountBelow,
            Length = teklaWeld.LengthBelow,
            Pitch = teklaWeld.PitchBelow
        };

        weld.Userdata.Add("tekla_id", teklaWeld.Identifier.GUID.ToString());
    }

    // Rebar
    public static void ToTeklaRebar(Rebar rebar, TSM.Reinforcement teklaRebar)
    {
        teklaRebar.Name = rebar.Name;
        teklaRebar.Grade = rebar.Grade;
        teklaRebar.RadiusValues = [rebar.BendingRadius];
        teklaRebar.Class = rebar.Class;
        teklaRebar.NumberingSeries = new TSM.NumberingSeries
        {
            Prefix = rebar.RebarNumber.Prefix,
            StartNumber = rebar.RebarNumber.StartNumber,
        };
        teklaRebar.OnPlaneOffsets = [.. rebar.OnPlaneCover];
        teklaRebar.StartPointOffsetValue = rebar.StartOffset;
        teklaRebar.StartPointOffsetType = rebar.StartOffsetType switch
        {
            RebarOffsetType.Cover => TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_COVER_THICKNESS,
            RebarOffsetType.Length => TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_LEG_LENGTH,
            _ => TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_COVER_THICKNESS
        };
        teklaRebar.EndPointOffsetValue = rebar.EndOffset;
        teklaRebar.EndPointOffsetType = rebar.EndOffsetType switch
        {
            RebarOffsetType.Cover => TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_COVER_THICKNESS,
            RebarOffsetType.Length => TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_LEG_LENGTH,
            _ => TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_COVER_THICKNESS
        };

        //if (teklaRebar is TSM.SingleRebar singleRebar)
        //{
        //    singleRebar.Size = rebar.Size.ToString();
        //    singleRebar.Polygon = new TSM.Polygon();
        //    foreach (var point in rebar.Shapes.FirstOrDefault())
        //    {
        //        singleRebar.Polygon.AddPoint(new TSG.Point(point.X, point.Y, point.Z));
        //    }
        //}
        //else if (teklaRebar is TSM.RebarGroup rebarGroup)
        //{
        //    rebarGroup.Size = rebar.Size.ToString();
        //    rebarGroup.Polygons.Clear();
        //    foreach (var shape in rebar.Shapes)
        //    {
        //        var polygon = new TSM.Polygon();
        //        foreach (var point in shape)
        //        {
        //            polygon.AddPoint(new TSG.Point(point.X, point.Y, point.Z));
        //        }
        //        rebarGroup.Polygons.Add(polygon);
        //    }
        //    rebarGroup.StartPoint = new TSG.Point(rebar.Polygon[0].X, rebar.Polygon[0].Y, rebar.Polygon[0].Z);
        //    rebarGroup.EndPoint = new TSG.Point(rebar.Polygon[1].X, rebar.Polygon[1].Y, rebar.Polygon[1].Z);
        //    rebarGroup.StartFromPlaneOffset = rebar.FromPlaneCover.FirstOrDefault();
        //    rebarGroup.EndFromPlaneOffset = rebar.FromPlaneCover.Skip(1).FirstOrDefault();
        //    rebarGroup.SpacingType = rebar.Method switch
        //    {
        //        RebarMethod.Number => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_NUMBER,
        //        RebarMethod.Target => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_TARGET_SPACE,
        //        RebarMethod.ExactFirst => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_START,
        //        RebarMethod.ExactLast => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_END,
        //        RebarMethod.ExactMiddle => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_MIDDLE,
        //        RebarMethod.ExactFirstLast => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_BOTH,
        //        RebarMethod.Exact => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACINGS,
        //        _ => TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_NUMBER
        //    };
        //}
    }

    public static void ToCalculusRebar(TSM.Reinforcement teklaRebar, Rebar rebar, string fatherId)
    {
        rebar.FatherId = fatherId;
        rebar.Name = teklaRebar.Name;
        rebar.Grade = teklaRebar.Grade;
        rebar.BendingRadius = teklaRebar.RadiusValues.OfType<double>().FirstOrDefault();
        rebar.Class = teklaRebar.Class;
        rebar.RebarNumber = new()
        {
            Prefix = teklaRebar.NumberingSeries.Prefix,
            StartNumber = teklaRebar.NumberingSeries.StartNumber,
        };
        rebar.OnPlaneCover = [.. teklaRebar.OnPlaneOffsets.OfType<double>()];
        rebar.StartOffset = teklaRebar.StartPointOffsetValue;
        rebar.StartOffsetType = teklaRebar.StartPointOffsetType switch
        {
            TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_COVER_THICKNESS => RebarOffsetType.Cover,
            TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_LEG_LENGTH => RebarOffsetType.Length,
            _ => RebarOffsetType.Cover
        };
        rebar.EndOffset = teklaRebar.EndPointOffsetValue;
        rebar.EndOffsetType = teklaRebar.EndPointOffsetType switch
        {
            TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_COVER_THICKNESS => RebarOffsetType.Cover,
            TSM.Reinforcement.RebarOffsetTypeEnum.OFFSET_TYPE_LEG_LENGTH => RebarOffsetType.Length,
            _ => RebarOffsetType.Cover
        };

        if (teklaRebar is TSM.SingleRebar singleRebar)
        {
            rebar.Size = double.TryParse(singleRebar.Size, out var size) ? size : 1;
            rebar.Shapes = [[.. singleRebar.Polygon.Points
                .OfType<TSG.Point>()
                .Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z))
            ]];
        }
        else if (teklaRebar is TSM.RebarGroup rebarGroup)
        {
            rebar.Size = double.TryParse(rebarGroup.Size, out var size) ? size : 1;
            rebar.Shapes = [.. rebarGroup.Polygons
                .OfType<TSM.Polygon>()
                .Select(poly => poly.Points
                    .OfType<TSG.Point>()
                    .Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z))
                    .ToArray())
            ];
            rebar.Polygon = [
                new Vector3((float)rebarGroup.StartPoint.X, (float)rebarGroup.StartPoint.Y, (float)rebarGroup.StartPoint.Z),
                new Vector3((float)rebarGroup.EndPoint.X, (float)rebarGroup.EndPoint.Y, (float)rebarGroup.EndPoint.Z)
            ];

            rebar.FromPlaneCover = [rebarGroup.StartFromPlaneOffset, rebarGroup.EndFromPlaneOffset];

            rebar.Method = rebarGroup.SpacingType switch
            {
                TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_NUMBER => RebarMethod.Number,
                TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_TARGET_SPACE => RebarMethod.Target,
                TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_START => RebarMethod.ExactFirst,
                TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_END => RebarMethod.ExactLast,
                TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_MIDDLE => RebarMethod.ExactMiddle,
                TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACE_FLEX_AT_BOTH => RebarMethod.ExactFirstLast,
                TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACINGS => RebarMethod.Exact,
                _ => RebarMethod.Number
            };

            if (rebarGroup.SpacingType is TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_NUMBER)
            {
                rebar.Number = (int)rebarGroup.Spacings.OfType<double>().FirstOrDefault();
            }
            else
            {
                rebar.Spacings = [.. rebarGroup.Spacings.OfType<double>()];
            }

            rebar.Exclude = rebarGroup.ExcludeType switch
            {
                TSM.BaseRebarGroup.ExcludeTypeEnum.EXCLUDE_TYPE_NONE => RebarExclude.None,
                TSM.BaseRebarGroup.ExcludeTypeEnum.EXCLUDE_TYPE_FIRST => RebarExclude.First,
                TSM.BaseRebarGroup.ExcludeTypeEnum.EXCLUDE_TYPE_LAST => RebarExclude.Last,
                TSM.BaseRebarGroup.ExcludeTypeEnum.EXCLUDE_TYPE_BOTH => RebarExclude.FirstLast,
                _ => RebarExclude.None
            };
        }

        rebar.Userdata.Add("tekla_id", teklaRebar.Identifier.GUID.ToString());
    }

    // Grid
    static List<float> ParseNumberList(string s)
    {
        // The string may look like "0 6000.4 5000.6 3*4000"
        // That should be converted to a list of floats [0, 6000.4, 5000.6, 4000, 4000, 4000]
        var result = new List<float>();
        var parts = s.Split(' ');

        foreach (var part in parts)
        {
            if (part.Contains('*'))
            {
                var subParts = part.Split('*');
                if (subParts.Length == 2 && int.TryParse(subParts[0], out var count) && float.TryParse(subParts[1], out var value))
                {
                    result.AddRange(Enumerable.Repeat(value, count));
                }
            }
            else if (float.TryParse(part, out var value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    public static void ToTeklaGrid(Grid grid, TSM.Grid teklaGrid)
    {
        teklaGrid.Origin = new TSG.Point(grid.Origin.X, grid.Origin.Y, grid.Origin.Z);
        teklaGrid.CoordinateX = string.Join(" ", grid.XSpacings);
        teklaGrid.CoordinateY = string.Join(" ", grid.YSpacings);
        teklaGrid.CoordinateZ = string.Join(" ", grid.ZLevels);
        teklaGrid.LabelX = string.Join(" ", grid.XLabels);
        teklaGrid.LabelY = string.Join(" ", grid.YLabels);
        teklaGrid.LabelZ = string.Join(" ", grid.ZLabels);
    }

    public static void ToCalculusGrid(TSM.Grid teklaGrid, Grid grid)
    {
        grid.Origin = new Vector3((float)teklaGrid.Origin.X, (float)teklaGrid.Origin.Y, (float)teklaGrid.Origin.Z);
        grid.Rotation = 0;
        grid.XSpacings = ParseNumberList(teklaGrid.CoordinateX);
        grid.YSpacings = ParseNumberList(teklaGrid.CoordinateY);
        grid.ZLevels = ParseNumberList(teklaGrid.CoordinateZ);
        grid.XLabels = [.. teklaGrid.LabelX.Split(' ')];
        grid.YLabels = [.. teklaGrid.LabelY.Split(' ')];
        grid.ZLabels = [.. teklaGrid.LabelZ.Split(' ')];
        grid.Userdata.Add("tekla_id", teklaGrid.Identifier.GUID.ToString());
    }

    // Line
    public static void ToTeklaLine(Line line, TSM.ControlLine teklaLine)
    {
        teklaLine.Line = new TSG.LineSegment(
            new TSG.Point(line.Start.X, line.Start.Y, line.Start.Z),
            new TSG.Point(line.End.X, line.End.Y, line.End.Z));
        teklaLine.Color = TSM.ControlLine.ControlLineColorEnum.BLACK;
        teklaLine.Extension = 0;
        teklaLine.LineType = TSM.ControlObjectLineType.DashedLine;
    }

    public static void ToCalculusLine(TSM.ControlLine teklaLine, Line line)
    {
        line.Start = new Vector3(
            (float)teklaLine.Line.StartPoint.X,
            (float)teklaLine.Line.StartPoint.Y,
            (float)teklaLine.Line.StartPoint.Z);
        line.End = new Vector3(
            (float)teklaLine.Line.EndPoint.X,
            (float)teklaLine.Line.EndPoint.Y,
            (float)teklaLine.Line.EndPoint.Z);
        line.Class = 0;
        // Tekla is broken. It is impossible to read line Guid.
        //line.Userdata.Add("tekla_id", teklaLine.Identifier.GUID.ToString());
    }
}

public static class CalculusTeklaProfileMapper
{
    private static readonly List<string> TeklaProfileNames = [.. GetTeklaProfileNames()];
    private static IEnumerable<string> GetTeklaProfileNames()
    {
        var catalogHandler = new Tekla.Structures.Catalogs.CatalogHandler();
        var enumerator = catalogHandler.GetProfileItems();
        while (enumerator.MoveNext())
        {
            var profile = enumerator.Current;
            if (profile is Tekla.Structures.Catalogs.LibraryProfileItem libProfile)
            {
                yield return libProfile.ProfileName;
            }
        }
    }

    public static string CalculusProfileToTeklaProfile(string calculusProfile)
    {
        if (TeklaProfileNames.Contains(calculusProfile))
        {
            return calculusProfile;
        }

        // Rectangular profiles
        var rectangularMatch = System.Text.RegularExpressions.Regex.Match(calculusProfile, @"^(\d+)[*xX](\d+)$");
        if (rectangularMatch.Success)
        {
            return calculusProfile;
        }
        // Circular profiles
        var circularMatch = System.Text.RegularExpressions.Regex.Match(calculusProfile, @"^D(\d+)$");
        if (circularMatch.Success)
        {
            return calculusProfile;
        }

        return TeklaProfileNames.MinBy(s => LevenshteinDistance.Compute(s, calculusProfile))!;
    }

}

public static class LevenshteinDistance
{
    /// <summary>
    /// Compute the distance between two strings.
    /// </summary>
    public static int Compute(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        // Step 1
        if (n == 0)
        {
            return m;
        }

        if (m == 0)
        {
            return n;
        }

        // Step 2
        for (int i = 0; i <= n; d[i, 0] = i++)
        {
        }

        for (int j = 0; j <= m; d[0, j] = j++)
        {
        }

        // Step 3
        for (int i = 1; i <= n; i++)
        {
            //Step 4
            for (int j = 1; j <= m; j++)
            {
                // Step 5
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                // Step 6
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        // Step 7
        return d[n, m];
    }
}