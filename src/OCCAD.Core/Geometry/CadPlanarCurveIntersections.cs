using OcctNet;

namespace OCCAD;

internal readonly record struct CadCurveIntersection(
    OcctPoint3d Point,
    double TargetParameter);

internal static class CadPlanarCurveIntersections
{
    private const double Tolerance = 1e-9;
    private const double PlaneTolerance = 1e-6;

    internal static IReadOnlyList<CadCurveIntersection> WithLine(
        OcctPoint3d start,
        OcctPoint3d end,
        CadEntity boundary,
        CadWorkPlane plane,
        bool targetSegment)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(plane);

        if (!boundary.Placement.IsIdentity)
        {
            return WithLine(
                start,
                end,
                boundary.CreateWorldGeometrySnapshot(),
                plane,
                targetSegment);
        }

        if (!IsOnPlane(start, plane) ||
            !IsOnPlane(end, plane) ||
            !IsBoundaryOnPlane(boundary, plane))
            return Array.Empty<CadCurveIntersection>();

        return boundary switch
        {
            CadLineEntity line =>
                LineLine(start, end, line.Start, line.End, plane, targetSegment),
            CadPolylineEntity polyline =>
                LinePolyline(start, end, polyline, plane, targetSegment),
            CadCircleEntity circle =>
                LineCircle(start, end, circle.Center, circle.Radius, plane, targetSegment),
            CadArcEntity arc =>
                LineCircle(start, end, arc.Center, arc.Radius, plane, targetSegment)
                    .Where(value => arc.TryParameterAt(value.Point, out _))
                    .ToArray(),
            CadPathEntity path =>
                Distinct(
                    path.Segments.SelectMany(
                        segment => WithLine(
                            start,
                            end,
                            segment.ToEntity(),
                            plane,
                            targetSegment))),
            _ => Array.Empty<CadCurveIntersection>()
        };
    }

    internal static IReadOnlyList<OcctPoint3d> WithCircle(
        OcctPoint3d center,
        double radius,
        CadEntity boundary,
        CadWorkPlane plane)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(plane);

        if (!boundary.Placement.IsIdentity)
        {
            return WithCircle(
                center,
                radius,
                boundary.CreateWorldGeometrySnapshot(),
                plane);
        }

        if (!IsOnPlane(center, plane) ||
            radius <= Tolerance ||
            !IsBoundaryOnPlane(boundary, plane))
            return Array.Empty<OcctPoint3d>();

        return boundary switch
        {
            CadLineEntity line =>
                LineCircle(line.Start, line.End, center, radius, plane, targetSegment: true)
                    .Select(static value => value.Point)
                    .ToArray(),
            CadPolylineEntity polyline =>
                CirclePolyline(center, radius, polyline, plane),
            CadCircleEntity circle =>
                CircleCircle(center, radius, circle.Center, circle.Radius, plane),
            CadArcEntity arc =>
                CircleCircle(center, radius, arc.Center, arc.Radius, plane)
                    .Where(point => arc.TryParameterAt(point, out _))
                    .ToArray(),
            CadPathEntity path =>
                DistinctPoints(
                    path.Segments.SelectMany(
                        segment => WithCircle(
                            center,
                            radius,
                            segment.ToEntity(),
                            plane))),
            _ => Array.Empty<OcctPoint3d>()
        };
    }

    internal static bool IsBoundaryOnPlane(
        CadEntity entity,
        CadWorkPlane plane)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(plane);

        if (!entity.Placement.IsIdentity)
        {
            return IsBoundaryOnPlane(
                entity.CreateWorldGeometrySnapshot(),
                plane);
        }

        return entity switch
        {
            CadLineEntity line =>
                IsOnPlane(line.Start, plane) &&
                IsOnPlane(line.End, plane),
            CadPolylineEntity polyline =>
                polyline.Points.All(point => IsOnPlane(point, plane)),
            CadCircleEntity circle =>
                IsCircleOnPlane(circle.Center, circle.Normal, plane),
            CadArcEntity arc =>
                IsCircleOnPlane(arc.Center, arc.Normal, plane),
            CadPathEntity path =>
                path.Segments.All(
                    segment => IsBoundaryOnPlane(
                        segment.ToEntity(),
                        plane)),
            _ => false
        };
    }

    internal static bool IsOnPlane(
        OcctPoint3d point,
        CadWorkPlane plane)
    {
        var axial = CadTransformMath.Dot(
            CadTransformMath.Between(plane.Origin, point),
            plane.Normal);
        return Math.Abs(axial) <= PlaneTolerance;
    }

    private static bool IsCircleOnPlane(
        OcctPoint3d center,
        OcctVector3d normal,
        CadWorkPlane plane)
    {
        if (!IsOnPlane(center, plane))
            return false;

        var dot = Math.Abs(
            CadTransformMath.Dot(
                normal.Normalized(),
                plane.Normal));
        return Math.Abs(1.0 - dot) <= PlaneTolerance;
    }

    private static IReadOnlyList<CadCurveIntersection> LinePolyline(
        OcctPoint3d start,
        OcctPoint3d end,
        CadPolylineEntity polyline,
        CadWorkPlane plane,
        bool targetSegment)
    {
        var result = new List<CadCurveIntersection>();
        var count = polyline.Closed
            ? polyline.Points.Count
            : polyline.Points.Count - 1;

        for (var index = 0; index < count; index++)
        {
            var next = (index + 1) % polyline.Points.Count;
            result.AddRange(
                LineLine(
                    start,
                    end,
                    polyline.Points[index],
                    polyline.Points[next],
                    plane,
                    targetSegment));
        }

        return Distinct(result);
    }

    private static IReadOnlyList<OcctPoint3d> CirclePolyline(
        OcctPoint3d center,
        double radius,
        CadPolylineEntity polyline,
        CadWorkPlane plane)
    {
        var result = new List<OcctPoint3d>();
        var count = polyline.Closed
            ? polyline.Points.Count
            : polyline.Points.Count - 1;

        for (var index = 0; index < count; index++)
        {
            var next = (index + 1) % polyline.Points.Count;
            result.AddRange(
                LineCircle(
                    polyline.Points[index],
                    polyline.Points[next],
                    center,
                    radius,
                    plane,
                    targetSegment: true)
                .Select(static value => value.Point));
        }

        return DistinctPoints(result);
    }

    private static IReadOnlyList<CadCurveIntersection> LineLine(
        OcctPoint3d start,
        OcctPoint3d end,
        OcctPoint3d boundaryStart,
        OcctPoint3d boundaryEnd,
        CadWorkPlane plane,
        bool targetSegment)
    {
        var a = plane.WorldToLocal(start);
        var b = plane.WorldToLocal(end);
        var c = plane.WorldToLocal(boundaryStart);
        var d = plane.WorldToLocal(boundaryEnd);

        var rx = b.X - a.X;
        var ry = b.Y - a.Y;
        var sx = d.X - c.X;
        var sy = d.Y - c.Y;
        var denominator = Cross(rx, ry, sx, sy);
        if (Math.Abs(denominator) <= Tolerance)
            return Array.Empty<CadCurveIntersection>();

        var qx = c.X - a.X;
        var qy = c.Y - a.Y;
        var t = Cross(qx, qy, sx, sy) / denominator;
        var u = Cross(qx, qy, rx, ry) / denominator;

        if ((targetSegment && (t < -Tolerance || t > 1.0 + Tolerance)) ||
            u < -Tolerance ||
            u > 1.0 + Tolerance)
            return Array.Empty<CadCurveIntersection>();

        return
        [
            new CadCurveIntersection(
                plane.LocalToWorld(
                    new CadPlanePoint(
                        a.X + rx * t,
                        a.Y + ry * t)),
                t)
        ];
    }

    private static IReadOnlyList<CadCurveIntersection> LineCircle(
        OcctPoint3d start,
        OcctPoint3d end,
        OcctPoint3d center,
        double radius,
        CadWorkPlane plane,
        bool targetSegment)
    {
        var a = plane.WorldToLocal(start);
        var b = plane.WorldToLocal(end);
        var c = plane.WorldToLocal(center);

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var fx = a.X - c.X;
        var fy = a.Y - c.Y;
        var aa = dx * dx + dy * dy;
        if (aa <= Tolerance * Tolerance)
            return Array.Empty<CadCurveIntersection>();

        var bb = 2.0 * (fx * dx + fy * dy);
        var cc = fx * fx + fy * fy - radius * radius;
        var discriminant = bb * bb - 4.0 * aa * cc;
        if (discriminant < -Tolerance)
            return Array.Empty<CadCurveIntersection>();

        var result = new List<CadCurveIntersection>(2);
        Add(-bb - Math.Sqrt(Math.Max(0.0, discriminant)));
        if (discriminant > Tolerance)
            Add(-bb + Math.Sqrt(discriminant));
        return result;

        void Add(double numerator)
        {
            var t = numerator / (2.0 * aa);
            if (targetSegment &&
                (t < -Tolerance || t > 1.0 + Tolerance))
                return;

            result.Add(
                new CadCurveIntersection(
                    plane.LocalToWorld(
                        new CadPlanePoint(
                            a.X + dx * t,
                            a.Y + dy * t)),
                    t));
        }
    }

    private static IReadOnlyList<OcctPoint3d> CircleCircle(
        OcctPoint3d firstCenter,
        double firstRadius,
        OcctPoint3d secondCenter,
        double secondRadius,
        CadWorkPlane plane)
    {
        var a = plane.WorldToLocal(firstCenter);
        var b = plane.WorldToLocal(secondCenter);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance <= Tolerance ||
            distance > firstRadius + secondRadius + Tolerance ||
            distance < Math.Abs(firstRadius - secondRadius) - Tolerance)
            return Array.Empty<OcctPoint3d>();

        var along =
            (firstRadius * firstRadius -
             secondRadius * secondRadius +
             distance * distance) /
            (2.0 * distance);
        var heightSquared =
            firstRadius * firstRadius -
            along * along;
        if (heightSquared < -Tolerance)
            return Array.Empty<OcctPoint3d>();

        var ux = dx / distance;
        var uy = dy / distance;
        var baseX = a.X + along * ux;
        var baseY = a.Y + along * uy;

        if (Math.Abs(heightSquared) <= Tolerance)
        {
            return
            [
                plane.LocalToWorld(
                    new CadPlanePoint(baseX, baseY))
            ];
        }

        var height = Math.Sqrt(Math.Max(0.0, heightSquared));
        var px = -uy * height;
        var py = ux * height;
        return
        [
            plane.LocalToWorld(
                new CadPlanePoint(baseX + px, baseY + py)),
            plane.LocalToWorld(
                new CadPlanePoint(baseX - px, baseY - py))
        ];
    }

    private static IReadOnlyList<CadCurveIntersection> Distinct(
        IEnumerable<CadCurveIntersection> values) =>
        values
            .GroupBy(value => Math.Round(value.TargetParameter, 9))
            .Select(static group => group.First())
            .ToArray();

    private static IReadOnlyList<OcctPoint3d> DistinctPoints(
        IEnumerable<OcctPoint3d> values)
    {
        var result = new List<OcctPoint3d>();
        foreach (var point in values)
        {
            if (result.Any(existing =>
                    existing.DistanceTo(point) <= 1e-7))
                continue;
            result.Add(point);
        }
        return result;
    }

    private static double Cross(
        double ax,
        double ay,
        double bx,
        double by) =>
        ax * by - ay * bx;
}
