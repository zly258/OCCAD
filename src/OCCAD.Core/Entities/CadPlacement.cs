using OcctNet;

namespace OCCAD;

/// <summary>
/// Rigid local-to-world placement for an entity. Definition geometry remains in
/// the entity's local coordinates; ordinary move/rotate operations compose this
/// placement instead of rewriting feature parameters.
/// </summary>
public readonly record struct CadPlacement
{
    public CadPlacement(OcctTransform3d transform)
    {
        if (!transform.IsFinite)
            throw new ArgumentException(
                "Placement transform must contain only finite values.",
                nameof(transform));
        if (!IsRigidTransform(transform))
            throw new ArgumentException(
                "Placement must be a rigid transform without scale or reflection.",
                nameof(transform));

        Transform = transform;
    }

    public OcctTransform3d Transform { get; }

    public static CadPlacement Identity =>
        new(OcctTransform3d.Identity);

    public bool IsIdentity =>
        Transform == OcctTransform3d.Identity;

    public OcctPoint3d Position =>
        ToWorldPoint(OcctPoint3d.Origin);

    public (OcctPoint3d Origin, OcctVector3d XAxis, OcctVector3d YAxis, OcctVector3d ZAxis) GetWorldAxes() =>
        (Position,
         ToWorldVector(new OcctVector3d(1, 0, 0)),
         ToWorldVector(new OcctVector3d(0, 1, 0)),
         ToWorldVector(new OcctVector3d(0, 0, 1)));

    public CadPlacement TranslateWorld(OcctVector3d displacement)
    {
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement));

        return new CadPlacement(
            Multiply(
                OcctTransform3d.Translation(
                    displacement.X,
                    displacement.Y,
                    displacement.Z),
                Transform));
    }

    public CadPlacement RotateWorld(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        if (!center.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(center));
        if (!axis.TryNormalize(out var normal))
            throw new ArgumentOutOfRangeException(nameof(axis));
        if (!double.IsFinite(angleDegrees))
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));

        var radians = angleDegrees * Math.PI / 180.0;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var oneMinusCosine = 1.0 - cosine;
        var x = normal.X;
        var y = normal.Y;
        var z = normal.Z;

        var r00 = cosine + x * x * oneMinusCosine;
        var r01 = x * y * oneMinusCosine - z * sine;
        var r02 = x * z * oneMinusCosine + y * sine;
        var r10 = y * x * oneMinusCosine + z * sine;
        var r11 = cosine + y * y * oneMinusCosine;
        var r12 = y * z * oneMinusCosine - x * sine;
        var r20 = z * x * oneMinusCosine - y * sine;
        var r21 = z * y * oneMinusCosine + x * sine;
        var r22 = cosine + z * z * oneMinusCosine;

        var tx =
            center.X -
            (r00 * center.X + r01 * center.Y + r02 * center.Z);
        var ty =
            center.Y -
            (r10 * center.X + r11 * center.Y + r12 * center.Z);
        var tz =
            center.Z -
            (r20 * center.X + r21 * center.Y + r22 * center.Z);

        var rotation = new OcctTransform3d(
            r00, r01, r02, tx,
            r10, r11, r12, ty,
            r20, r21, r22, tz);

        return new CadPlacement(
            Multiply(rotation, Transform));
    }

    public OcctPoint3d ToWorldPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(point));

        var t = Transform;
        return new OcctPoint3d(
            t.M00 * point.X + t.M01 * point.Y + t.M02 * point.Z + t.M03,
            t.M10 * point.X + t.M11 * point.Y + t.M12 * point.Z + t.M13,
            t.M20 * point.X + t.M21 * point.Y + t.M22 * point.Z + t.M23);
    }

    public OcctVector3d ToWorldVector(OcctVector3d vector)
    {
        if (!vector.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(vector));

        var t = Transform;
        return new OcctVector3d(
            t.M00 * vector.X + t.M01 * vector.Y + t.M02 * vector.Z,
            t.M10 * vector.X + t.M11 * vector.Y + t.M12 * vector.Z,
            t.M20 * vector.X + t.M21 * vector.Y + t.M22 * vector.Z);
    }

    public OcctPoint3d ToLocalPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(point));

        var t = Transform;
        var x = point.X - t.M03;
        var y = point.Y - t.M13;
        var z = point.Z - t.M23;

        // Rigid placement inverse: R^-1 = R^T.
        return new OcctPoint3d(
            t.M00 * x + t.M10 * y + t.M20 * z,
            t.M01 * x + t.M11 * y + t.M21 * z,
            t.M02 * x + t.M12 * y + t.M22 * z);
    }

    public OcctVector3d ToLocalVector(OcctVector3d vector)
    {
        if (!vector.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(vector));

        var t = Transform;
        return new OcctVector3d(
            t.M00 * vector.X + t.M10 * vector.Y + t.M20 * vector.Z,
            t.M01 * vector.X + t.M11 * vector.Y + t.M21 * vector.Z,
            t.M02 * vector.X + t.M12 * vector.Y + t.M22 * vector.Z);
    }

    private static OcctTransform3d Multiply(
        OcctTransform3d left,
        OcctTransform3d right) =>
        new(
            left.M00 * right.M00 + left.M01 * right.M10 + left.M02 * right.M20,
            left.M00 * right.M01 + left.M01 * right.M11 + left.M02 * right.M21,
            left.M00 * right.M02 + left.M01 * right.M12 + left.M02 * right.M22,
            left.M00 * right.M03 + left.M01 * right.M13 + left.M02 * right.M23 + left.M03,

            left.M10 * right.M00 + left.M11 * right.M10 + left.M12 * right.M20,
            left.M10 * right.M01 + left.M11 * right.M11 + left.M12 * right.M21,
            left.M10 * right.M02 + left.M11 * right.M12 + left.M12 * right.M22,
            left.M10 * right.M03 + left.M11 * right.M13 + left.M12 * right.M23 + left.M13,

            left.M20 * right.M00 + left.M21 * right.M10 + left.M22 * right.M20,
            left.M20 * right.M01 + left.M21 * right.M11 + left.M22 * right.M21,
            left.M20 * right.M02 + left.M21 * right.M12 + left.M22 * right.M22,
            left.M20 * right.M03 + left.M21 * right.M13 + left.M22 * right.M23 + left.M23);

    private static bool IsRigidTransform(
        OcctTransform3d value)
    {
        const double tolerance = 1e-8;

        var x = new OcctVector3d(value.M00, value.M10, value.M20);
        var y = new OcctVector3d(value.M01, value.M11, value.M21);
        var z = new OcctVector3d(value.M02, value.M12, value.M22);

        if (Math.Abs(x.Length - 1.0) > tolerance ||
            Math.Abs(y.Length - 1.0) > tolerance ||
            Math.Abs(z.Length - 1.0) > tolerance ||
            Math.Abs(x.Dot(y)) > tolerance ||
            Math.Abs(x.Dot(z)) > tolerance ||
            Math.Abs(y.Dot(z)) > tolerance)
            return false;

        var determinant = x.Dot(y.Cross(z));
        return Math.Abs(determinant - 1.0) <= tolerance;
    }
}
