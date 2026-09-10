using OcctNet;

namespace OCCAD;

internal static class CadPolylineEditGeometry
{
    private const double Tolerance = 1e-9;
    private const double PlaneTolerance = 1e-6;

    internal static bool TryTrim(
        CadPolylineEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];
        if (target.Points.Count < 2 ||
            target.Points.Any(point => !IsOnPlane(point, plane)))
            return false;

        if (target.Closed)
            return TryTrimClosed(
                target,
                boundaries,
                hitPoint,
                plane,
                out replacements);

        var segmentIndex = NearestSegment(
            target.Points,
            hitPoint,
            plane);
        if (segmentIndex < 0)
            return false;

        var start = target.Points[segmentIndex];
        var end = target.Points[segmentIndex + 1];
        var hitT = ParameterOnSegment(
            start,
            end,
            hitPoint,
            plane);

        var cuts = new List<double>();
        foreach (var boundary in boundaries)
        {
            foreach (var value in CadPlanarCurveIntersections.WithLine(
                         start,
                         end,
                         boundary,
                         plane,
                         targetSegment: true))
            {
                if (value.TargetParameter > Tolerance &&
                    value.TargetParameter < 1.0 - Tolerance)
                    cuts.Add(value.TargetParameter);
            }
        }

        var ordered = cuts
            .DistinctBy(static value => Math.Round(value, 9))
            .OrderBy(static value => value)
            .ToArray();
        if (ordered.Length == 0)
            return false;

        var left = 0.0;
        var right = 1.0;
        foreach (var cut in ordered)
        {
            if (cut < hitT - Tolerance)
                left = cut;
            else if (cut > hitT + Tolerance)
            {
                right = cut;
                break;
            }
            else
            {
                return false;
            }
        }

        if (left <= Tolerance &&
            right >= 1.0 - Tolerance)
            return false;

        var result = new List<CadEntity>(2);

        var leftPoints = new List<OcctPoint3d>();
        for (var index = 0; index <= segmentIndex; index++)
            leftPoints.Add(target.Points[index]);
        if (left > Tolerance)
            leftPoints.Add(PointAt(start, end, left));
        if (leftPoints.Count >= 2)
            result.Add(target.CopyWithPoints(leftPoints, closed: false));

        var rightPoints = new List<OcctPoint3d>();
        if (right < 1.0 - Tolerance)
            rightPoints.Add(PointAt(start, end, right));
        for (var index = segmentIndex + 1; index < target.Points.Count; index++)
            rightPoints.Add(target.Points[index]);
        if (rightPoints.Count >= 2)
            result.Add(target.CopyWithPoints(rightPoints, closed: false));

        replacements = result.ToArray();
        return replacements.Length > 0;
    }

    private static bool TryTrimClosed(
        CadPolylineEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];

        if (!CadPolylinePathGeometry.TryClosestPosition(
                target,
                hitPoint,
                plane,
                out var hitPosition))
            return false;

        var cuts = new List<CadPolylinePathPosition>();
        var segmentCount =
            CadPolylinePathGeometry.SegmentCount(target);

        for (var segment = 0;
             segment < segmentCount;
             segment++)
        {
            var next =
                (segment + 1) %
                target.Points.Count;

            foreach (var boundary in boundaries)
            {
                if (ReferenceEquals(boundary, target))
                    continue;

                foreach (var value in CadPlanarCurveIntersections.WithLine(
                             target.Points[segment],
                             target.Points[next],
                             boundary,
                             plane,
                             targetSegment: true))
                {
                    if (value.TargetParameter <= Tolerance ||
                        value.TargetParameter >= 1.0 - Tolerance)
                        continue;

                    cuts.Add(
                        new CadPolylinePathPosition(
                            segment,
                            value.TargetParameter));
                }
            }
        }

        var ordered = cuts
            .GroupBy(value => Math.Round(value.Value, 9))
            .Select(static group => group.First())
            .OrderBy(static value => value.Value)
            .ToArray();
        if (ordered.Length < 2)
            return false;

        CadPolylinePathPosition previous = ordered[^1];
        CadPolylinePathPosition nextCut = ordered[0];
        var hitValue = hitPosition.Value;

        for (var index = 0; index < ordered.Length; index++)
        {
            var current = ordered[index];
            var following =
                index + 1 < ordered.Length
                    ? ordered[index + 1]
                    : ordered[0];

            var currentValue = current.Value;
            var followingValue = following.Value;
            var candidateHit = hitValue;

            if (index == ordered.Length - 1)
            {
                followingValue += segmentCount;
                if (candidateHit < currentValue)
                    candidateHit += segmentCount;
            }

            if (candidateHit >= currentValue - Tolerance &&
                candidateHit <= followingValue + Tolerance)
            {
                previous = current;
                nextCut = following;
                break;
            }
        }

        var keep = CadPolylinePathGeometry.BuildForwardPath(
            target,
            nextCut,
            previous,
            allowWrap: true);
        if (keep.Count < 2)
            return false;

        replacements =
        [
            target.CopyWithPoints(
                keep,
                closed: false)
        ];
        return true;
    }

    internal static bool TryExtend(
        CadPolylineEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadPolylineEntity replacement)
    {
        replacement = null!;
        if (target.Closed ||
            target.Points.Count < 2 ||
            target.Points.Any(point => !IsOnPlane(point, plane)))
            return false;

        var points = target.Points;
        var extendStart =
            hitPoint.DistanceTo(points[0]) <=
            hitPoint.DistanceTo(points[^1]);

        var segmentStart = extendStart
            ? points[1]
            : points[^2];
        var segmentEnd = extendStart
            ? points[0]
            : points[^1];

        var bestDistance = double.MaxValue;
        OcctPoint3d? bestPoint = null;

        foreach (var boundary in boundaries)
        {
            foreach (var value in CadPlanarCurveIntersections.WithLine(
                         segmentStart,
                         segmentEnd,
                         boundary,
                         plane,
                         targetSegment: false))
            {
                if (value.TargetParameter <= 1.0 + Tolerance)
                    continue;

                var distance =
                    segmentEnd.DistanceTo(value.Point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = value.Point;
                }
            }
        }

        if (bestPoint is null)
            return false;

        var values = points.ToArray();
        values[extendStart ? 0 : values.Length - 1] =
            bestPoint.Value;
        replacement = target.CopyWithPoints(
            values,
            closed: false);
        return true;
    }

    private static int NearestSegment(
        IReadOnlyList<OcctPoint3d> points,
        OcctPoint3d hitPoint,
        CadWorkPlane plane)
    {
        var hit = plane.WorldToLocal(hitPoint);
        var bestIndex = -1;
        var bestDistance = double.MaxValue;

        for (var index = 0; index + 1 < points.Count; index++)
        {
            var start = plane.WorldToLocal(points[index]);
            var end = plane.WorldToLocal(points[index + 1]);
            var distance = DistanceSquaredToSegment(
                hit,
                start,
                end);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static double ParameterOnSegment(
        OcctPoint3d start,
        OcctPoint3d end,
        OcctPoint3d point,
        CadWorkPlane plane)
    {
        var a = plane.WorldToLocal(start);
        var b = plane.WorldToLocal(end);
        var p = plane.WorldToLocal(point);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= Tolerance * Tolerance)
            return 0.0;

        return Math.Clamp(
            ((p.X - a.X) * dx +
             (p.Y - a.Y) * dy) /
            lengthSquared,
            0.0,
            1.0);
    }

    private static OcctPoint3d PointAt(
        OcctPoint3d start,
        OcctPoint3d end,
        double t) =>
        start +
        CadTransformMath.Between(start, end) * t;

    private static double DistanceSquaredToSegment(
        CadPlanePoint point,
        CadPlanePoint start,
        CadPlanePoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= Tolerance * Tolerance)
        {
            var px = point.X - start.X;
            var py = point.Y - start.Y;
            return px * px + py * py;
        }

        var t =
            ((point.X - start.X) * dx +
             (point.Y - start.Y) * dy) /
            lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        var qx = start.X + dx * t;
        var qy = start.Y + dy * t;
        var ox = point.X - qx;
        var oy = point.Y - qy;
        return ox * ox + oy * oy;
    }

    private static bool IsOnPlane(
        OcctPoint3d point,
        CadWorkPlane plane)
    {
        var axial = CadTransformMath.Dot(
            CadTransformMath.Between(
                plane.Origin,
                point),
            plane.Normal);
        return Math.Abs(axial) <= PlaneTolerance;
    }
}
