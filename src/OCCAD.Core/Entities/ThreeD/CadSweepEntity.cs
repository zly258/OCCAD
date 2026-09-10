using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadSweepEntity : CadEntity
{
    private CadEntity _profile;
    private CadEntity _path;

    public CadSweepEntity(
        CadEntity profile,
        CadEntity path) : base("Sweep")
    {
        _profile = CadPlanarProfileGeometry.Snapshot(profile);
        _path = SnapshotPath(path);
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Browsable(false)]
    public string ProfileType => _profile.EntityType;

    [Category("Geometry"), ReadOnly(true)]
    public int PathSegmentCount =>
        _path switch
        {
            CadPolylineEntity polyline => polyline.Points.Count - 1,
            CadPathEntity path => path.SegmentCount,
            CadHelixEntity => 1,
            _ => 0
        };

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var spine = BuildSpine(engine);
        var profile =
            CadPlanarProfileGeometry.BuildFace(
                engine,
                _profile);

        try
        {
            return engine.Sweep(
                spine,
                profile,
                hideInputs: true);
        }
        finally
        {
            if (engine.ContainsObject(spine.Id))
                engine.Delete(spine);
            if (engine.ContainsObject(profile.Id))
                engine.Delete(profile);
        }
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var result = new List<CadSnapPoint>();
        foreach (var snap in _path.GetSnapPoints())
        {
            result.Add(
                new CadSnapPoint(
                    this,
                    snap.Position,
                    snap.Type,
                    result.Count,
                    snap.WorkPlane));
        }

        result.Add(
            new CadSnapPoint(
                this,
                CadPlanarProfileGeometry.Center(_profile),
                CadSnapType.Center,
                result.Count));
        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var result = new List<CadGripPoint>();
        foreach (var grip in _path.GetGripPoints())
        {
            result.Add(
                new CadGripPoint(
                    this,
                    result.Count,
                    grip.Position,
                    grip.WorkPlane,
                    grip.ConstraintOrigin,
                    grip.PrecisionInputs,
                    grip.Kind));
        }

        return result;
    }

    public override void MoveGrip(
        int index,
        OcctPoint3d targetPoint)
    {
        var grips = _path.GetGripPoints();
        if ((uint)index >= (uint)grips.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _path.MoveGrip(
            grips[index].Index,
            targetPoint);
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadSweepEntity(
                _profile,
                _path));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadSweepEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _profile = value._profile.Duplicate();
        _path = SnapshotPath(value._path);
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(
        OcctVector3d displacement)
    {
        _profile.Translate(displacement);
        _path.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _profile.Rotate(
            center,
            axis,
            angleDegrees);
        _path.Rotate(
            center,
            axis,
            angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _profile.Scale(center, factor);
        _path.Scale(center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadSweepEntity entity) =>
        new()
        {
            ["profile"] =
                CadPlanarProfileGeometry.Write(entity._profile),
            ["path"] = WritePath(entity._path)
        };

    internal static CadSweepEntity ReadGeometry(
        JsonObject data)
    {
        return new CadSweepEntity(
            CadPlanarProfileGeometry.Read(
                data["profile"] as JsonObject ??
                throw new FormatException(
                    "Sweep profile is missing.")),
            ReadPath(
                data["path"] as JsonObject ??
                throw new FormatException(
                    "Sweep path is missing.")));
    }

    internal static bool IsPath(CadEntity entity) =>
        entity switch
        {
            CadLineEntity => true,
            CadArcEntity => true,
            CadHelixEntity => true,
            CadPolylineEntity { Closed: false } => true,
            CadPathEntity { Closed: false } => true,
            _ => false
        };

    private static CadEntity SnapshotPath(CadEntity path) =>
        path switch
        {
            CadLineEntity line =>
                new CadPathEntity([line]),
            CadArcEntity arc =>
                new CadPathEntity([arc]),
            CadHelixEntity helix =>
                helix.Duplicate(),
            CadPolylineEntity { Closed: false } polyline =>
                polyline.Duplicate(),
            CadPathEntity { Closed: false } mixed =>
                mixed.Duplicate(),
            _ => throw new ArgumentException(
                "Sweep path must be a line, arc, helix, open polyline, or open path.",
                nameof(path))
        };

    private static JsonObject WritePath(CadEntity path) =>
        path switch
        {
            CadPolylineEntity polyline =>
                new JsonObject
                {
                    ["type"] = "polyline",
                    ["geometry"] =
                        CadPolylineEntity.WriteGeometry(polyline)
                },
            CadPathEntity mixed =>
                new JsonObject
                {
                    ["type"] = "path",
                    ["geometry"] =
                        CadPathEntity.WriteGeometry(mixed)
                },
            CadHelixEntity helix =>
                new JsonObject
                {
                    ["type"] = "helix",
                    ["geometry"] =
                        CadHelixEntity.WriteGeometry(helix)
                },
            _ => throw new InvalidOperationException()
        };

    private OcctShape BuildSpine(OcctEngine engine)
    {
        if (_path is not CadHelixEntity helix)
            return _path.BuildShape(engine);

        using var model = new OcctModelingSession();
        var edge = model.MakeHelix(
            helix.Radius,
            helix.Pitch,
            helix.Turns,
            helix.Origin,
            helix.Axis,
            helix.XAxis);
        var wire = model.MakeWire([edge]);
        return engine.CreateShapeFromModel(model, wire);
    }

    private static CadEntity ReadPath(JsonObject data)
    {
        var type =
            data["type"]?.GetValue<string>() ??
            throw new FormatException(
                "Sweep path type is missing.");
        var geometry =
            data["geometry"] as JsonObject ??
            throw new FormatException(
                "Sweep path geometry is missing.");

        return type.ToLowerInvariant() switch
        {
            "polyline" => CadPolylineEntity.ReadGeometry(geometry),
            "path" => CadPathEntity.ReadGeometry(geometry),
            "helix" => CadHelixEntity.ReadGeometry(geometry),
            _ => throw new FormatException(
                $"Unsupported sweep path type '{type}'.")
        };
    }
}
