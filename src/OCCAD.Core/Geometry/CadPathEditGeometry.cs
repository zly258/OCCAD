using OcctNet;

namespace OCCAD;

internal static class CadPathEditGeometry
{
    private const double Tolerance = 1e-7;

    internal static bool TryTrim(
        CadPathEntity path,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(boundaries);
        ArgumentNullException.ThrowIfNull(plane);

        replacements = [];
        if (!CadPathCurveGeometry.TryClosestPosition(
                path,
                hitPoint,
                plane,
                out var position))
            return false;

        var segmentIndex =
            position.SegmentIndex;
        var source =
            path.Segments[segmentIndex];

        CadEntity[] trimmed;
        switch (source)
        {
            case CadLineEntity line:
                if (!CadLineEditGeometry.TryTrim(
                        line,
                        boundaries,
                        hitPoint,
                        plane,
                        out trimmed))
                    return false;
                break;

            case CadArcEntity arc:
                if (!CadArcEditGeometry.TryTrim(
                        arc,
                        boundaries,
                        hitPoint,
                        plane,
                        out trimmed))
                    return false;
                break;

            default:
                return false;
        }

        CadEntity? left = null;
        CadEntity? right = null;
        var sourceStart =
            CadPathEntity.SegmentStart(source);
        var sourceEnd =
            CadPathEntity.SegmentEnd(source);

        foreach (var item in trimmed)
        {
            if (CadPathEntity
                    .SegmentStart(item)
                    .DistanceTo(sourceStart) <=
                Tolerance)
            {
                left = item;
            }

            if (CadPathEntity
                    .SegmentEnd(item)
                    .DistanceTo(sourceEnd) <=
                Tolerance)
            {
                right = item;
            }
        }

        return path.Closed
            ? TryBuildClosed(
                path,
                segmentIndex,
                left,
                right,
                out replacements)
            : TryBuildOpen(
                path,
                segmentIndex,
                left,
                right,
                out replacements);
    }

    private static bool TryBuildOpen(
        CadPathEntity path,
        int segmentIndex,
        CadEntity? left,
        CadEntity? right,
        out CadEntity[] replacements)
    {
        var result =
            new List<CadEntity>(2);

        var leftSegments =
            new List<CadEntity>();
        for (var index = 0;
             index < segmentIndex;
             index++)
        {
            leftSegments.Add(
                CadPathEntity.SnapshotSegment(
                    path.Segments[index]));
        }
        if (left is not null)
            leftSegments.Add(left);

        if (leftSegments.Count > 0)
            result.Add(
                path.CopyWithSegments(
                    leftSegments));

        var rightSegments =
            new List<CadEntity>();
        if (right is not null)
            rightSegments.Add(right);
        for (var index = segmentIndex + 1;
             index < path.Segments.Count;
             index++)
        {
            rightSegments.Add(
                CadPathEntity.SnapshotSegment(
                    path.Segments[index]));
        }

        if (rightSegments.Count > 0)
            result.Add(
                path.CopyWithSegments(
                    rightSegments));

        replacements = result.ToArray();
        return replacements.Length > 0;
    }

    private static bool TryBuildClosed(
        CadPathEntity path,
        int segmentIndex,
        CadEntity? left,
        CadEntity? right,
        out CadEntity[] replacements)
    {
        replacements = [];

        var output =
            new List<CadEntity>(
                path.Segments.Count + 1);

        if (right is not null)
            output.Add(right);

        for (var offset = 1;
             offset < path.Segments.Count;
             offset++)
        {
            var index =
                (segmentIndex + offset) %
                path.Segments.Count;
            output.Add(
                CadPathEntity.SnapshotSegment(
                    path.Segments[index]));
        }

        if (left is not null)
            output.Add(left);

        if (output.Count == 0)
            return false;

        try
        {
            var replacement =
                path.CopyWithSegments(output);
            if (replacement.Closed)
                return false;

            replacements = [replacement];
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
