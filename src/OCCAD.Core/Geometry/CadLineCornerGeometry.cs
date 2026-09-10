using OcctNet;

namespace OCCAD;

internal static class CadLineCornerGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryFillet(
        CadLineEntity first,
        OcctPoint3d firstPick,
        CadLineEntity second,
        OcctPoint3d secondPick,
        double radius,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];
        if (!double.IsFinite(radius) || radius <= Tolerance)
            return false;

        if (!TryCorner(
                first,
                firstPick,
                second,
                secondPick,
                plane,
                out var corner))
            return false;

        var cosine = Math.Clamp(
            Dot(corner.FirstDirection, corner.SecondDirection),
            -1.0,
            1.0);
        var angle = Math.Acos(cosine);
        if (angle <= 1e-7 || angle >= Math.PI - 1e-7)
            return false;

        var tangentDistance =
            radius / Math.Tan(angle * 0.5);
        if (!double.IsFinite(tangentDistance) ||
            tangentDistance <= Tolerance ||
            tangentDistance >= corner.FirstAvailable - Tolerance ||
            tangentDistance >= corner.SecondAvailable - Tolerance)
            return false;

        var firstTangent =
            Add(corner.Intersection, corner.FirstDirection, tangentDistance);
        var secondTangent =
            Add(corner.Intersection, corner.SecondDirection, tangentDistance);

        var bisector = Normalize(
            new CadPlanePoint(
                corner.FirstDirection.X + corner.SecondDirection.X,
                corner.FirstDirection.Y + corner.SecondDirection.Y));
        var centerDistance =
            radius / Math.Sin(angle * 0.5);
        var center =
            Add(corner.Intersection, bisector, centerDistance);

        var firstRadius = new CadPlanePoint(
            firstTangent.X - center.X,
            firstTangent.Y - center.Y);
        var secondRadius = new CadPlanePoint(
            secondTangent.X - center.X,
            secondTangent.Y - center.Y);
        var middleDirection = Normalize(
            new CadPlanePoint(
                firstRadius.X + secondRadius.X,
                firstRadius.Y + secondRadius.Y));
        var middle =
            Add(center, middleDirection, radius);

        var firstTrimmed = first.CreateLine(
            corner.FirstFarPoint,
            plane.LocalToWorld(firstTangent));
        var secondTrimmed = second.CreateLine(
            corner.SecondFarPoint,
            plane.LocalToWorld(secondTangent));
        var arc = first.CreateArc(
            plane.LocalToWorld(firstTangent),
            plane.LocalToWorld(middle),
            plane.LocalToWorld(secondTangent));

        replacements =
        [
            firstTrimmed,
            secondTrimmed,
            arc
        ];
        return true;
    }

    internal static bool TryChamfer(
        CadLineEntity first,
        OcctPoint3d firstPick,
        CadLineEntity second,
        OcctPoint3d secondPick,
        double firstDistance,
        double secondDistance,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];

        if (!double.IsFinite(firstDistance) ||
            !double.IsFinite(secondDistance) ||
            firstDistance <= Tolerance ||
            secondDistance <= Tolerance)
            return false;

        if (!TryCorner(
                first,
                firstPick,
                second,
                secondPick,
                plane,
                out var corner))
            return false;

        if (firstDistance >= corner.FirstAvailable - Tolerance ||
            secondDistance >= corner.SecondAvailable - Tolerance)
            return false;

        var firstPoint =
            Add(corner.Intersection, corner.FirstDirection, firstDistance);
        var secondPoint =
            Add(corner.Intersection, corner.SecondDirection, secondDistance);

        if (Distance(firstPoint, secondPoint) <= Tolerance)
            return false;

        replacements =
        [
            first.CreateLine(
                corner.FirstFarPoint,
                plane.LocalToWorld(firstPoint)),
            second.CreateLine(
                corner.SecondFarPoint,
                plane.LocalToWorld(secondPoint)),
            first.CreateLine(
                plane.LocalToWorld(firstPoint),
                plane.LocalToWorld(secondPoint))
        ];
        return true;
    }

    private static bool TryCorner(
        CadLineEntity first,
        OcctPoint3d firstPick,
        CadLineEntity second,
        OcctPoint3d secondPick,
        CadWorkPlane plane,
        out Corner corner)
    {
        corner = default;

        if (ReferenceEquals(first, second) ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(first, plane) ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(second, plane))
            return false;

        var a = plane.WorldToLocal(first.Start);
        var b = plane.WorldToLocal(first.End);
        var c = plane.WorldToLocal(second.Start);
        var d = plane.WorldToLocal(second.End);
        var firstPickLocal = plane.WorldToLocal(firstPick);
        var secondPickLocal = plane.WorldToLocal(secondPick);

        if (!TryInfiniteIntersection(a, b, c, d, out var intersection))
            return false;

        if (!TrySelectedRay(
                a,
                b,
                firstPickLocal,
                intersection,
                out var firstDirection,
                out var firstFar,
                out var firstAvailable) ||
            !TrySelectedRay(
                c,
                d,
                secondPickLocal,
                intersection,
                out var secondDirection,
                out var secondFar,
                out var secondAvailable))
            return false;

        var cross =
            Cross(
                firstDirection.X,
                firstDirection.Y,
                secondDirection.X,
                secondDirection.Y);
        if (Math.Abs(cross) <= Tolerance)
            return false;

        corner = new Corner(
            intersection,
            firstDirection,
            secondDirection,
            plane.LocalToWorld(firstFar),
            plane.LocalToWorld(secondFar),
            firstAvailable,
            secondAvailable);
        return true;
    }

    private static bool TrySelectedRay(
        CadPlanePoint start,
        CadPlanePoint end,
        CadPlanePoint pick,
        CadPlanePoint intersection,
        out CadPlanePoint direction,
        out CadPlanePoint farPoint,
        out double available)
    {
        var raw = new CadPlanePoint(
            end.X - start.X,
            end.Y - start.Y);
        var length = Math.Sqrt(
            raw.X * raw.X + raw.Y * raw.Y);
        if (length <= Tolerance)
        {
            direction = default;
            farPoint = default;
            available = 0.0;
            return false;
        }

        var unit = new CadPlanePoint(
            raw.X / length,
            raw.Y / length);
        var pickProjection =
            (pick.X - intersection.X) * unit.X +
            (pick.Y - intersection.Y) * unit.Y;
        if (Math.Abs(pickProjection) <= Tolerance)
        {
            var startProjection =
                (start.X - intersection.X) * unit.X +
                (start.Y - intersection.Y) * unit.Y;
            var endProjection =
                (end.X - intersection.X) * unit.X +
                (end.Y - intersection.Y) * unit.Y;
            pickProjection =
                Math.Abs(startProjection) >= Math.Abs(endProjection)
                    ? startProjection
                    : endProjection;
        }

        var sign = Math.Sign(pickProjection);
        if (sign == 0)
        {
            direction = default;
            farPoint = default;
            available = 0.0;
            return false;
        }

        direction = new CadPlanePoint(
            unit.X * sign,
            unit.Y * sign);

        var startDistance =
            (start.X - intersection.X) * direction.X +
            (start.Y - intersection.Y) * direction.Y;
        var endDistance =
            (end.X - intersection.X) * direction.X +
            (end.Y - intersection.Y) * direction.Y;

        if (startDistance <= Tolerance &&
            endDistance <= Tolerance)
        {
            farPoint = default;
            available = 0.0;
            return false;
        }

        if (startDistance >= endDistance)
        {
            farPoint = start;
            available = startDistance;
        }
        else
        {
            farPoint = end;
            available = endDistance;
        }

        return available > Tolerance;
    }

    private static bool TryInfiniteIntersection(
        CadPlanePoint a,
        CadPlanePoint b,
        CadPlanePoint c,
        CadPlanePoint d,
        out CadPlanePoint intersection)
    {
        var rx = b.X - a.X;
        var ry = b.Y - a.Y;
        var sx = d.X - c.X;
        var sy = d.Y - c.Y;
        var denominator = Cross(rx, ry, sx, sy);
        if (Math.Abs(denominator) <= Tolerance)
        {
            intersection = default;
            return false;
        }

        var qx = c.X - a.X;
        var qy = c.Y - a.Y;
        var t = Cross(qx, qy, sx, sy) / denominator;
        intersection = new CadPlanePoint(
            a.X + rx * t,
            a.Y + ry * t);
        return double.IsFinite(intersection.X) &&
               double.IsFinite(intersection.Y);
    }

    private static CadPlanePoint Normalize(CadPlanePoint value)
    {
        var length = Math.Sqrt(value.X * value.X + value.Y * value.Y);
        if (length <= Tolerance)
            throw new InvalidOperationException("Direction is degenerate.");
        return new CadPlanePoint(
            value.X / length,
            value.Y / length);
    }

    private static CadPlanePoint Add(
        CadPlanePoint point,
        CadPlanePoint direction,
        double distance) =>
        new(
            point.X + direction.X * distance,
            point.Y + direction.Y * distance);

    private static double Dot(
        CadPlanePoint first,
        CadPlanePoint second) =>
        first.X * second.X + first.Y * second.Y;

    private static double Distance(
        CadPlanePoint first,
        CadPlanePoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Cross(
        double ax,
        double ay,
        double bx,
        double by) =>
        ax * by - ay * bx;

    private readonly record struct Corner(
        CadPlanePoint Intersection,
        CadPlanePoint FirstDirection,
        CadPlanePoint SecondDirection,
        OcctPoint3d FirstFarPoint,
        OcctPoint3d SecondFarPoint,
        double FirstAvailable,
        double SecondAvailable);
}
