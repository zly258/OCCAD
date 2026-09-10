using OcctNet;

namespace OCCAD;

/// <summary>
/// Creates detached mirrored geometry for entity types in the active OCCAD
/// capability surface. Associative derived entities deliberately drop source
/// references so a mirrored copy cannot keep reacting to an original host.
/// </summary>
internal static class CadMirrorGeometry
{
    public static CadEntity Create(
        CadEntity entity,
        OcctPoint3d origin,
        OcctVector3d normal)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!origin.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (!normal.TryNormalize(out var n))
            throw new ArgumentOutOfRangeException(nameof(normal));

        OcctPoint3d Point(OcctPoint3d point)
        {
            var world = entity.ToWorldPoint(point);
            return world - n * (2.0 * (world - origin).Dot(n));
        }

        OcctVector3d Vector(OcctVector3d vector)
        {
            var world = entity.ToWorldVector(vector);
            return world - n * (2.0 * world.Dot(n));
        }

        // Reflection flips handedness. Reverse reflected plane normals so curve
        // winding remains consistent with the source entity convention.
        OcctVector3d PlaneNormal(OcctVector3d vector) => Vector(vector) * -1.0;

        return entity switch
        {
            CadPointEntity value =>
                new CadPointEntity(Point(value.Point)),
            CadLineEntity value =>
                new CadLineEntity(Point(value.Start), Point(value.End)),
            CadCenterLineEntity value =>
                new CadCenterLineEntity(
                    Point(value.Start),
                    Point(value.End),
                    value.StartExtend,
                    value.EndExtend,
                    value.ShowExtend),
            CadCenterMarkEntity value =>
                new CadCenterMarkEntity(
                    Point(value.Center),
                    PlaneNormal(value.Normal),
                    value.Radius,
                    value.CrossSizeFactor,
                    value.CrossSpacingFactor,
                    value.LeftExtend,
                    value.RightExtend,
                    value.TopExtend,
                    value.BottomExtend,
                    value.ShowExtend),
            CadPolylineEntity value =>
                new CadPolylineEntity(
                    value.Points.Select(Point),
                    value.Closed),
            CadPolygonEntity value =>
                new CadPolygonEntity(value.Points.Select(Point)),
            CadRegularPolygonEntity value =>
                new CadRegularPolygonEntity(
                    Point(value.Center),
                    PlaneNormal(value.Normal),
                    Vector(value.XAxis),
                    value.Radius,
                    value.Sides),
            CadRectangleEntity value =>
                new CadRectangleEntity(
                    Point(value.Center),
                    Vector(value.XAxis),
                    Vector(value.YAxis),
                    value.Width,
                    value.Height),
            CadCircleEntity value =>
                new CadCircleEntity(
                    Point(value.Center),
                    PlaneNormal(value.Normal),
                    value.Radius),
            CadArcEntity value =>
                new CadArcEntity(
                    Point(value.Start),
                    Point(value.Middle),
                    Point(value.End)),
            CadEllipseEntity value =>
                new CadEllipseEntity(
                    Point(value.Center),
                    PlaneNormal(value.Normal),
                    Vector(value.XAxis),
                    value.MajorRadius,
                    value.MinorRadius),
            CadSplineEntity value =>
                new CadSplineEntity(
                    value.FitPoints.Select(Point),
                    value.Periodic,
                    value.Tolerance),
            CadBoxEntity value =>
                new CadBoxEntity(
                    Point(value.Origin) + Vector(value.ZAxis) * value.Height,
                    Vector(value.XAxis),
                    Vector(value.YAxis),
                    Vector(value.ZAxis) * -1.0,
                    value.Length,
                    value.Width,
                    value.Height),
            CadCylinderEntity value =>
                new CadCylinderEntity(
                    Point(value.Origin),
                    Vector(value.Axis),
                    value.Radius,
                    value.Height),
            CadConeEntity value =>
                new CadConeEntity(
                    Point(value.Origin),
                    Vector(value.Axis),
                    value.Radius,
                    value.Height),
            CadSphereEntity value =>
                new CadSphereEntity(Point(value.Center), value.Radius),
            _ => throw new NotSupportedException(
                $"Mirroring is not supported for entity type '{entity.EntityType}'.")
        };
    }
}
