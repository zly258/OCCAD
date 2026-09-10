using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

internal static class CadSolidFeatureGeometry
{
    internal static bool IsSolid(CadEntity entity) =>
        entity is CadBoxEntity or
        CadCylinderEntity or
        CadConeEntity or
        CadFrustumEntity or
        CadSphereEntity or
        CadEllipsoidEntity or
        CadTorusEntity or
        CadExtrudeEntity or
        CadRevolveEntity or
        CadSweepEntity or
        CadLoftEntity or
        CadEdgeFilletEntity or
        CadEdgeChamferEntity or
        CadShellEntity or
        CadShapeOffsetEntity or
        CadImportedShapeEntity or
        CadBooleanEntity;

    internal static CadEntity Snapshot(CadEntity entity) =>
        IsSolid(entity)
            ? entity.Duplicate()
            : throw new ArgumentException(
                "Entity is not a supported solid feature.",
                nameof(entity));

    internal static JsonObject Write(CadEntity entity)
    {
        var (type, geometry) = entity switch
        {
            CadBoxEntity value =>
                ("box", CadBoxEntity.WriteGeometry(value)),
            CadCylinderEntity value =>
                ("cylinder", CadCylinderEntity.WriteGeometry(value)),
            CadConeEntity value =>
                ("cone", CadConeEntity.WriteGeometry(value)),
            CadFrustumEntity value =>
                ("frustum", CadFrustumEntity.WriteGeometry(value)),
            CadSphereEntity value =>
                ("sphere", CadSphereEntity.WriteGeometry(value)),
            CadEllipsoidEntity value =>
                ("ellipsoid", CadEllipsoidEntity.WriteGeometry(value)),
            CadTorusEntity value =>
                ("torus", CadTorusEntity.WriteGeometry(value)),
            CadExtrudeEntity value =>
                ("extrude", CadExtrudeEntity.WriteGeometry(value)),
            CadRevolveEntity value =>
                ("revolve", CadRevolveEntity.WriteGeometry(value)),
            CadSweepEntity value =>
                ("sweep", CadSweepEntity.WriteGeometry(value)),
            CadLoftEntity value =>
                ("loft", CadLoftEntity.WriteGeometry(value)),
            CadEdgeFilletEntity value =>
                ("edgefillet", CadEdgeFilletEntity.WriteGeometry(value)),
            CadEdgeChamferEntity value =>
                ("edgechamfer", CadEdgeChamferEntity.WriteGeometry(value)),
            CadShellEntity value =>
                ("shell", CadShellEntity.WriteGeometry(value)),
            CadShapeOffsetEntity value =>
                ("shapeoffset", CadShapeOffsetEntity.WriteGeometry(value)),
            CadImportedShapeEntity value =>
                ("importedshape", CadImportedShapeEntity.WriteGeometry(value)),
            CadBooleanEntity value =>
                ("boolean", CadBooleanEntity.WriteGeometry(value)),
            _ => throw new ArgumentException(
                "Entity is not a supported solid feature.",
                nameof(entity))
        };

        return new JsonObject
        {
            ["type"] = type,
            ["geometry"] = geometry
        };
    }

    internal static CadEntity Read(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var type =
            data["type"]?.GetValue<string>() ??
            throw new FormatException(
                "Solid feature type is missing.");
        var geometry =
            data["geometry"] as JsonObject ??
            throw new FormatException(
                "Solid feature geometry is missing.");

        return type.ToLowerInvariant() switch
        {
            "box" => CadBoxEntity.ReadGeometry(geometry),
            "cylinder" => CadCylinderEntity.ReadGeometry(geometry),
            "cone" => CadConeEntity.ReadGeometry(geometry),
            "frustum" => CadFrustumEntity.ReadGeometry(geometry),
            "sphere" => CadSphereEntity.ReadGeometry(geometry),
            "ellipsoid" => CadEllipsoidEntity.ReadGeometry(geometry),
            "torus" => CadTorusEntity.ReadGeometry(geometry),
            "extrude" => CadExtrudeEntity.ReadGeometry(geometry),
            "revolve" => CadRevolveEntity.ReadGeometry(geometry),
            "sweep" => CadSweepEntity.ReadGeometry(geometry),
            "loft" => CadLoftEntity.ReadGeometry(geometry),
            "edgefillet" => CadEdgeFilletEntity.ReadGeometry(geometry),
            "edgechamfer" => CadEdgeChamferEntity.ReadGeometry(geometry),
            "shell" => CadShellEntity.ReadGeometry(geometry),
            "shapeoffset" => CadShapeOffsetEntity.ReadGeometry(geometry),
            "importedshape" => CadImportedShapeEntity.ReadGeometry(geometry),
            "boolean" => CadBooleanEntity.ReadGeometry(geometry),
            _ => throw new FormatException(
                $"Unsupported solid feature type '{type}'.")
        };
    }

    internal static OcctPoint3d ApproximateCenter(CadEntity entity)
    {
        var snaps = entity.GetSnapPoints();
        if (snaps.Count == 0)
            return OcctPoint3d.Origin;

        var x = 0.0;
        var y = 0.0;
        var z = 0.0;
        foreach (var snap in snaps)
        {
            x += snap.Position.X;
            y += snap.Position.Y;
            z += snap.Position.Z;
        }

        var scale = 1.0 / snaps.Count;
        return new OcctPoint3d(
            x * scale,
            y * scale,
            z * scale);
    }
}
