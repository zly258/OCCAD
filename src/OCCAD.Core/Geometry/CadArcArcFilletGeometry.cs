using OcctNet;

namespace OCCAD;

internal static class CadArcArcFilletGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryFillet(
        CadArcEntity first,
        OcctPoint3d firstPick,
        CadArcEntity second,
        OcctPoint3d secondPick,
        double filletRadius,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];

        if (ReferenceEquals(first, second) ||
            !double.IsFinite(filletRadius) ||
            filletRadius <= Tolerance ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(first, plane) ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(second, plane))
            return false;

        var firstCenter = plane.WorldToLocal(first.Center);
        var secondCenter = plane.WorldToLocal(second.Center);
        var candidates = new List<Candidate>();

        foreach (var firstMode in new[] { -1.0, 1.0 })
        {
            var firstOffsetRadius =
                first.Radius + filletRadius * firstMode;
            if (firstOffsetRadius <= Tolerance)
                continue;

            foreach (var secondMode in new[] { -1.0, 1.0 })
            {
                var secondOffsetRadius =
                    second.Radius + filletRadius * secondMode;
                if (secondOffsetRadius <= Tolerance)
                    continue;

                foreach (var filletCenter in CircleIntersections(
                             firstCenter,
                             firstOffsetRadius,
                             secondCenter,
                             secondOffsetRadius))
                {
                    var firstDirection = Normalize(
                        new CadPlanePoint(
                            filletCenter.X - firstCenter.X,
                            filletCenter.Y - firstCenter.Y));
                    var secondDirection = Normalize(
                        new CadPlanePoint(
                            filletCenter.X - secondCenter.X,
                            filletCenter.Y - secondCenter.Y));

                    var firstTangent = new CadPlanePoint(
                        firstCenter.X +
                        firstDirection.X * first.Radius,
                        firstCenter.Y +
                        firstDirection.Y * first.Radius);
                    var secondTangent = new CadPlanePoint(
                        secondCenter.X +
                        secondDirection.X * second.Radius,
                        secondCenter.Y +
                        secondDirection.Y * second.Radius);

                    var firstTangentWorld =
                        plane.LocalToWorld(firstTangent);
                    var secondTangentWorld =
                        plane.LocalToWorld(secondTangent);

                    if (!first.TryParameterAt(
                            firstTangentWorld,
                            out var firstParameter) ||
                        !second.TryParameterAt(
                            secondTangentWorld,
                            out var secondParameter))
                        continue;

                    if (!TryTrimArc(
                            first,
                            firstPick,
                            firstParameter,
                            out var firstTrimmed) ||
                        !TryTrimArc(
                            second,
                            secondPick,
                            secondParameter,
                            out var secondTrimmed))
                        continue;

                    var filletCenterWorld =
                        plane.LocalToWorld(filletCenter);
                    if (!TryMiddlePoint(
                            filletCenterWorld,
                            firstTangentWorld,
                            secondTangentWorld,
                            filletRadius,
                            out var middle))
                        continue;

                    CadArcEntity filletArc;
                    try
                    {
                        filletArc = first.CreateArc(
                            firstTangentWorld,
                            middle,
                            secondTangentWorld);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    var score =
                        firstPick.DistanceTo(firstTangentWorld) +
                        secondPick.DistanceTo(secondTangentWorld);

                    candidates.Add(
                        new Candidate(
                            score,
                            firstTrimmed,
                            secondTrimmed,
                            filletArc));
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        var best = candidates
            .OrderBy(static candidate => candidate.Score)
            .First();

        replacements =
        [
            best.First,
            best.Second,
            best.Fillet
        ];
        return true;
    }

    private static bool TryTrimArc(
        CadArcEntity arc,
        OcctPoint3d pick,
        double tangentParameter,
        out CadArcEntity trimmed)
    {
        trimmed = null!;

        if (!arc.TryClosestParameter(
                pick,
                out var pickParameter))
            return false;

        try
        {
            if (pickParameter <= tangentParameter)
            {
                if (tangentParameter <= Tolerance)
                    return false;

                trimmed = arc.CopyWithParameterRange(
                    0.0,
                    tangentParameter);
            }
            else
            {
                if (tangentParameter >= 1.0 - Tolerance)
                    return false;

                trimmed = arc.CopyWithParameterRange(
                    tangentParameter,
                    1.0);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<CadPlanePoint> CircleIntersections(
        CadPlanePoint firstCenter,
        double firstRadius,
        CadPlanePoint secondCenter,
        double secondRadius)
    {
        var dx = secondCenter.X - firstCenter.X;
        var dy = secondCenter.Y - firstCenter.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance <= Tolerance ||
            distance > firstRadius + secondRadius + Tolerance ||
            distance < Math.Abs(firstRadius - secondRadius) - Tolerance)
            yield break;

        var along =
            (firstRadius * firstRadius -
             secondRadius * secondRadius +
             distance * distance) /
            (2.0 * distance);
        var heightSquared =
            firstRadius * firstRadius -
            along * along;

        if (heightSquared < -Tolerance)
            yield break;

        var ux = dx / distance;
        var uy = dy / distance;
        var baseX = firstCenter.X + along * ux;
        var baseY = firstCenter.Y + along * uy;

        if (Math.Abs(heightSquared) <= Tolerance)
        {
            yield return new CadPlanePoint(
                baseX,
                baseY);
            yield break;
        }

        var height = Math.Sqrt(
            Math.Max(0.0, heightSquared));
        var px = -uy * height;
        var py = ux * height;

        yield return new CadPlanePoint(
            baseX + px,
            baseY + py);
        yield return new CadPlanePoint(
            baseX - px,
            baseY - py);
    }

    private static bool TryMiddlePoint(
        OcctPoint3d center,
        OcctPoint3d start,
        OcctPoint3d end,
        double radius,
        out OcctPoint3d middle)
    {
        var first = CadTransformMath.Normalize(
            CadTransformMath.Between(center, start),
            nameof(start));
        var second = CadTransformMath.Normalize(
            CadTransformMath.Between(center, end),
            nameof(end));
        var sum = new OcctVector3d(
            first.X + second.X,
            first.Y + second.Y,
            first.Z + second.Z);

        if (!sum.TryNormalize(out var direction))
        {
            middle = default;
            return false;
        }

        middle = center + direction * radius;
        return true;
    }

    private static CadPlanePoint Normalize(
        CadPlanePoint value)
    {
        var length = Math.Sqrt(
            value.X * value.X +
            value.Y * value.Y);
        if (length <= Tolerance)
            throw new InvalidOperationException();

        return new CadPlanePoint(
            value.X / length,
            value.Y / length);
    }

    private readonly record struct Candidate(
        double Score,
        CadArcEntity First,
        CadArcEntity Second,
        CadArcEntity Fillet);
}
