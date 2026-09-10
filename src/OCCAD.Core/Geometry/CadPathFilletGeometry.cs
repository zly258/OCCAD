using OcctNet;

namespace OCCAD;

internal static class CadPathFilletGeometry
{
    private const double Tolerance = 1e-7;

    internal static bool TryFillet(
        CadEntity source,
        OcctPoint3d pickPoint,
        double radius,
        CadWorkPlane plane,
        out CadPathEntity replacement)
    {
        replacement = null!;

        return source switch
        {
            CadPolylineEntity polyline =>
                TryFilletPolyline(
                    polyline,
                    pickPoint,
                    radius,
                    plane,
                    out replacement),

            CadPathEntity path =>
                TryFilletPath(
                    path,
                    pickPoint,
                    radius,
                    plane,
                    out replacement),

            _ => false
        };
    }

    private static bool TryFilletPolyline(
        CadPolylineEntity polyline,
        OcctPoint3d pickPoint,
        double radius,
        CadWorkPlane plane,
        out CadPathEntity replacement)
    {
        replacement = null!;

        var points = polyline.Points;
        var vertex = FindVertex(
            points,
            polyline.Closed,
            pickPoint);
        if (vertex < 0)
            return false;

        var segments = new List<CadEntity>(
            polyline.Closed
                ? points.Count
                : points.Count - 1);

        var segmentCount =
            polyline.Closed
                ? points.Count
                : points.Count - 1;

        for (var index = 0; index < segmentCount; index++)
        {
            segments.Add(
                new CadLineEntity(
                    points[index],
                    points[(index + 1) % points.Count]));
        }

        if (!TryReplaceCorner(
                segments,
                polyline.Closed,
                vertex,
                radius,
                plane,
                out var result))
            return false;

        replacement = polyline.CreatePath(result);
        return true;
    }

    private static bool TryFilletPath(
        CadPathEntity path,
        OcctPoint3d pickPoint,
        double radius,
        CadWorkPlane plane,
        out CadPathEntity replacement)
    {
        replacement = null!;

        var segments = path.Segments
            .Select(CadPathEntity.SnapshotSegment)
            .ToList();

        var vertex = FindPathVertex(
            segments,
            path.Closed,
            pickPoint);
        if (vertex < 0)
            return false;

        if (!TryReplaceCorner(
                segments,
                path.Closed,
                vertex,
                radius,
                plane,
                out var result))
            return false;

        replacement = path.CopyWithSegments(result);
        return true;
    }

    private static bool TryReplaceCorner(
        IReadOnlyList<CadEntity> source,
        bool closed,
        int vertex,
        double radius,
        CadWorkPlane plane,
        out IReadOnlyList<CadEntity> result)
    {
        result = Array.Empty<CadEntity>();

        var previousIndex =
            vertex == 0
                ? source.Count - 1
                : vertex - 1;
        var nextIndex =
            vertex == source.Count
                ? 0
                : vertex;

        if (!closed &&
            (previousIndex < 0 ||
             nextIndex >= source.Count))
            return false;

        var previous = source[previousIndex];
        var next = source[nextIndex];
        var corner =
            CadPathEntity.SegmentEnd(previous);
        if (corner.DistanceTo(
                CadPathEntity.SegmentStart(next)) >
            Tolerance)
            return false;

        if (!TryBuildFilletParts(
                previous,
                next,
                radius,
                plane,
                out var previousTrimmed,
                out var filletArc,
                out var nextTrimmed))
            return false;

        var output = new List<CadEntity>(
            source.Count + 1);

        if (closed && vertex == 0)
        {
            output.Add(nextTrimmed);
            for (var index = 1;
                 index < source.Count - 1;
                 index++)
            {
                output.Add(
                    CadPathEntity.SnapshotSegment(
                        source[index]));
            }

            output.Add(previousTrimmed);
            output.Add(filletArc);
        }
        else
        {
            for (var index = 0;
                 index < source.Count;
                 index++)
            {
                if (index == previousIndex)
                {
                    output.Add(previousTrimmed);
                    output.Add(filletArc);
                    continue;
                }

                if (index == nextIndex)
                {
                    output.Add(nextTrimmed);
                    continue;
                }

                output.Add(
                    CadPathEntity.SnapshotSegment(
                        source[index]));
            }
        }

        try
        {
            _ = new CadPathEntity(output);
        }
        catch (ArgumentException)
        {
            return false;
        }

        result = output;
        return true;
    }

    private static bool TryBuildFilletParts(
        CadEntity previous,
        CadEntity next,
        double radius,
        CadWorkPlane plane,
        out CadEntity previousTrimmed,
        out CadArcEntity fillet,
        out CadEntity nextTrimmed)
    {
        previousTrimmed = null!;
        nextTrimmed = null!;
        fillet = null!;

        CadEntity[] parts;
        switch (previous, next)
        {
            case (CadLineEntity first, CadLineEntity second):
                if (!CadLineCornerGeometry.TryFillet(
                        first,
                        first.Start,
                        second,
                        second.End,
                        radius,
                        plane,
                        out parts))
                    return false;
                break;

            case (CadLineEntity line, CadArcEntity arc):
                if (!CadLineArcFilletGeometry.TryFillet(
                        line,
                        line.Start,
                        arc,
                        arc.End,
                        radius,
                        plane,
                        out parts))
                    return false;
                break;

            case (CadArcEntity arc, CadLineEntity line):
                if (!CadLineArcFilletGeometry.TryFillet(
                        line,
                        line.End,
                        arc,
                        arc.Start,
                        radius,
                        plane,
                        out parts) ||
                    parts.Length != 3 ||
                    parts[0] is not CadLineEntity lineTrimmed ||
                    parts[1] is not CadArcEntity arcTrimmed ||
                    parts[2] is not CadArcEntity reverseFillet)
                    return false;

                previousTrimmed = arcTrimmed;
                nextTrimmed = lineTrimmed;
                fillet = reverseFillet.ReversedCopy();
                return true;

            case (CadArcEntity first, CadArcEntity second):
                if (!CadArcArcFilletGeometry.TryFillet(
                        first,
                        first.Start,
                        second,
                        second.End,
                        radius,
                        plane,
                        out parts))
                    return false;
                break;

            default:
                return false;
        }

        if (parts.Length != 3 ||
            parts[2] is not CadArcEntity filletArc)
            return false;

        previousTrimmed = parts[0];
        nextTrimmed = parts[1];
        fillet = filletArc;
        return true;
    }

    private static int FindVertex(
        IReadOnlyList<OcctPoint3d> points,
        bool closed,
        OcctPoint3d pickPoint)
    {
        var start = closed ? 0 : 1;
        var end = closed
            ? points.Count
            : points.Count - 1;

        var best = -1;
        var distance = double.MaxValue;

        for (var index = start; index < end; index++)
        {
            var current =
                points[index].DistanceTo(pickPoint);
            if (current >= distance)
                continue;

            distance = current;
            best = index;
        }

        return best;
    }

    private static int FindPathVertex(
        IReadOnlyList<CadEntity> segments,
        bool closed,
        OcctPoint3d pickPoint)
    {
        if (segments.Count < 2)
            return -1;

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
                    ? CadPathEntity.SegmentStart(segments[0])
                    : CadPathEntity.SegmentStart(segments[vertex]);

            var current = point.DistanceTo(pickPoint);
            if (current >= distance)
                continue;

            distance = current;
            best = vertex;
        }

        return best;
    }
}
