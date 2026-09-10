using OcctNet;

namespace OCCAD;

internal static class CadPathChamferGeometry
{
    private const double Tolerance = 1e-7;

    internal static bool TryChamfer(
        CadPathEntity path,
        OcctPoint3d pickPoint,
        double firstDistance,
        double secondDistance,
        CadWorkPlane plane,
        out CadPathEntity replacement)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(plane);
        replacement = null!;

        var segments = path.Segments
            .Select(CadPathEntity.SnapshotSegment)
            .ToList();
        if (segments.Count < 2 ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(path, plane))
            return false;

        var vertex = FindVertex(
            segments,
            path.Closed,
            pickPoint);
        if (vertex < 0)
            return false;

        var previousIndex =
            vertex == 0
                ? segments.Count - 1
                : vertex - 1;
        var nextIndex = vertex;

        if (!path.Closed &&
            (previousIndex < 0 ||
             nextIndex >= segments.Count))
            return false;

        if (segments[previousIndex] is not CadLineEntity previous ||
            segments[nextIndex] is not CadLineEntity next)
            return false;

        var corner = previous.End;
        if (corner.DistanceTo(next.Start) > Tolerance)
            return false;

        if (!CadLineCornerGeometry.TryChamfer(
                previous,
                previous.Start,
                next,
                next.End,
                firstDistance,
                secondDistance,
                plane,
                out var parts) ||
            parts.Length != 3 ||
            parts[0] is not CadLineEntity previousTrimmed ||
            parts[1] is not CadLineEntity nextTrimmed ||
            parts[2] is not CadLineEntity chamfer)
            return false;

        var output = new List<CadEntity>(
            segments.Count + 1);

        if (path.Closed && vertex == 0)
        {
            output.Add(nextTrimmed);
            for (var index = 1;
                 index < segments.Count - 1;
                 index++)
            {
                output.Add(
                    CadPathEntity.SnapshotSegment(
                        segments[index]));
            }

            output.Add(previousTrimmed);
            output.Add(chamfer);
        }
        else
        {
            for (var index = 0;
                 index < segments.Count;
                 index++)
            {
                if (index == previousIndex)
                {
                    output.Add(previousTrimmed);
                    output.Add(chamfer);
                    continue;
                }

                if (index == nextIndex)
                {
                    output.Add(nextTrimmed);
                    continue;
                }

                output.Add(
                    CadPathEntity.SnapshotSegment(
                        segments[index]));
            }
        }

        try
        {
            replacement = path.CopyWithSegments(output);
            return true;
        }
        catch (ArgumentException)
        {
            replacement = null!;
            return false;
        }
    }

    private static int FindVertex(
        IReadOnlyList<CadEntity> segments,
        bool closed,
        OcctPoint3d pickPoint)
    {
        var start = closed ? 0 : 1;
        var end = closed
            ? segments.Count
            : segments.Count;

        var best = -1;
        var distance = double.MaxValue;
        for (var vertex = start; vertex < end; vertex++)
        {
            var point =
                vertex == 0
                    ? CadPathEntity.SegmentStart(
                        segments[0])
                    : CadPathEntity.SegmentStart(
                        segments[vertex]);
            var current =
                point.DistanceTo(pickPoint);
            if (current >= distance)
                continue;

            distance = current;
            best = vertex;
        }

        return best;
    }
}
