using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

internal static class CadPlanarProfileGeometry
{
    internal static bool IsSource(CadEntity entity) =>
        entity is CadCircleEntity or
        CadRectangleEntity or
        CadPolygonEntity or
        CadRegularPolygonEntity or
        CadPolylineEntity { Closed: true };

    internal static bool IsSupported(CadEntity entity) =>
        IsSource(entity) || entity is CadRegionEntity;

    internal static bool IsWireProfile(CadEntity entity) =>
        entity switch
        {
            CadRectangleEntity => true,
            CadPolygonEntity => true,
            CadRegularPolygonEntity => true,
            CadPolylineEntity { Closed: true } => true,
            CadRegionEntity region =>
                IsWireProfile(region.ProfileSnapshot()),
            _ => false
        };

    internal static CadEntity WireProfileSnapshot(CadEntity entity)
    {
        var snapshot = Snapshot(entity);
        if (!IsWireProfile(snapshot))
            throw new ArgumentException(
                "Entity is not a supported wire profile.",
                nameof(entity));
        return snapshot;
    }

    internal static CadEntity Snapshot(CadEntity entity) =>
        entity switch
        {
            CadRegionEntity region => region.ProfileSnapshot(),
            _ when IsSource(entity) => entity.Duplicate(),
            _ => throw new ArgumentException(
                "Entity is not a supported planar profile.",
                nameof(entity))
        };

    internal static OcctShape BuildFace(
        OcctEngine engine,
        CadEntity profile)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(profile);

        var wire = profile.BuildShape(engine);
        try
        {
            return engine.MakeFace(wire);
        }
        finally
        {
            if (engine.ContainsObject(wire.Id))
                engine.Delete(wire);
        }
    }

    internal static OcctPoint3d Center(CadEntity profile) =>
        profile switch
        {
            CadCircleEntity circle => circle.Center,
            CadRectangleEntity rectangle => rectangle.Center,
            CadRegularPolygonEntity polygon => polygon.Center,
            CadPolygonEntity polygon => Average(polygon.Points),
            CadPolylineEntity { Closed: true } polyline => Average(polyline.Points),
            _ => throw new ArgumentException(
                "Entity is not a supported planar profile.",
                nameof(profile))
        };

    internal static OcctVector3d Normal(CadEntity profile) =>
        profile switch
        {
            CadCircleEntity circle => circle.Normal,
            CadRectangleEntity rectangle =>
                rectangle.XAxis.Cross(rectangle.YAxis).Normalized(),
            CadRegularPolygonEntity polygon => polygon.Normal,
            CadPolygonEntity polygon => PolygonNormal(polygon.Points),
            CadPolylineEntity { Closed: true } polyline =>
                PolygonNormal(polyline.Points),
            _ => throw new ArgumentException(
                "Entity is not a supported planar profile.",
                nameof(profile))
        };

    internal static JsonObject Write(CadEntity profile)
    {
        var (id, geometry) = profile switch
        {
            CadCircleEntity value =>
                ("circle", CadCircleEntity.WriteGeometry(value)),
            CadRectangleEntity value =>
                ("rectangle", CadRectangleEntity.WriteGeometry(value)),
            CadPolygonEntity value =>
                ("polygon", CadPolygonEntity.WriteGeometry(value)),
            CadRegularPolygonEntity value =>
                ("regularpolygon", CadRegularPolygonEntity.WriteGeometry(value)),
            CadPolylineEntity { Closed: true } value =>
                ("polyline", CadPolylineEntity.WriteGeometry(value)),
            _ => throw new ArgumentException(
                "Entity is not a supported planar profile.",
                nameof(profile))
        };

        return new JsonObject
        {
            ["type"] = id,
            ["geometry"] = geometry
        };
    }

    internal static CadEntity Read(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var type =
            data["type"]?.GetValue<string>() ??
            throw new FormatException("Profile type is missing.");
        var geometry =
            data["geometry"] as JsonObject ??
            throw new FormatException("Profile geometry is missing.");

        return type.ToLowerInvariant() switch
        {
            "circle" => CadCircleEntity.ReadGeometry(geometry),
            "rectangle" => CadRectangleEntity.ReadGeometry(geometry),
            "polygon" => CadPolygonEntity.ReadGeometry(geometry),
            "regularpolygon" => CadRegularPolygonEntity.ReadGeometry(geometry),
            "polyline" => ReadClosedPolyline(geometry),
            _ => throw new FormatException(
                $"Unsupported profile type '{type}'.")
        };
    }

    private static CadEntity ReadClosedPolyline(JsonObject data)
    {
        var value = CadPolylineEntity.ReadGeometry(data);
        if (!value.Closed)
            throw new FormatException(
                "Region and feature profiles require a closed polyline.");
        return value;
    }

    private static OcctPoint3d Average(
        IReadOnlyList<OcctPoint3d> points)
    {
        if (points.Count == 0)
            throw new ArgumentException(
                "Profile point collection is empty.",
                nameof(points));

        var x = 0.0;
        var y = 0.0;
        var z = 0.0;
        foreach (var point in points)
        {
            x += point.X;
            y += point.Y;
            z += point.Z;
        }

        var scale = 1.0 / points.Count;
        return new OcctPoint3d(
            x * scale,
            y * scale,
            z * scale);
    }

    private static OcctVector3d PolygonNormal(
        IReadOnlyList<OcctPoint3d> points)
    {
        if (points.Count < 3)
            throw new ArgumentException(
                "Profile requires at least three points.",
                nameof(points));

        var origin = points[0];
        for (var first = 1; first < points.Count - 1; first++)
        {
            var a = CadTransformMath.Between(origin, points[first]);
            for (var second = first + 1; second < points.Count; second++)
            {
                var b = CadTransformMath.Between(origin, points[second]);
                var cross = a.Cross(b);
                if (cross.TryNormalize(out var normal))
                    return normal;
            }
        }

        throw new ArgumentException(
            "Profile points are collinear.",
            nameof(points));
    }
}
