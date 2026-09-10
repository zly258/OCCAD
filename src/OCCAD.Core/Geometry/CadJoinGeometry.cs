using OcctNet;

namespace OCCAD;

internal static class CadJoinGeometry
{
    private const double JoinTolerance = 1e-7;
    private const double AngularTolerance = 1e-6;

    internal static bool TryJoin(
        IReadOnlyList<CadEntity> entities,
        out CadEntity result)
    {
        ArgumentNullException.ThrowIfNull(entities);
        result = null!;

        if (entities.Count < 2)
            return false;

        if (entities.All(static entity => entity is CadArcEntity))
            return TryJoinArcs(
                entities.Cast<CadArcEntity>().ToArray(),
                out result);

        if (entities.All(
                static entity =>
                    entity is CadLineEntity or
                    CadPolylineEntity { Closed: false }))
            return TryJoinLinear(entities, out result);

        return TryJoinMixed(entities, out result);
    }

    private static bool TryJoinMixed(
        IReadOnlyList<CadEntity> entities,
        out CadEntity result)
    {
        result = null!;

        if (entities.Any(
                static entity =>
                    entity is not CadLineEntity and
                    not CadArcEntity and
                    not CadPolylineEntity { Closed: false }))
            return false;

        var fragments = entities
            .Select(entity =>
                new Fragment(
                    entity,
                    [EntityStart(entity), EntityEnd(entity)]))
            .ToArray();

        if (!TryOrderFragments(
                fragments,
                out var ordered))
            return false;

        var segments = new List<CadEntity>();
        foreach (var item in ordered)
        {
            AppendEntitySegments(
                item.Fragment.Source,
                item.Reverse,
                segments);
        }

        if (segments.Count == 0)
            return false;

        result = ordered[0].Fragment.Source switch
        {
            CadLineEntity line => line.CreatePath(segments),
            CadArcEntity arc => arc.CreatePath(segments),
            CadPolylineEntity polyline => polyline.CreatePath(segments),
            _ => throw new InvalidOperationException()
        };
        return true;
    }

    private static OcctPoint3d EntityStart(CadEntity entity) =>
        entity switch
        {
            CadLineEntity line => line.Start,
            CadArcEntity arc => arc.Start,
            CadPolylineEntity { Closed: false } polyline => polyline.Points[0],
            _ => throw new ArgumentException("Unsupported join entity.", nameof(entity))
        };

    private static OcctPoint3d EntityEnd(CadEntity entity) =>
        entity switch
        {
            CadLineEntity line => line.End,
            CadArcEntity arc => arc.End,
            CadPolylineEntity { Closed: false } polyline => polyline.Points[^1],
            _ => throw new ArgumentException("Unsupported join entity.", nameof(entity))
        };

    private static void AppendEntitySegments(
        CadEntity entity,
        bool reverse,
        List<CadEntity> target)
    {
        switch (entity)
        {
            case CadLineEntity line:
                target.Add(
                    reverse
                        ? line.CreateLine(line.End, line.Start)
                        : line.Duplicate());
                return;

            case CadArcEntity arc:
                target.Add(
                    reverse
                        ? arc.ReversedCopy()
                        : arc.Duplicate());
                return;

            case CadPolylineEntity { Closed: false } polyline:
            {
                if (!reverse)
                {
                    for (var index = 0; index + 1 < polyline.Points.Count; index++)
                    {
                        target.Add(
                            new CadLineEntity(
                                polyline.Points[index],
                                polyline.Points[index + 1]));
                    }
                }
                else
                {
                    for (var index = polyline.Points.Count - 1; index > 0; index--)
                    {
                        target.Add(
                            new CadLineEntity(
                                polyline.Points[index],
                                polyline.Points[index - 1]));
                    }
                }

                return;
            }

            default:
                throw new InvalidOperationException();
        }
    }

    private static bool TryJoinLinear(
        IReadOnlyList<CadEntity> entities,
        out CadEntity result)
    {
        result = null!;
        var fragments = new List<Fragment>(entities.Count);
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case CadLineEntity line:
                    fragments.Add(
                        new Fragment(
                            entity,
                            [line.Start, line.End]));
                    break;

                case CadPolylineEntity { Closed: false } polyline:
                    fragments.Add(
                        new Fragment(
                            entity,
                            polyline.Points.ToArray()));
                    break;

                default:
                    return false;
            }
        }

        if (!TryOrderFragments(
                fragments,
                out var ordered))
            return false;

        var points = new List<OcctPoint3d>();
        foreach (var item in ordered)
            AppendFragment(
                item.Fragment.Points,
                item.Reverse,
                points);

        if (points.Count < 2)
            return false;

        result = fragments[0].Source switch
        {
            CadLineEntity sourceLine =>
                sourceLine.CreatePolyline(points),
            CadPolylineEntity sourcePolyline =>
                sourcePolyline.CopyWithPoints(
                    points,
                    closed: false),
            _ => throw new InvalidOperationException()
        };
        return true;
    }

    private static bool TryJoinArcs(
        IReadOnlyList<CadArcEntity> arcs,
        out CadEntity result)
    {
        result = null!;
        var first = arcs[0];
        if (arcs.Any(arc =>
                !SameCircle(first, arc)))
            return false;

        var fragments = arcs
            .Select(static arc =>
                new Fragment(
                    arc,
                    [arc.Start, arc.End]))
            .ToArray();

        if (!TryOrderFragments(
                fragments,
                out var ordered))
            return false;

        var firstItem = ordered[0];
        var firstArc = (CadArcEntity)firstItem.Fragment.Source;
        var startAngle = firstItem.Reverse
            ? firstArc.StartAngleDegrees + firstArc.SweepAngleDegrees
            : firstArc.StartAngleDegrees;
        var firstSweep = firstItem.Reverse
            ? -firstArc.SweepAngleDegrees
            : firstArc.SweepAngleDegrees;

        var direction = Math.Sign(firstSweep);
        if (direction == 0)
            return false;

        var totalSweep = 0.0;
        foreach (var item in ordered)
        {
            var arc = (CadArcEntity)item.Fragment.Source;
            var sweep = item.Reverse
                ? -arc.SweepAngleDegrees
                : arc.SweepAngleDegrees;

            if (Math.Sign(sweep) != direction)
                return false;

            totalSweep += sweep;
        }

        if (Math.Abs(totalSweep) <= AngularTolerance ||
            Math.Abs(totalSweep) >= 360.0 - AngularTolerance)
            return false;

        result = firstArc.CopyWithAngles(
            startAngle,
            totalSweep);
        return true;
    }

    private static bool SameCircle(
        CadArcEntity first,
        CadArcEntity second)
    {
        if (first.Center.DistanceTo(second.Center) > JoinTolerance ||
            Math.Abs(first.Radius - second.Radius) > JoinTolerance)
            return false;

        var normalDot =
            CadTransformMath.Dot(
                first.Normal.Normalized(),
                second.Normal.Normalized());
        return Math.Abs(1.0 - normalDot) <= AngularTolerance;
    }

    private static bool TryOrderFragments(
        IReadOnlyList<Fragment> fragments,
        out IReadOnlyList<OrderedFragment> ordered)
    {
        ordered = Array.Empty<OrderedFragment>();
        var startFragmentIndex = -1;
        var reverseStart = false;

        for (var index = 0; index < fragments.Count; index++)
        {
            var fragment = fragments[index];
            var startMatches = CountEndpointMatches(
                fragments,
                index,
                fragment.Points[0]);
            var endMatches = CountEndpointMatches(
                fragments,
                index,
                fragment.Points[^1]);

            if (startMatches > 1 || endMatches > 1)
                return false;

            if (startMatches == 0 && startFragmentIndex < 0)
            {
                startFragmentIndex = index;
                reverseStart = false;
            }

            if (endMatches == 0 && startFragmentIndex < 0)
            {
                startFragmentIndex = index;
                reverseStart = true;
            }
        }

        if (startFragmentIndex < 0)
            return false;

        var used = new bool[fragments.Count];
        var result = new List<OrderedFragment>(fragments.Count);
        var currentIndex = startFragmentIndex;
        var currentReverse = reverseStart;

        while (true)
        {
            result.Add(
                new OrderedFragment(
                    fragments[currentIndex],
                    currentReverse));
            used[currentIndex] = true;

            if (result.Count == fragments.Count)
                break;

            var currentEnd = currentReverse
                ? fragments[currentIndex].Points[0]
                : fragments[currentIndex].Points[^1];

            var matchIndex = -1;
            var reverse = false;

            for (var index = 0; index < fragments.Count; index++)
            {
                if (used[index])
                    continue;

                var fragment = fragments[index];
                var matchesStart =
                    currentEnd.DistanceTo(fragment.Points[0]) <=
                    JoinTolerance;
                var matchesEnd =
                    currentEnd.DistanceTo(fragment.Points[^1]) <=
                    JoinTolerance;

                if (!matchesStart && !matchesEnd)
                    continue;

                if (matchesStart && matchesEnd)
                    return false;

                if (matchIndex >= 0)
                    return false;

                matchIndex = index;
                reverse = matchesEnd;
            }

            if (matchIndex < 0)
                return false;

            currentIndex = matchIndex;
            currentReverse = reverse;
        }

        ordered = result;
        return true;
    }

    private static int CountEndpointMatches(
        IReadOnlyList<Fragment> fragments,
        int sourceIndex,
        OcctPoint3d endpoint)
    {
        var count = 0;
        for (var index = 0; index < fragments.Count; index++)
        {
            if (index == sourceIndex)
                continue;

            var fragment = fragments[index];
            if (endpoint.DistanceTo(fragment.Points[0]) <=
                JoinTolerance)
                count++;
            if (endpoint.DistanceTo(fragment.Points[^1]) <=
                JoinTolerance)
                count++;
        }

        return count;
    }

    private static void AppendFragment(
        IReadOnlyList<OcctPoint3d> source,
        bool reverse,
        List<OcctPoint3d> target)
    {
        if (reverse)
        {
            for (var index = source.Count - 1; index >= 0; index--)
                AppendPoint(source[index], target);
            return;
        }

        for (var index = 0; index < source.Count; index++)
            AppendPoint(source[index], target);
    }

    private static void AppendPoint(
        OcctPoint3d point,
        List<OcctPoint3d> target)
    {
        if (target.Count > 0 &&
            target[^1].DistanceTo(point) <= JoinTolerance)
            return;

        target.Add(point);
    }

    private sealed record Fragment(
        CadEntity Source,
        IReadOnlyList<OcctPoint3d> Points);

    private readonly record struct OrderedFragment(
        Fragment Fragment,
        bool Reverse);
}
