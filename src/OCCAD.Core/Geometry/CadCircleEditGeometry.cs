using OcctNet;

namespace OCCAD;

internal static class CadCircleEditGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryTrim(
        CadCircleEntity target,
        IReadOnlyList<CadEntity> boundaries,
        OcctPoint3d hitPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];
        if (!CadPlanarCurveIntersections.IsBoundaryOnPlane(target, plane))
            return false;

        var intersections = new List<OcctPoint3d>();
        foreach (var boundary in boundaries)
        {
            if (ReferenceEquals(boundary, target))
                continue;

            intersections.AddRange(
                CadPlanarCurveIntersections.WithCircle(
                    target.Center,
                    target.Radius,
                    boundary,
                    plane));
        }

        var cuts = intersections
            .Select(point => (
                Point: point,
                Angle: Angle(target.Center, point, plane)))
            .GroupBy(value => Math.Round(value.Angle, 9))
            .Select(static group => group.First())
            .OrderBy(static value => value.Angle)
            .ToArray();

        if (cuts.Length < 2)
            return false;

        var hitAngle = Angle(
            target.Center,
            hitPoint,
            plane);

        var previous = cuts[^1];
        var next = cuts[0] with
        {
            Angle = cuts[0].Angle + Math.PI * 2.0
        };
        for (var index = 0; index < cuts.Length; index++)
        {
            var current = cuts[index];
            var following = index + 1 < cuts.Length
                ? cuts[index + 1]
                : cuts[0] with
                {
                    Angle = cuts[0].Angle + Math.PI * 2.0
                };

            var candidateHit = hitAngle;
            if (index == cuts.Length - 1 &&
                candidateHit < current.Angle)
                candidateHit += Math.PI * 2.0;

            if (candidateHit >= current.Angle - Tolerance &&
                candidateHit <= following.Angle + Tolerance)
            {
                previous = current;
                next = following;
                break;
            }
        }

        var removedSweep = next.Angle - previous.Angle;
        if (removedSweep <= Tolerance ||
            removedSweep >= Math.PI * 2.0 - Tolerance)
            return false;

        var keepStart = next.Angle % (Math.PI * 2.0);
        var keepSweep = Math.PI * 2.0 - removedSweep;
        if (keepSweep <= Tolerance ||
            keepSweep >= Math.PI * 2.0 - Tolerance)
            return false;

        var start = PointAt(
            target.Center,
            target.Radius,
            keepStart,
            plane);
        var middle = PointAt(
            target.Center,
            target.Radius,
            keepStart + keepSweep * 0.5,
            plane);
        var end = PointAt(
            target.Center,
            target.Radius,
            keepStart + keepSweep,
            plane);

        replacements = [target.CreateArc(start, middle, end)];
        return true;
    }

    private static double Angle(
        OcctPoint3d center,
        OcctPoint3d point,
        CadWorkPlane plane)
    {
        var c = plane.WorldToLocal(center);
        var p = plane.WorldToLocal(point);
        var value = Math.Atan2(
            p.Y - c.Y,
            p.X - c.X);
        return value < 0.0
            ? value + Math.PI * 2.0
            : value;
    }

    private static OcctPoint3d PointAt(
        OcctPoint3d center,
        double radius,
        double angle,
        CadWorkPlane plane)
    {
        var local = plane.WorldToLocal(center);
        return plane.LocalToWorld(
            new CadPlanePoint(
                local.X + Math.Cos(angle) * radius,
                local.Y + Math.Sin(angle) * radius));
    }
}
