using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadSweepEntity : CadEntity
{
    private CadEntity _profile;
    private CadPolylineEntity _path;

    public CadSweepEntity(
        CadEntity profile,
        CadPolylineEntity path) : base("Sweep")
    {
        _profile = CadPlanarProfileGeometry.Snapshot(profile);
        if (path.Closed)
            throw new ArgumentException(
                "Sweep path must be an open polyline.",
                nameof(path));
        _path = (CadPolylineEntity)path.Duplicate();
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Browsable(false)]
    public string ProfileType => _profile.EntityType;

    [Category("Geometry"), ReadOnly(true)]
    public int PathPointCount => _path.Points.Count;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var spine = _path.BuildShape(engine);
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
        _path =
            (CadPolylineEntity)value._path.Duplicate();
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
            ["path"] =
                CadPolylineEntity.WriteGeometry(entity._path)
        };

    internal static CadSweepEntity ReadGeometry(
        JsonObject data)
    {
        var path =
            CadPolylineEntity.ReadGeometry(
                data["path"] as JsonObject ??
                throw new FormatException(
                    "Sweep path is missing."));
        if (path.Closed)
            throw new FormatException(
                "Sweep path must be open.");

        return new CadSweepEntity(
            CadPlanarProfileGeometry.Read(
                data["profile"] as JsonObject ??
                throw new FormatException(
                    "Sweep profile is missing.")),
            path);
    }
}
