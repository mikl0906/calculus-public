using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalculusModel;

// Interfaces, for additional type constraint only

public interface IBeamPosition
{
    double Angle { get; set; }
    BeamHorizontalPosition YPosition { get; set; }
    double YOffset { get; set; }
    BeamVerticalPosition ZPosition { get; set; }
    double ZOffset { get; set; }
    Vector3 StartOffset { get; set; }
    Vector3 EndOffset { get; set; }
}

public interface IPlatePosition
{
    PlatePosition ZPosition { get; set; }
    double ZOffset { get; set; }
}

// -----------------------------------------------------------------------------
// Primitives
// -----------------------------------------------------------------------------

public class Vec2(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}

public class Vec3(double x, double y, double z)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;
}

public class Vec2WithChamfer(double x, double y, Chamfer? chamfer)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Chamfer? Chamfer { get; set; } = chamfer;
}

public class Vec3WithChamfer
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Chamfer? Chamfer { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Dz1 { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Dz2 { get; set; }
}

public class CoordinateSystem
{
    public Vector3 Origin { get; set; }
    public Vector3 AxisX { get; set; }
    public Vector3 AxisY { get; set; }
}

public abstract class Chamfer;

public class LineChamfer(double x, double y) : Chamfer
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}

public class RoundChamfer(double r) : Chamfer
{
    public double R { get; set; } = r;
}

public class ArcChamfer(double r) : Chamfer
{
    public double R { get; set; } = r;
}

public class ArcPointChamfer : Chamfer;

public class SquareChamfer(double x, double y) : Chamfer
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}

public class SquareParallelChamfer(double x, double y) : Chamfer
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}

public class LineAndArcChamfer(double x, double y) : Chamfer
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}

public class NumberingSeries
{
    public string Prefix { get; set; } = string.Empty;
    public int StartNumber { get; set; }
}

// -----------------------------------------------------------------------------
// Structural model
// -----------------------------------------------------------------------------

public abstract class ModelObject;

// Beam
public enum BeamHorizontalPosition { Left, Center, Right }
public enum BeamVerticalPosition { Top, Center, Bottom }
public class Beam : ModelObject, IBeamPosition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Vector3 Start { get; set; }
    public Vector3 End { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int Class { get; set; }
    public NumberingSeries PartNumber { get; set; } = null!;
    public NumberingSeries AssemblyNumber { get; set; } = null!;
    public double Angle { get; set; }
    public BeamHorizontalPosition YPosition { get; set; }
    public double YOffset { get; set; }
    public BeamVerticalPosition ZPosition { get; set; }
    public double ZOffset { get; set; }
    public Vector3 StartOffset { get; set; }
    public Vector3 EndOffset { get; set; }
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// Plate
public enum PlatePosition { Top, Center, Bottom }
public class Plate : ModelObject, IPlatePosition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Vec3WithChamfer[] Polygon { get; set; } = [];
    public string Name { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int Class { get; set; }
    public NumberingSeries PartNumber { get; set; } = null!;
    public NumberingSeries AssemblyNumber { get; set; } = null!;
    public PlatePosition ZPosition { get; set; }
    public double ZOffset { get; set; }
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// Cut beam / cut plate: a beam- or plate-shaped cutting volume, its shape
// fields flat beside partIds (format v18; a `cutPart` with a nested `cutter`
// before). CutPart is only the C# base, never a JSON type.
public abstract class CutPart : ModelObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string[] PartIds { get; set; } = [];
    public string Name { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int Class { get; set; }
    public Dictionary<string, string> Userdata { get; set; } = [];
}

public class CutBeam : CutPart, IBeamPosition
{
    public Vector3 Start { get; set; }
    public Vector3 End { get; set; }
    public double Angle { get; set; }
    public BeamHorizontalPosition YPosition { get; set; }
    public double YOffset { get; set; }
    public BeamVerticalPosition ZPosition { get; set; }
    public double ZOffset { get; set; }
    public Vector3 StartOffset { get; set; }
    public Vector3 EndOffset { get; set; }
}

public class CutPlate : CutPart, IPlatePosition
{
    public Vec3WithChamfer[] Polygon { get; set; } = [];
    public PlatePosition ZPosition { get; set; }
    public double ZOffset { get; set; }
}

// CutPlane
public class CutPlane : ModelObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string[] PartIds { get; set; } = [];
    public Vector3 P1 { get; set; }
    public Vector3 P2 { get; set; }
    public Vector3 P3 { get; set; }
    public bool Fit { get; set; } = false;
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// Bolt
public enum BoltSiteShop { Site, Shop }
public enum BoltConnectAs { None, Secondary, Subassembly }
public enum BoltGroupType { Array, Circle, List }
public class Bolt : ModelObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string[] PartIds { get; set; } = [];
    public Vector3 P1 { get; set; }
    public Vector3 P2 { get; set; }
    public Vector3 P3 { get; set; }
    public string Standard { get; set; } = string.Empty;
    public double Size { get; set; }
    public string Grade { get; set; } = string.Empty;
    public BoltSiteShop SiteShop { get; set; }
    public BoltConnectAs ConnectAs { get; set; }
    public bool ThreadInMaterial { get; set; }
    public double CutLength { get; set; }
    public double ExtraLength { get; set; }
    [JsonPropertyName("bolt")]
    // Bolt assembly
    public bool BoltCheck { get; set; }
    public bool Washer1 { get; set; }
    public bool Washer2 { get; set; }
    public bool Washer3 { get; set; }
    public bool Nut1 { get; set; }
    public bool Nut2 { get; set; }
    // Group
    public BoltGroupType GroupType { get; set; }
    public double[] DistX { get; set; } = [];
    public double[] DistY { get; set; } = [];
    public double[] CircleRadius { get; set; } = [];
    public int CircleNumber { get; set; }
    public Vector3 Offset { get; set; }
    // Holes
    public double Tolerance { get; set; }
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// Weld
public enum WeldType { None, Fillet, ButtV, ButtBevel, ButtVBroad, ButtBevelBroad, ButtU, ButtJ, FlareV, FlareBevel, Plug, ButtVSteep, ButtBevelSteep, Edge }
public enum WeldContour { None, Flush, Convex, Concave }
public enum WeldFinish { None, Grind, Machine, Chip, Flush, Smooth }
public class WeldLine
{
    public string Prefix { get; set; } = string.Empty;
    public double Size { get; set; }
    public WeldType Type { get; set; }
    public double Angle { get; set; }
    public double RootFace { get; set; }
    public double Gap { get; set; }
    public WeldContour Contour { get; set; }
    public WeldFinish Finish { get; set; }
    public int NInrc { get; set; }
    public double Length { get; set; }
    public double Pitch { get; set; }
}

public enum WeldPosition { XPlus, XMinus, YPlus, YMinus, ZPlus, ZMinus }
public enum WeldEdgeAround { Edge, Around }
public enum WeldSiteShop { Site, Shop }
public enum WeldShape { Continuous, Chain, Staggered }
public enum WeldConnectAs { None, Secondary, Subassembly }
public class Weld : ModelObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string[] PartIds { get; set; } = [];
    public Vector3[] Polygon { get; set; } = [];
    public WeldPosition Position { get; set; }
    public WeldEdgeAround EdgeAround { get; set; }
    public WeldSiteShop SiteShop { get; set; }
    public WeldShape Shape { get; set; }
    public WeldConnectAs ConnectAs { get; set; }
    public WeldLine AboveLine { get; set; } = new WeldLine();
    public WeldLine BelowLine { get; set; } = new WeldLine();
    public string Tail { get; set; } = string.Empty;
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// Rebar
public enum RebarOffsetType { Cover, Length }
public enum RebarMethod { Number, Target, Exact, ExactFirst, ExactLast, ExactMiddle, ExactFirstLast }
public enum RebarExclude { None, First, Last, FirstLast }

public class Rebar : ModelObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FatherId { get; set; } = string.Empty;
    public Vector3[][] Shapes { get; set; } = [];
    public Vector3[] Polygon { get; set; } = [];
    public string Name { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public double Size { get; set; }
    public double BendingRadius { get; set; }
    public int Class { get; set; }
    public NumberingSeries RebarNumber { get; set; } = null!;
    public double[] OnPlaneCover { get; set; } = [];
    public double[] FromPlaneCover { get; set; } = [];
    public double StartOffset { get; set; }
    public RebarOffsetType StartOffsetType { get; set; }
    public double EndOffset { get; set; }
    public RebarOffsetType EndOffsetType { get; set; }
    public RebarMethod Method { get; set; }
    public int Number { get; set; }
    public double[] Spacings { get; set; } = [];
    public RebarExclude Exclude { get; set; }
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// Grid
public class Grid : ModelObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Vector3 Origin { get; set; }
    public float Rotation { get; set; }
    public List<float> XSpacings { get; set; } = [];
    public List<float> YSpacings { get; set; } = [];
    public List<float> ZLevels { get; set; } = [];
    public List<string> XLabels { get; set; } = [];
    public List<string> YLabels { get; set; } = [];
    public List<string> ZLabels { get; set; } = [];
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// Line
public class Line : ModelObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Vector3 Start { get; set; }
    public Vector3 End { get; set; }
    public int Class { get; set; }
    public Dictionary<string, string> Userdata { get; set; } = [];
}

// -----------------------------------------------------------------------------
// Calculus Model
// -----------------------------------------------------------------------------

public class Model
{
    public string Name { get; set; } = string.Empty;
    public List<ModelObject> Objects { get; set; } = [];
}