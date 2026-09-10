using OcctNet;

namespace OCCAD;

internal readonly record struct CadPolylinePathPosition(
    int SegmentIndex,
    double SegmentParameter)
{
    public double Value => SegmentIndex + SegmentParameter;
}

internal static class CadPolylinePathGeometry
{
    private const double Tolerance = 1e-9;

    internal static int SegmentCount(
        CadPolylineEntity polyline) =>
        polyline.Closed
            ? polyline.Points.Count
            : polyline.Points.Count - 1;

    internal static bool TryClosestPosition(
        CadPolylineEntity polyline,
        OcctPoint3d point,
        CadWorkPlane plane,
        out CadPolylinePathPosition position)
    {
        ArgumentNullException.ThrowIfNull(polyline);
        ArgumentNullException.ThrowIfNull(plane);

        position = default;
        var segmentCount = SegmentCount(polyline);
        if (segmentCount <= 0)
            return false;

        var localPoint = plane.WorldToLocal(point);
        var bestDistance = double.MaxValue;
        var found = false;

        for (var index = 0; index < segmentCount; index++)
        {
            var next = (index + 1) % polyline.Points.Count;
            var start = plane.WorldToLocal(polyline.Points[index]);
            var end = plane.WorldToLocal(polyline.Points[next]);
            var parameter = SegmentParameter(
                start,
                end,
                localPoint);
            var closest = new CadPlanePoint(
                start.X + (end.X - start.X) * parameter,
                start.Y + (end.Y - start.Y) * parameter);
            var dx = localPoint.X - closest.X;
            var dy = localPoint.Y - closest.Y;
            var distance = dx * dx + dy * dy;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            position = new CadPolylinePathPosition(
                index,
                parameter);
            found = true;
        }

        return found;
    }

    internal static OcctPoint3d PointAt(
        CadPolylineEntity polyline,
        CadPolylinePathPosition position)
    {
        var segmentCount = SegmentCount(polyline);
        if ((uint)position.SegmentIndex >= (uint)segmentCount)
            throw new ArgumentOutOfRangeException(nameof(position));

        var next =
            (position.SegmentIndex + 1) %
            polyline.Points.Count;
        var start = polyline.Points[position.SegmentIndex];
        var end = polyline.Points[next];
        return start +
               CadTransformMath.Between(start, end) *
               position.SegmentParameter;
    }

    internal static IReadOnlyList<OcctPoint3d> BuildForwardPath(
        CadPolylineEntity polyline,
        CadPolylinePathPosition start,
        CadPolylinePathPosition end,
        bool allowWrap)
    {
        var segmentCount = SegmentCount(polyline);
        if (segmentCount <= 0)
            return Array.Empty<OcctPoint3d>();

        var startValue = start.Value;
        var endValue = end.Value;
        if (allowWrap && endValue <= startValue + Tolerance)
            endValue += segmentCount;

        if (endValue <= startValue + Tolerance)
            return Array.Empty<OcctPoint3d>();

        var result = new List<OcctPoint3d>
        {
            PointAt(polyline, start)
        };

        var firstVertex =
            (int)Math.Floor(startValue + Tolerance) + 1;
        var lastVertex =
            (int)Math.Floor(endValue - Tolerance);

        for (var vertex = firstVertex;
             vertex <= lastVertex;
             vertex++)
        {
            var pointIndex =
                vertex % polyline.Points.Count;
            AppendDistinct(
                result,
                polyline.Points[pointIndex]);
        }

        AppendDistinct(
            result,
            PointAt(
                polyline,
                NormalizePosition(
                    end,
                    segmentCount)));
        return result;
    }

    internal static CadPolylinePathPosition NormalizePosition(
        CadPolylinePathPosition position,
        int segmentCount)
    {
        if (segmentCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(segmentCount));

        var index = position.SegmentIndex % segmentCount;
        if (index < 0)
            index += segmentCount;

        return new CadPolylinePathPosition(
            index,
            Math.Clamp(
                position.SegmentParameter,
                0.0,
                1.0));
    }

    private static double SegmentParameter(
        CadPlanePoint start,
        CadPlanePoint end,
        CadPlanePoint point)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= Tolerance * Tolerance)
            return 0.0;

        return Math.Clamp(
            ((point.X - start.X) * dx +
             (point.Y - start.Y) * dy) /
            lengthSquared,
            0.0,
            1.0);
    }

    private static void AppendDistinct(
        List<OcctPoint3d> points,
        OcctPoint3d point)
    {
        if (points.Count > 0 &&
            points[^1].DistanceTo(point) <= 1e-8)
            return;
        points.Add(point);
    }
}
