using OcctNet;

namespace OCCAD;

internal static class CadMirrorGeometry
{
    public static CadEntity Create(CadEntity entity, OcctPoint3d origin, OcctVector3d normal)
    {
        if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin));
        if (!normal.TryNormalize(out var n)) throw new ArgumentOutOfRangeException(nameof(normal));
        OcctPoint3d Point(OcctPoint3d p) => p - n * (2 * (p - origin).Dot(n));
        OcctVector3d Vector(OcctVector3d v) => v - n * (2 * v.Dot(n));
        OcctVector3d PlaneNormal(OcctVector3d v) => Vector(v) * -1;
        return entity switch
        {
            CadAngleDimensionEntity e => new CadAngleDimensionEntity(Point(e.Vertex), Vector(e.FirstDirection), Vector(e.SecondDirection), e.Radius, e.TextHeight, e.ArrowSize, e.FontName),
            CadCircularDimensionEntity e => new CadCircularDimensionEntity(e.Kind, Point(e.Center), PlaneNormal(e.Normal), Vector(e.Direction), e.Radius, e.Offset, e.TextHeight, e.ArrowSize, e.FontName),
            CadLengthDimensionEntity e => new CadLengthDimensionEntity(Point(e.Start), Point(e.End), PlaneNormal(e.Normal), e.Offset, e.TextHeight, e.ArrowSize, e.FontName),
            CadTextEntity e => new CadTextEntity(e.Text, Point(e.Position), PlaneNormal(e.Normal), Vector(e.XAxis), e.Height, e.AngleDegrees, e.FontName),
            CadPointEntity e => new CadPointEntity(Point(e.Position)),
            CadLineEntity e => new CadLineEntity(Point(e.Start), Point(e.End)),
            CadPolylineEntity e => new CadPolylineEntity(e.Points.Select(Point), e.Closed),
            CadPolygonEntity e => new CadPolygonEntity(e.Points.Select(Point)),
            CadRegularPolygonEntity e => new CadRegularPolygonEntity(Point(e.Center), PlaneNormal(e.Normal), Vector(e.XAxis), e.Radius, e.Sides),
            CadRectangleEntity e => new CadRectangleEntity(Point(e.Center), Vector(e.XAxis), Vector(e.YAxis), e.Width, e.Height),
            CadCircleEntity e => new CadCircleEntity(Point(e.Center), PlaneNormal(e.Normal), e.Radius),
            CadArcEntity e => new CadArcEntity(Point(e.Start), Point(e.Middle), Point(e.End)),
            CadEllipseEntity e => new CadEllipseEntity(Point(e.Center), PlaneNormal(e.Normal), Vector(e.XAxis), e.MajorRadius, e.MinorRadius),
            CadSplineEntity e => new CadSplineEntity(e.FitPoints.Select(Point), e.Periodic, e.Tolerance),
            // Reflection reverses handedness. Swap the box's bottom/top reference
            // and reverse its Z axis to retain a right-handed, positive-size frame.
            CadBoxEntity e => new CadBoxEntity(Point(e.Origin) + Vector(e.ZAxis) * e.Height,
                Vector(e.XAxis), Vector(e.YAxis), Vector(e.ZAxis) * -1, e.Length, e.Width, e.Height),
            CadCylinderEntity e => new CadCylinderEntity(Point(e.Origin), Vector(e.Axis), e.Radius, e.Height),
            CadConeEntity e => new CadConeEntity(Point(e.Origin), Vector(e.Axis), e.Radius, e.Height),
            CadFrustumEntity e => new CadFrustumEntity(Point(e.Origin), Vector(e.Axis), e.BaseRadius, e.TopRadius, e.Height),
            CadSphereEntity e => new CadSphereEntity(Point(e.Center), e.Radius),
            CadHelixEntity e => new CadHelixEntity(Point(e.Origin), PlaneNormal(e.Axis), Vector(e.XAxis), e.Radius, -e.Pitch, e.Turns),
            CadEllipsoidEntity e => new CadEllipsoidEntity(Point(e.Center), Vector(e.XAxis), Vector(e.YAxis), PlaneNormal(e.ZAxis), e.XRadius, e.YRadius, e.ZRadius),
            CadTorusEntity e => new CadTorusEntity(Point(e.Center), Vector(e.Axis), e.MajorRadius, e.MinorRadius),
            _ => throw new NotSupportedException($"Mirroring is not supported for entity type '{entity.EntityType}'.")
        };
    }
}






