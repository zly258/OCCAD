using OcctNet;

namespace OCCAD;

internal static class CadLineEditGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryTrim(
        CadLineEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(boundaries);
        ArgumentNullException.ThrowIfNull(plane);

        replacements = [];
        if (!CadPlanarCurveIntersections.IsOnPlane(target.Start, plane) ||
            !CadPlanarCurveIntersections.IsOnPlane(target.End, plane))
            return false;

        var start = plane.WorldToLocal(target.Start);
        var end = plane.WorldToLocal(target.End);
        var hit = plane.WorldToLocal(hitPoint);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= Tolerance * Tolerance)
            return false;

        var hitT =
            ((hit.X - start.X) * dx +
             (hit.Y - start.Y) * dy) /
            lengthSquared;
        hitT = Math.Clamp(hitT, 0.0, 1.0);

        var intersections = new List<double>();
        foreach (var boundary in boundaries)
        {
            if (ReferenceEquals(boundary, target))
                continue;

            foreach (var value in CadPlanarCurveIntersections.WithLine(
                         target.Start,
                         target.End,
                         boundary,
                         plane,
                         targetSegment: true))
            {
                if (value.TargetParameter > Tolerance &&
                    value.TargetParameter < 1.0 - Tolerance)
                    intersections.Add(value.TargetParameter);
            }
        }

        var cuts = intersections
            .DistinctBy(static value => Math.Round(value, 9))
            .OrderBy(static value => value)
            .ToArray();
        if (cuts.Length == 0)
            return false;

        var left = 0.0;
        var right = 1.0;
        foreach (var cut in cuts)
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

        if (left <= Tolerance && right >= 1.0 - Tolerance)
            return false;

        var result = new List<CadEntity>(2);
        if (left > Tolerance)
            result.Add(CreateSegment(target, 0.0, left));
        if (right < 1.0 - Tolerance)
            result.Add(CreateSegment(target, right, 1.0));

        replacements = result.ToArray();
        return true;
    }

    internal static bool TryExtend(
        CadLineEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadLineEntity replacement)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(boundaries);
        ArgumentNullException.ThrowIfNull(plane);

        replacement = null!;
        if (!CadPlanarCurveIntersections.IsOnPlane(target.Start, plane) ||
            !CadPlanarCurveIntersections.IsOnPlane(target.End, plane))
            return false;

        var start = plane.WorldToLocal(target.Start);
        var end = plane.WorldToLocal(target.End);
        var hit = plane.WorldToLocal(hitPoint);
        var hitStart =
            (hit.X - start.X) * (hit.X - start.X) +
            (hit.Y - start.Y) * (hit.Y - start.Y);
        var hitEnd =
            (hit.X - end.X) * (hit.X - end.X) +
            (hit.Y - end.Y) * (hit.Y - end.Y);
        var extendStart = hitStart <= hitEnd;

        double? bestT = null;
        foreach (var boundary in boundaries)
        {
            if (ReferenceEquals(boundary, target))
                continue;

            foreach (var value in CadPlanarCurveIntersections.WithLine(
                         target.Start,
                         target.End,
                         boundary,
                         plane,
                         targetSegment: false))
            {
                var targetT = value.TargetParameter;
                if (extendStart)
                {
                    if (targetT >= -Tolerance)
                        continue;
                    if (bestT is null || targetT > bestT.Value)
                        bestT = targetT;
                }
                else
                {
                    if (targetT <= 1.0 + Tolerance)
                        continue;
                    if (bestT is null || targetT < bestT.Value)
                        bestT = targetT;
                }
            }
        }

        if (bestT is null)
            return false;

        var point = PointAt(target, bestT.Value);
        replacement = (CadLineEntity)target.Duplicate();
        replacement.MoveGrip(
            extendStart ? 0 : 2,
            point);
        return true;
    }

    private static CadLineEntity CreateSegment(
        CadLineEntity source,
        double startT,
        double endT)
    {
        var copy = (CadLineEntity)source.Duplicate();
        copy.MoveGrip(0, PointAt(source, startT));
        copy.MoveGrip(2, PointAt(source, endT));
        return copy;
    }

    private static OcctPoint3d PointAt(
        CadLineEntity line,
        double t) =>
        line.Start +
        CadTransformMath.Between(line.Start, line.End) * t;

}
