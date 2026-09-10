using OcctNet;

namespace OCCAD;

internal static class CadTransformMath
{
    private const double Epsilon = 1e-12;

    public static OcctVector3d Normalize(OcctVector3d value, string name)
    {
        if (!value.TryNormalize(out var normalized))
            throw new ArgumentOutOfRangeException(name, "Vector must be finite and non-zero.");
        return normalized;
    }

    public static OcctPoint3d RotatePoint(
        OcctPoint3d point,
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        if (!point.IsFinite) throw new ArgumentOutOfRangeException(nameof(point));
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        var direction = new OcctVector3d(point.X - center.X, point.Y - center.Y, point.Z - center.Z);
        var rotated = RotateVector(direction, axis, angleDegrees);
        return new OcctPoint3d(center.X + rotated.X, center.Y + rotated.Y, center.Z + rotated.Z);
    }

    public static OcctVector3d RotateVector(OcctVector3d vector, OcctVector3d axis, double angleDegrees)
    {
        var unit = Normalize(axis, nameof(axis));
        if (!double.IsFinite(angleDegrees)) throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dot = Dot(unit, vector);
        var cross = unit.Cross(vector);
        return new OcctVector3d(
            vector.X * cos + cross.X * sin + unit.X * dot * (1.0 - cos),
            vector.Y * cos + cross.Y * sin + unit.Y * dot * (1.0 - cos),
            vector.Z * cos + cross.Z * sin + unit.Z * dot * (1.0 - cos));
    }

    public static OcctPoint3d ScalePoint(OcctPoint3d point, OcctPoint3d center, double factor)
    {
        if (!point.IsFinite) throw new ArgumentOutOfRangeException(nameof(point));
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        ValidateScale(factor);
        return new OcctPoint3d(
            center.X + (point.X - center.X) * factor,
            center.Y + (point.Y - center.Y) * factor,
            center.Z + (point.Z - center.Z) * factor);
    }

    public static void ValidateScale(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(factor), "Scale factor must be finite and greater than zero.");
    }

    public static double Dot(OcctVector3d left, OcctVector3d right) =>
        left.X * right.X + left.Y * right.Y + left.Z * right.Z;

    public static OcctVector3d Between(OcctPoint3d from, OcctPoint3d to) =>
        new(to.X - from.X, to.Y - from.Y, to.Z - from.Z);

    public static OcctPoint3d Add(OcctPoint3d point, OcctVector3d vector, double scale = 1.0) =>
        new(point.X + vector.X * scale, point.Y + vector.Y * scale, point.Z + vector.Z * scale);

    public static (OcctVector3d XAxis, OcctVector3d YAxis) PerpendicularAxes(OcctVector3d normal)
    {
        var n = Normalize(normal, nameof(normal));
        var reference = Math.Abs(n.X) < 0.9
            ? OcctVector3d.UnitX
            : OcctVector3d.UnitY;
        var projection = Dot(reference, n);
        var x = Normalize(
            new OcctVector3d(
                reference.X - n.X * projection,
                reference.Y - n.Y * projection,
                reference.Z - n.Z * projection),
            nameof(normal));
        var y = n.Cross(x).Normalized();
        return (x, y);
    }

    public static double SignedAngleDegrees(OcctVector3d from, OcctVector3d to, OcctVector3d axis)
    {
        var a = Normalize(from, nameof(from));
        var b = Normalize(to, nameof(to));
        var n = Normalize(axis, nameof(axis));
        var sin = Dot(n, a.Cross(b));
        var cos = Math.Clamp(Dot(a, b), -1.0, 1.0);
        return Math.Atan2(sin, cos) * 180.0 / Math.PI;
    }


    public static OcctPoint3d TransformPoint(OcctTransform3d transform, OcctPoint3d point)
    {
        if (!transform.IsFinite) throw new ArgumentException("Transformation must be finite.", nameof(transform));
        if (!point.IsFinite) throw new ArgumentOutOfRangeException(nameof(point));
        return new OcctPoint3d(
            transform.M00 * point.X + transform.M01 * point.Y + transform.M02 * point.Z + transform.M03,
            transform.M10 * point.X + transform.M11 * point.Y + transform.M12 * point.Z + transform.M13,
            transform.M20 * point.X + transform.M21 * point.Y + transform.M22 * point.Z + transform.M23);
    }

    public static OcctTransform3d Multiply(OcctTransform3d left, OcctTransform3d right)
    {
        if (!left.IsFinite) throw new ArgumentException("Transformation must be finite.", nameof(left));
        if (!right.IsFinite) throw new ArgumentException("Transformation must be finite.", nameof(right));

        return new OcctTransform3d(
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
    }

    public static OcctTransform3d TranslationTransform(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        return OcctTransform3d.Translation(displacement.X, displacement.Y, displacement.Z);
    }

    public static OcctTransform3d RotationTransform(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        var unit = Normalize(axis, nameof(axis));
        if (!double.IsFinite(angleDegrees)) throw new ArgumentOutOfRangeException(nameof(angleDegrees));

        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var oneMinusCos = 1.0 - cos;
        var x = unit.X;
        var y = unit.Y;
        var z = unit.Z;

        var m00 = cos + x * x * oneMinusCos;
        var m01 = x * y * oneMinusCos - z * sin;
        var m02 = x * z * oneMinusCos + y * sin;
        var m10 = y * x * oneMinusCos + z * sin;
        var m11 = cos + y * y * oneMinusCos;
        var m12 = y * z * oneMinusCos - x * sin;
        var m20 = z * x * oneMinusCos - y * sin;
        var m21 = z * y * oneMinusCos + x * sin;
        var m22 = cos + z * z * oneMinusCos;

        return new OcctTransform3d(
            m00, m01, m02, center.X - (m00 * center.X + m01 * center.Y + m02 * center.Z),
            m10, m11, m12, center.Y - (m10 * center.X + m11 * center.Y + m12 * center.Z),
            m20, m21, m22, center.Z - (m20 * center.X + m21 * center.Y + m22 * center.Z));
    }

    public static OcctTransform3d ScaleTransform(OcctPoint3d center, double factor)
    {
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        ValidateScale(factor);
        return new OcctTransform3d(
            factor, 0, 0, center.X * (1.0 - factor),
            0, factor, 0, center.Y * (1.0 - factor),
            0, 0, factor, center.Z * (1.0 - factor));
    }

    private static void ValidateDisplacement(OcctVector3d displacement)
    {
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement), "Displacement must be finite.");
    }

    public static bool TryGetAxisAngle(
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        OcctVector3d zAxis,
        out OcctVector3d axis,
        out double angleDegrees)
    {
        var x = Normalize(xAxis, nameof(xAxis));
        var y = Normalize(yAxis, nameof(yAxis));
        var z = Normalize(zAxis, nameof(zAxis));

        var m00 = x.X; var m01 = y.X; var m02 = z.X;
        var m10 = x.Y; var m11 = y.Y; var m12 = z.Y;
        var m20 = x.Z; var m21 = y.Z; var m22 = z.Z;

        var cos = Math.Clamp((m00 + m11 + m22 - 1.0) * 0.5, -1.0, 1.0);
        var angle = Math.Acos(cos);
        if (angle <= Epsilon)
        {
            axis = OcctVector3d.UnitZ;
            angleDegrees = 0.0;
            return false;
        }

        if (Math.PI - angle <= 1e-8)
        {
            var xx = Math.Max(0.0, (m00 + 1.0) * 0.5);
            var yy = Math.Max(0.0, (m11 + 1.0) * 0.5);
            var zz = Math.Max(0.0, (m22 + 1.0) * 0.5);
            var ax = Math.Sqrt(xx);
            var ay = Math.CopySign(Math.Sqrt(yy), m01 + m10);
            var az = Math.CopySign(Math.Sqrt(zz), m02 + m20);
            var candidate = new OcctVector3d(ax, ay, az);
            axis = candidate.TryNormalize(out var normalized) ? normalized : OcctVector3d.UnitX;
        }
        else
        {
            axis = new OcctVector3d(m21 - m12, m02 - m20, m10 - m01).Normalized();
        }

        angleDegrees = angle * 180.0 / Math.PI;
        return true;
    }
}
