using OcctNet;

namespace OCCAD;

internal readonly record struct CadPathPosition(
    int SegmentIndex,
    double SegmentParameter)
{
    public double Value => SegmentIndex + SegmentParameter;
}

internal static class CadPathCurveGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryClosestPosition(
        CadPathEntity path,
        OcctPoint3d point,
        CadWorkPlane plane,
        out CadPathPosition position)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(plane);

        position = default;
        if (path.Segments.Count == 0 ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(
                path,
                plane))
            return false;

        var bestDistance = double.MaxValue;
        var found = false;
        for (var index = 0;
             index < path.Segments.Count;
             index++)
        {
            var segment = path.Segments[index];
            double parameter;
            OcctPoint3d closest;

            switch (segment)
            {
                case CadLineEntity line:
                    parameter = LineParameter(
                        line,
                        point,
                        plane);
                    closest =
                        line.Start +
                        CadTransformMath.Between(
                            line.Start,
                            line.End) *
                        parameter;
                    break;

                case CadArcEntity arc:
                    if (!arc.TryClosestParameter(
                            point,
                            out parameter))
                        continue;
                    closest =
                        arc.PointAtParameter(
                            parameter);
                    break;

                default:
                    continue;
            }

            var distance =
                closest.DistanceTo(point);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            position = new CadPathPosition(
                index,
                parameter);
            found = true;
        }

        return found;
    }

    internal static OcctPoint3d PointAt(
        CadPathEntity path,
        CadPathPosition position)
    {
        if ((uint)position.SegmentIndex >=
            (uint)path.Segments.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position));
        }

        var parameter = Math.Clamp(
            position.SegmentParameter,
            0.0,
            1.0);

        return path.Segments[position.SegmentIndex] switch
        {
            CadLineEntity line =>
                line.Start +
                CadTransformMath.Between(
                    line.Start,
                    line.End) *
                parameter,
            CadArcEntity arc =>
                arc.PointAtParameter(parameter),
            _ => throw new InvalidOperationException(
                "Path contains an unsupported segment.")
        };
    }

    internal static IReadOnlyList<CadEntity> BuildForwardSegments(
        CadPathEntity path,
        CadPathPosition start,
        CadPathPosition end,
        bool allowWrap)
    {
        ArgumentNullException.ThrowIfNull(path);
        var count = path.Segments.Count;
        if (count <= 0)
            return Array.Empty<CadEntity>();

        var startValue = NormalizeValue(
            start,
            count);
        var endValue = NormalizeValue(
            end,
            count);

        if (allowWrap &&
            endValue <= startValue + Tolerance)
            endValue += count;

        if (endValue <= startValue + Tolerance)
            return Array.Empty<CadEntity>();

        var output = new List<CadEntity>();
        var current = startValue;

        while (current < endValue - Tolerance)
        {
            var absoluteIndex =
                (int)Math.Floor(current);
            var segmentIndex =
                absoluteIndex % count;
            if (segmentIndex < 0)
                segmentIndex += count;

            var segmentStart =
                current - absoluteIndex;
            if (segmentStart < Tolerance)
                segmentStart = 0.0;

            var boundary =
                absoluteIndex + 1.0;
            var next =
                Math.Min(
                    endValue,
                    boundary);
            var segmentEnd =
                next - absoluteIndex;
            if (segmentEnd > 1.0 - Tolerance)
                segmentEnd = 1.0;

            if (segmentEnd -
                segmentStart >
                Tolerance)
            {
                output.Add(
                    CopySegmentRange(
                        path.Segments[segmentIndex],
                        segmentStart,
                        segmentEnd));
            }

            current = next;
        }

        return output;
    }

    private static CadEntity CopySegmentRange(
        CadEntity segment,
        double start,
        double end)
    {
        if (start <= Tolerance &&
            end >= 1.0 - Tolerance)
        {
            return CadPathEntity.SnapshotSegment(
                segment);
        }

        return segment switch
        {
            CadLineEntity line =>
                line.CreateLine(
                    PointAt(line, start),
                    PointAt(line, end)),
            CadArcEntity arc =>
                arc.CopyWithParameterRange(
                    start,
                    end),
            _ => throw new InvalidOperationException(
                "Path contains an unsupported segment.")
        };
    }

    private static OcctPoint3d PointAt(
        CadLineEntity line,
        double parameter) =>
        line.Start +
        CadTransformMath.Between(
            line.Start,
            line.End) *
        parameter;

    private static double NormalizeValue(
        CadPathPosition position,
        int segmentCount)
    {
        var index =
            position.SegmentIndex %
            segmentCount;
        if (index < 0)
            index += segmentCount;

        var parameter = Math.Clamp(
            position.SegmentParameter,
            0.0,
            1.0);
        if (parameter <= Tolerance)
            parameter = 0.0;
        else if (parameter >= 1.0 - Tolerance)
            parameter = 1.0;

        return index + parameter;
    }

    private static double LineParameter(
        CadLineEntity line,
        OcctPoint3d point,
        CadWorkPlane plane)
    {
        var start =
            plane.WorldToLocal(line.Start);
        var end =
            plane.WorldToLocal(line.End);
        var value =
            plane.WorldToLocal(point);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared =
            dx * dx + dy * dy;
        if (lengthSquared <=
            Tolerance * Tolerance)
            return 0.0;

        return Math.Clamp(
            ((value.X - start.X) * dx +
             (value.Y - start.Y) * dy) /
            lengthSquared,
            0.0,
            1.0);
    }
}
