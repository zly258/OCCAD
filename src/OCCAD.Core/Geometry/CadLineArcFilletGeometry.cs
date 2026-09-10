using OcctNet;

namespace OCCAD;

internal static class CadLineArcFilletGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryFillet(
        CadLineEntity line,
        OcctPoint3d linePick,
        CadArcEntity arc,
        OcctPoint3d arcPick,
        double filletRadius,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];

        if (!double.IsFinite(filletRadius) ||
            filletRadius <= Tolerance ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(line, plane) ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(arc, plane))
            return false;

        var a = plane.WorldToLocal(line.Start);
        var b = plane.WorldToLocal(line.End);
        var center = plane.WorldToLocal(arc.Center);
        var pickLine = plane.WorldToLocal(linePick);

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= Tolerance)
            return false;

        var ux = dx / length;
        var uy = dy / length;
        var nx = -uy;
        var ny = ux;

        var linePickProjection =
            (pickLine.X - a.X) * ux +
            (pickLine.Y - a.Y) * uy;

        var candidates = new List<Candidate>();
        foreach (var lineSide in new[] { -1.0, 1.0 })
        {
            var offsetPoint = new CadPlanePoint(
                a.X + nx * filletRadius * lineSide,
                a.Y + ny * filletRadius * lineSide);

            foreach (var circleMode in new[] { -1.0, 1.0 })
            {
                var centerDistance =
                    arc.Radius + filletRadius * circleMode;
                if (centerDistance <= Tolerance)
                    continue;

                foreach (var filletCenter in IntersectLineCircle(
                             offsetPoint,
                             new CadPlanePoint(ux, uy),
                             center,
                             centerDistance))
                {
                    var projection =
                        (filletCenter.X - a.X) * ux +
                        (filletCenter.Y - a.Y) * uy;
                    var lineTangent = new CadPlanePoint(
                        a.X + ux * projection,
                        a.Y + uy * projection);

                    var radial = Normalize(
                        new CadPlanePoint(
                            filletCenter.X - center.X,
                            filletCenter.Y - center.Y));
                    var arcTangent = new CadPlanePoint(
                        center.X + radial.X * arc.Radius,
                        center.Y + radial.Y * arc.Radius);
                    var arcTangentWorld =
                        plane.LocalToWorld(arcTangent);

                    if (!arc.TryParameterAt(
                            arcTangentWorld,
                            out var arcParameter))
                        continue;

                    var lineTangentWorld =
                        plane.LocalToWorld(lineTangent);
                    if (!TryTrimLine(
                            line,
                            linePickProjection,
                            projection,
                            lineTangentWorld,
                            plane,
                            out var trimmedLine))
                        continue;

                    if (!TryTrimArc(
                            arc,
                            arcPick,
                            arcParameter,
                            out var trimmedArc))
                        continue;

                    var filletCenterWorld =
                        plane.LocalToWorld(filletCenter);
                    var start = lineTangentWorld;
                    var end = arcTangentWorld;
                    var middle = FilletMiddlePoint(
                        filletCenterWorld,
                        start,
                        end,
                        filletRadius);

                    CadArcEntity filletArc;
                    try
                    {
                        filletArc = line.CreateArc(
                            start,
                            middle,
                            end);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    var score =
                        linePick.DistanceTo(lineTangentWorld) +
                        arcPick.DistanceTo(arcTangentWorld);
                    candidates.Add(
                        new Candidate(
                            score,
                            trimmedLine,
                            trimmedArc,
                            filletArc));
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        var best = candidates
            .OrderBy(static value => value.Score)
            .First();

        replacements =
        [
            best.Line,
            best.Arc,
            best.Fillet
        ];
        return true;
    }

    private static bool TryTrimLine(
        CadLineEntity line,
        double pickProjection,
        double tangentProjection,
        OcctPoint3d tangent,
        CadWorkPlane plane,
        out CadLineEntity trimmed)
    {
        trimmed = null!;
        var a = plane.WorldToLocal(line.Start);
        var b = plane.WorldToLocal(line.End);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var startProjection = 0.0;
        var endProjection = length;
        var keepStart =
            Math.Abs(pickProjection - startProjection) <=
            Math.Abs(pickProjection - endProjection);

        if (keepStart)
        {
            if (tangentProjection <= Tolerance)
                return false;
            trimmed = line.CreateLine(
                line.Start,
                tangent);
        }
        else
        {
            if (tangentProjection >= length - Tolerance)
                return false;
            trimmed = line.CreateLine(
                tangent,
                line.End);
        }

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

    private static IEnumerable<CadPlanePoint> IntersectLineCircle(
        CadPlanePoint linePoint,
        CadPlanePoint lineDirection,
        CadPlanePoint center,
        double radius)
    {
        var fx = linePoint.X - center.X;
        var fy = linePoint.Y - center.Y;
        var b = 2.0 *
                (fx * lineDirection.X +
                 fy * lineDirection.Y);
        var c = fx * fx + fy * fy - radius * radius;
        var discriminant = b * b - 4.0 * c;
        if (discriminant < -Tolerance)
            yield break;

        var root = Math.Sqrt(Math.Max(0.0, discriminant));
        yield return new CadPlanePoint(
            linePoint.X +
            lineDirection.X * ((-b - root) * 0.5),
            linePoint.Y +
            lineDirection.Y * ((-b - root) * 0.5));

        if (root > Tolerance)
        {
            yield return new CadPlanePoint(
                linePoint.X +
                lineDirection.X * ((-b + root) * 0.5),
                linePoint.Y +
                lineDirection.Y * ((-b + root) * 0.5));
        }
    }

    private static OcctPoint3d FilletMiddlePoint(
        OcctPoint3d center,
        OcctPoint3d start,
        OcctPoint3d end,
        double radius)
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

        if (!sum.TryNormalize(out var middleDirection))
        {
            var axis = first.Cross(OcctVector3d.UnitZ);
            if (!axis.TryNormalize(out middleDirection))
                middleDirection = OcctVector3d.UnitY;
        }

        return center + middleDirection * radius;
    }

    private static CadPlanePoint Normalize(CadPlanePoint value)
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
        CadLineEntity Line,
        CadArcEntity Arc,
        CadArcEntity Fillet);
}
