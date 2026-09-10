using OcctNet;

namespace OCCAD;

internal static class CadArcEditGeometry
{
    private const double Tolerance = 1e-9;
    private const double PlaneTolerance = 1e-6;

    internal static bool TryTrim(
        CadArcEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];
        if (!CadPlanarCurveIntersections.IsBoundaryOnPlane(target, plane) ||
            !target.TryParameterAt(hitPoint, out var hitParameter))
            return false;

        var cuts = new List<double>();
        foreach (var boundary in boundaries)
        {
            foreach (var point in CadPlanarCurveIntersections.WithCircle(
                         target.Center,
                         target.Radius,
                         boundary,
                         plane))
            {
                if (target.TryParameterAt(
                        point,
                        out var parameter) &&
                    parameter > Tolerance &&
                    parameter < 1.0 - Tolerance)
                {
                    cuts.Add(parameter);
                }
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
            if (cut < hitParameter - Tolerance)
                left = cut;
            else if (cut > hitParameter + Tolerance)
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
        if (left > Tolerance)
            result.Add(
                target.CopyWithParameterRange(
                    0.0,
                    left));
        if (right < 1.0 - Tolerance)
            result.Add(
                target.CopyWithParameterRange(
                    right,
                    1.0));

        replacements = result.ToArray();
        return true;
    }

    internal static bool TryExtend(
        CadArcEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadArcEntity replacement)
    {
        replacement = null!;
        if (!CadPlanarCurveIntersections.IsBoundaryOnPlane(target, plane))
            return false;

        var extendStart =
            hitPoint.DistanceTo(target.Start) <=
            hitPoint.DistanceTo(target.End);
        var currentSpan = Math.Abs(target.SweepAngleDegrees);
        var clockwise = target.SweepAngleDegrees < 0.0;
        double? bestExtension = null;
        double bestCandidateAngle = 0.0;

        foreach (var boundary in boundaries)
        {
            foreach (var point in CadPlanarCurveIntersections.WithCircle(
                         target.Center,
                         target.Radius,
                         boundary,
                         plane))
            {
                if (!TryAngle(
                        target,
                        point,
                        out var candidateAngle))
                    continue;

                if (extendStart)
                {
                    var extension = clockwise
                        ? NormalizePositive(
                            candidateAngle -
                            target.StartAngleDegrees)
                        : NormalizePositive(
                            target.StartAngleDegrees -
                            candidateAngle);
                    if (extension <= Tolerance ||
                        extension + currentSpan >= 360.0 - Tolerance)
                        continue;

                    if (bestExtension is null ||
                        extension < bestExtension.Value)
                    {
                        bestExtension = extension;
                        bestCandidateAngle = candidateAngle;
                    }
                }
                else
                {
                    var endAngle =
                        target.StartAngleDegrees +
                        target.SweepAngleDegrees;
                    var extension = clockwise
                        ? NormalizePositive(
                            endAngle -
                            candidateAngle)
                        : NormalizePositive(
                            candidateAngle -
                            endAngle);
                    if (extension <= Tolerance ||
                        extension + currentSpan >= 360.0 - Tolerance)
                        continue;

                    if (bestExtension is null ||
                        extension < bestExtension.Value)
                    {
                        bestExtension = extension;
                        bestCandidateAngle = candidateAngle;
                    }
                }
            }
        }

        if (bestExtension is null)
            return false;

        if (extendStart)
        {
            var newSweep = clockwise
                ? -(currentSpan + bestExtension.Value)
                : currentSpan + bestExtension.Value;
            replacement = target.CopyWithAngles(
                bestCandidateAngle,
                newSweep);
        }
        else
        {
            var newSweep = clockwise
                ? -(currentSpan + bestExtension.Value)
                : currentSpan + bestExtension.Value;
            replacement = target.CopyWithAngles(
                target.StartAngleDegrees,
                newSweep);
        }

        return true;
    }

    private static bool TryAngle(
        CadArcEntity arc,
        OcctPoint3d point,
        out double angle)
    {
        var delta =
            CadTransformMath.Between(
                arc.Center,
                point);
        var yAxis =
            arc.Normal.Cross(arc.XAxis).Normalized();
        var x = CadTransformMath.Dot(
            delta,
            arc.XAxis);
        var y = CadTransformMath.Dot(
            delta,
            yAxis);
        if (x * x + y * y <= Tolerance * Tolerance)
        {
            angle = 0.0;
            return false;
        }

        angle = NormalizeDegrees(
            Math.Atan2(y, x) *
            180.0 /
            Math.PI);
        return true;
    }

    private static double NormalizeDegrees(double angle)
    {
        var value = angle % 360.0;
        return value < 0.0 ? value + 360.0 : value;
    }

    private static double NormalizePositive(double angle)
    {
        var value = angle % 360.0;
        return value < 0.0 ? value + 360.0 : value;
    }
}
