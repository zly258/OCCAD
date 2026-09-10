using OcctNet;

namespace OCCAD;

internal static class CadLineEditGeometry
{
    private const double Tolerance = 1e-9;
    private const double PlaneTolerance = 1e-6;

    internal static bool TryTrim(
        CadLineEntity target,
        IReadOnlyList<CadLineEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(boundaries);
        ArgumentNullException.ThrowIfNull(plane);

        replacements = [];
        if (!IsOnPlane(target, plane))
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
            if (ReferenceEquals(boundary, target) ||
                !IsOnPlane(boundary, plane))
                continue;

            if (TryIntersect(
                    target,
                    boundary,
                    plane,
                    requireTargetSegment: true,
                    requireBoundarySegment: true,
                    out var targetT) &&
                targetT > Tolerance &&
                targetT < 1.0 - Tolerance)
            {
                intersections.Add(targetT);
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
        IReadOnlyList<CadLineEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadLineEntity replacement)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(boundaries);
        ArgumentNullException.ThrowIfNull(plane);

        replacement = null!;
        if (!IsOnPlane(target, plane))
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
            if (ReferenceEquals(boundary, target) ||
                !IsOnPlane(boundary, plane))
                continue;

            if (!TryIntersect(
                    target,
                    boundary,
                    plane,
                    requireTargetSegment: false,
                    requireBoundarySegment: true,
                    out var targetT))
                continue;

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

    private static bool IsOnPlane(
        CadLineEntity line,
        CadWorkPlane plane)
    {
        var startDistance = CadTransformMath.Dot(
            CadTransformMath.Between(plane.Origin, line.Start),
            plane.Normal);
        var endDistance = CadTransformMath.Dot(
            CadTransformMath.Between(plane.Origin, line.End),
            plane.Normal);
        return Math.Abs(startDistance) <= PlaneTolerance &&
               Math.Abs(endDistance) <= PlaneTolerance;
    }

    private static bool TryIntersect(
        CadLineEntity first,
        CadLineEntity second,
        CadWorkPlane plane,
        bool requireTargetSegment,
        bool requireBoundarySegment,
        out double firstT)
    {
        var a = plane.WorldToLocal(first.Start);
        var b = plane.WorldToLocal(first.End);
        var c = plane.WorldToLocal(second.Start);
        var d = plane.WorldToLocal(second.End);

        var rx = b.X - a.X;
        var ry = b.Y - a.Y;
        var sx = d.X - c.X;
        var sy = d.Y - c.Y;
        var denominator = Cross(rx, ry, sx, sy);
        if (Math.Abs(denominator) <= Tolerance)
        {
            firstT = 0.0;
            return false;
        }

        var qpx = c.X - a.X;
        var qpy = c.Y - a.Y;
        var t = Cross(qpx, qpy, sx, sy) / denominator;
        var u = Cross(qpx, qpy, rx, ry) / denominator;

        if ((requireTargetSegment &&
             (t < -Tolerance || t > 1.0 + Tolerance)) ||
            (requireBoundarySegment &&
             (u < -Tolerance || u > 1.0 + Tolerance)))
        {
            firstT = 0.0;
            return false;
        }

        firstT = t;
        return double.IsFinite(t);
    }

    private static double Cross(
        double ax,
        double ay,
        double bx,
        double by) =>
        ax * by - ay * bx;
}
