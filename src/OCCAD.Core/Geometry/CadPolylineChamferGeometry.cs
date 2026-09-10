using OcctNet;

namespace OCCAD;

internal static class CadPolylineChamferGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryChamfer(
        CadPolylineEntity polyline,
        OcctPoint3d pickPoint,
        double firstDistance,
        double secondDistance,
        CadWorkPlane plane,
        out CadPolylineEntity replacement)
    {
        replacement = null!;

        if (!double.IsFinite(firstDistance) ||
            !double.IsFinite(secondDistance) ||
            firstDistance <= Tolerance ||
            secondDistance <= Tolerance ||
            !CadPlanarCurveIntersections.IsBoundaryOnPlane(polyline, plane))
            return false;

        var vertex = FindNearestEditableVertex(
            polyline,
            pickPoint);
        if (vertex < 0)
            return false;

        var count = polyline.Points.Count;
        var previousIndex = vertex == 0
            ? count - 1
            : vertex - 1;
        var nextIndex = vertex + 1 == count
            ? 0
            : vertex + 1;

        if (!polyline.Closed &&
            (vertex == 0 || vertex == count - 1))
            return false;

        var previous = polyline.Points[previousIndex];
        var corner = polyline.Points[vertex];
        var next = polyline.Points[nextIndex];

        var firstLength = previous.DistanceTo(corner);
        var secondLength = corner.DistanceTo(next);
        if (firstDistance >= firstLength - Tolerance ||
            secondDistance >= secondLength - Tolerance)
            return false;

        var firstDirection = CadTransformMath.Normalize(
            CadTransformMath.Between(corner, previous),
            nameof(previous));
        var secondDirection = CadTransformMath.Normalize(
            CadTransformMath.Between(corner, next),
            nameof(next));

        var cosine = Math.Abs(
            CadTransformMath.Dot(
                firstDirection,
                secondDirection));
        if (Math.Abs(1.0 - cosine) <= 1e-7)
            return false;

        var firstPoint =
            corner + firstDirection * firstDistance;
        var secondPoint =
            corner + secondDirection * secondDistance;
        if (firstPoint.DistanceTo(secondPoint) <= Tolerance)
            return false;

        var points = new List<OcctPoint3d>(count + 1);
        for (var index = 0; index < count; index++)
        {
            if (index != vertex)
            {
                points.Add(polyline.Points[index]);
                continue;
            }

            points.Add(firstPoint);
            points.Add(secondPoint);
        }

        replacement = polyline.CopyWithPoints(
            points,
            polyline.Closed);
        return true;
    }

    private static int FindNearestEditableVertex(
        CadPolylineEntity polyline,
        OcctPoint3d pickPoint)
    {
        var start = polyline.Closed ? 0 : 1;
        var end = polyline.Closed
            ? polyline.Points.Count
            : polyline.Points.Count - 1;

        var best = -1;
        var distance = double.MaxValue;
        for (var index = start; index < end; index++)
        {
            var current =
                polyline.Points[index].DistanceTo(pickPoint);
            if (current >= distance)
                continue;

            distance = current;
            best = index;
        }

        return best;
    }
}
