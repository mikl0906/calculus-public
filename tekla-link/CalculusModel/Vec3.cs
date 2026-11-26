namespace CalculusModel;

/// <summary>
/// A point or vector of the .calculus format: millimetres in double precision,
/// as Calculus and Tekla hold them. (The mirror used System.Numerics.Vector3,
/// which is float: a coordinate 10 km from the origin lost half a millimetre
/// on the way through, one in national grid coordinates hundreds — measured
/// Nov 26 2026.) Serialized as <c>{ "x", "y", "z" }</c>.
/// </summary>
public struct Vec3(double x, double y, double z) : IEquatable<Vec3>
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
    public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

    public readonly double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);

    public static Vec3 Subtract(Vec3 a, Vec3 b) => a - b;
    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static Vec3 Normalize(Vec3 v)
    {
        var length = v.Length();
        return length > 0 ? v * (1 / length) : v;
    }

    public readonly bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override readonly bool Equals(object? obj) => obj is Vec3 v && Equals(v);
    public override readonly int GetHashCode() => (X, Y, Z).GetHashCode();
}
