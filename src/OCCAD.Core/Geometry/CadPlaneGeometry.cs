using OcctNet;

namespace OCCAD;

internal static class CadPlaneGeometry
{
    internal static double RadialDistance(
        OcctPoint3d center,
        OcctPoint3d point,
        OcctVector3d xAxis,
        OcctVector3d yAxis)
    {
        var delta = CadTransformMath.Between(center, point);
        var x = CadTransformMath.Dot(delta, xAxis);
        var y = CadTransformMath.Dot(delta, yAxis);
        return Math.Sqrt(x * x + y * y);
    }

    internal static OcctPoint3d ProjectToPlane(
        OcctPoint3d origin,
        OcctPoint3d point,
        OcctVector3d xAxis,
        OcctVector3d yAxis)
    {
        var delta = CadTransformMath.Between(origin, point);
        var x = CadTransformMath.Dot(delta, xAxis);
        var y = CadTransformMath.Dot(delta, yAxis);
        return CadTransformMath.Add(
            CadTransformMath.Add(origin, xAxis, x),
            yAxis,
            y);
    }

    internal static double SignedDistance(
        OcctPoint3d reference,
        OcctPoint3d point,
        OcctVector3d axis) =>
        CadTransformMath.Dot(CadTransformMath.Between(reference, point), axis);
}
