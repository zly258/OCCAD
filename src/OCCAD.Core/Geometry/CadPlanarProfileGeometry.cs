using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

internal static class CadPlanarProfileGeometry
{
    private const double PlaneTolerance = 1e-7;

    internal static bool IsSource(CadEntity entity) =>
        entity is CadCircleEntity or
        CadEllipseEntity or
        CadRectangleEntity or
        CadPolygonEntity or
        CadRegularPolygonEntity or
        CadPolylineEntity { Closed: true } or
        CadPathEntity { Closed: true };

    internal static bool IsSupported(CadEntity entity) =>
        IsSource(entity) || entity is CadRegionEntity;

    internal static bool IsWireProfile(CadEntity entity) =>
        entity switch
        {
            CadCircleEntity => true,
            CadEllipseEntity => true,
            CadRectangleEntity => true,
            CadPolygonEntity => true,
            CadRegularPolygonEntity => true,
            CadPolylineEntity { Closed: true } => true,
            CadPathEntity { Closed: true } => true,
            CadRegionEntity region => region.HoleCount == 0,
            _ => false
        };

    internal static CadEntity WireProfileSnapshot(CadEntity entity)
    {
        if (!IsWireProfile(entity))
            throw new ArgumentException(
                "Entity is not a supported wire profile.",
                nameof(entity));

        return entity switch
        {
            CadRegionEntity region =>
                region.CreateWorldGeometrySnapshot<CadRegionEntity>()
                    .OuterSnapshot(),
            _ => entity.CreateWorldGeometrySnapshot()
        };
    }

    internal static CadEntity Snapshot(CadEntity entity) =>
        entity switch
        {
            CadRegionEntity region => region.CreateWorldGeometrySnapshot(),
            _ when IsSource(entity) => entity.CreateWorldGeometrySnapshot(),
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

        using var model = new OcctModelingSession();

        OcctModelShape face;
        if (profile is CadRegionEntity region)
        {
            var outer = BuildWire(model, region.OuterSnapshot());
            var holes = region.HoleSnapshots()
                .Select(hole => BuildWire(model, hole))
                .ToArray();

            face = model.MakePlanarFace(outer, holes);
        }
        else
        {
            var outer = BuildWire(model, profile);
            face = model.MakePlanarFace(outer);
        }

        return engine.CreateShapeFromModel(model, face);
    }

    internal static OcctModelShape BuildWire(
        OcctModelingSession model,
        CadEntity profile)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(profile);

        return profile switch
        {
            CadCircleEntity circle =>
                model.MakeWire(
                    [
                        model.MakeCircle(
                            circle.Center,
                            circle.Normal,
                            circle.Radius)
                    ]),

            CadEllipseEntity ellipse =>
                model.MakeWire(
                    [
                        BuildEllipseEdge(model, ellipse)
                    ]),

            CadRectangleEntity rectangle =>
                model.MakePolyline(
                    rectangle.CornerPoints,
                    closed: true),

            CadPolygonEntity polygon =>
                model.MakePolyline(
                    polygon.Points,
                    closed: true),

            CadRegularPolygonEntity polygon =>
                model.MakePolyline(
                    polygon.VertexPoints,
                    closed: true),

            CadPolylineEntity { Closed: true } polyline =>
                model.MakePolyline(
                    polyline.Points,
                    closed: true),

            CadPathEntity { Closed: true } path =>
                BuildPathWire(model, path),

            _ => throw new ArgumentException(
                "Entity is not a supported closed wire profile.",
                nameof(profile))
        };
    }

    internal static OcctPoint3d Center(CadEntity profile) =>
        profile switch
        {
            CadCircleEntity circle => circle.Center,
            CadEllipseEntity ellipse => ellipse.Center,
            CadRectangleEntity rectangle => rectangle.Center,
            CadRegularPolygonEntity polygon => polygon.Center,
            CadPolygonEntity polygon => Average(polygon.Points),
            CadPolylineEntity { Closed: true } polyline => Average(polyline.Points),
            CadPathEntity { Closed: true } path => PathCenter(path),
            CadRegionEntity region => Center(region.OuterSnapshot()),
            _ => throw new ArgumentException(
                "Entity is not a supported planar profile.",
                nameof(profile))
        };

    internal static OcctVector3d Normal(CadEntity profile) =>
        profile switch
        {
            CadCircleEntity circle => circle.Normal,
            CadEllipseEntity ellipse => ellipse.Normal,
            CadRectangleEntity rectangle =>
                rectangle.XAxis.Cross(rectangle.YAxis).Normalized(),
            CadRegularPolygonEntity polygon => polygon.Normal,
            CadPolygonEntity polygon => PolygonNormal(polygon.Points),
            CadPolylineEntity { Closed: true } polyline =>
                PolygonNormal(polyline.Points),
            CadPathEntity { Closed: true } path =>
                PathNormal(path),
            CadRegionEntity region =>
                Normal(region.OuterSnapshot()),
            _ => throw new ArgumentException(
                "Entity is not a supported planar profile.",
                nameof(profile))
        };

    internal static bool AreCoplanar(
        CadEntity outer,
        IEnumerable<CadEntity> holes)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(holes);

        if (!IsSource(outer))
            return false;

        var origin = Center(outer);
        var normal = Normal(outer).Normalized();

        foreach (var hole in holes)
        {
            if (!IsSource(hole))
                return false;

            var holeNormal = Normal(hole).Normalized();
            if (Math.Abs(
                    Math.Abs(CadTransformMath.Dot(normal, holeNormal)) -
                    1.0) >
                PlaneTolerance)
                return false;

            foreach (var point in RepresentativePoints(hole))
            {
                var distance = Math.Abs(
                    CadTransformMath.Dot(
                        CadTransformMath.Between(origin, point),
                        normal));
                if (distance > PlaneTolerance)
                    return false;
            }
        }

        return true;
    }

    internal static JsonObject Write(CadEntity profile)
    {
        var (id, geometry) = profile switch
        {
            CadCircleEntity value =>
                ("circle", CadCircleEntity.WriteGeometry(value)),
            CadEllipseEntity value =>
                ("ellipse", CadEllipseEntity.WriteGeometry(value)),
            CadRectangleEntity value =>
                ("rectangle", CadRectangleEntity.WriteGeometry(value)),
            CadPolygonEntity value =>
                ("polygon", CadPolygonEntity.WriteGeometry(value)),
            CadRegularPolygonEntity value =>
                ("regularpolygon", CadRegularPolygonEntity.WriteGeometry(value)),
            CadPolylineEntity { Closed: true } value =>
                ("polyline", CadPolylineEntity.WriteGeometry(value)),
            CadPathEntity { Closed: true } value =>
                ("path", CadPathEntity.WriteGeometry(value)),
            CadRegionEntity value =>
                ("region", CadRegionEntity.WriteGeometry(value)),
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
            "ellipse" => CadEllipseEntity.ReadGeometry(geometry),
            "rectangle" => CadRectangleEntity.ReadGeometry(geometry),
            "polygon" => CadPolygonEntity.ReadGeometry(geometry),
            "regularpolygon" => CadRegularPolygonEntity.ReadGeometry(geometry),
            "polyline" => ReadClosedPolyline(geometry),
            "path" => ReadClosedPath(geometry),
            "region" => CadRegionEntity.ReadGeometry(geometry),
            _ => throw new FormatException(
                $"Unsupported profile type '{type}'.")
        };
    }

    private static OcctModelShape BuildEllipseEdge(
        OcctModelingSession model,
        CadEllipseEntity ellipse)
    {
        var edge = model.MakeEllipse(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            ellipse.MajorRadius,
            ellipse.MinorRadius);

        var yAxis =
            ellipse.Normal.Cross(ellipse.XAxis).Normalized();

        if (CadTransformMath.TryGetAxisAngle(
                ellipse.XAxis,
                yAxis,
                ellipse.Normal,
                out var axis,
                out var angle))
        {
            edge = model.Rotate(
                edge,
                OcctPoint3d.Origin,
                axis,
                angle);
        }

        if (ellipse.Center != OcctPoint3d.Origin)
        {
            edge = model.Translate(
                edge,
                new OcctVector3d(
                    ellipse.Center.X,
                    ellipse.Center.Y,
                    ellipse.Center.Z));
        }

        return edge;
    }

    private static OcctModelShape BuildPathWire(
        OcctModelingSession model,
        CadPathEntity path)
    {
        var edges = path.Segments
            .Select(segment =>
                segment switch
                {
                    CadLineSegment line =>
                        model.MakeLine(line.Start, line.End),
                    CadArcSegment arc =>
                        model.MakeArc(
                            arc.Start,
                            arc.Middle,
                            arc.End),
                    _ => throw new InvalidOperationException(
                        "Path contains an unsupported segment.")
                })
            .ToArray();

        return model.MakeWire(edges);
    }

    private static CadEntity ReadClosedPolyline(JsonObject data)
    {
        var value = CadPolylineEntity.ReadGeometry(data);
        if (!value.Closed)
            throw new FormatException(
                "Feature profile requires a closed polyline.");
        return value;
    }

    private static CadEntity ReadClosedPath(JsonObject data)
    {
        var value = CadPathEntity.ReadGeometry(data);
        if (!value.Closed)
            throw new FormatException(
                "Feature profile requires a closed path.");
        return value;
    }

    private static OcctPoint3d PathCenter(CadPathEntity path) =>
        Average(
            path.Segments
                .SelectMany(segment =>
                    segment switch
                    {
                        CadLineSegment line =>
                            new[] { line.Start, line.End },
                        CadArcSegment arc =>
                            new[] { arc.Start, arc.Middle, arc.End },
                        _ => Array.Empty<OcctPoint3d>()
                    })
                .ToArray());

    private static OcctVector3d PathNormal(CadPathEntity path)
    {
        var arcNormals = path.Segments
            .OfType<CadArcSegment>()
            .Select(static arc => arc.Normal.Normalized())
            .ToArray();

        if (arcNormals.Length > 0)
        {
            var reference = arcNormals[0];
            if (arcNormals.Any(normal =>
                    Math.Abs(
                        Math.Abs(CadTransformMath.Dot(reference, normal)) -
                        1.0) >
                    PlaneTolerance))
            {
                throw new ArgumentException(
                    "Path arc segments are not coplanar.",
                    nameof(path));
            }

            var origin = path.Start;
            foreach (var point in RepresentativePoints(path))
            {
                var distance = Math.Abs(
                    CadTransformMath.Dot(
                        CadTransformMath.Between(origin, point),
                        reference));
                if (distance > PlaneTolerance)
                    throw new ArgumentException(
                        "Path segments are not coplanar.",
                        nameof(path));
            }

            return reference;
        }

        return PolygonNormal(
            path.Segments
                .Select(CadPathEntity.SegmentStart)
                .ToArray());
    }

    private static IReadOnlyList<OcctPoint3d> RepresentativePoints(
        CadEntity profile) =>
        profile switch
        {
            CadCircleEntity circle =>
                circle.GetSnapPoints()
                    .Select(static snap => snap.Position)
                    .ToArray(),
            CadEllipseEntity ellipse =>
                ellipse.GetSnapPoints()
                    .Select(static snap => snap.Position)
                    .ToArray(),
            CadRectangleEntity rectangle => rectangle.CornerPoints,
            CadRegularPolygonEntity polygon => polygon.VertexPoints,
            CadPolygonEntity polygon => polygon.Points,
            CadPolylineEntity polyline => polyline.Points,
            CadPathEntity path =>
                path.Segments
                    .SelectMany(segment =>
                        segment switch
                        {
                            CadLineSegment line =>
                                new[] { line.Start, line.End },
                            CadArcSegment arc =>
                                new[] { arc.Start, arc.Middle, arc.End },
                            _ => Array.Empty<OcctPoint3d>()
                        })
                    .ToArray(),
            _ => Array.Empty<OcctPoint3d>()
        };

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
