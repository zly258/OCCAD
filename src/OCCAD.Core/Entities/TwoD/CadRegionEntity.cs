using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadRegionEntity : CadEntity
{
    private CadEntity _profile;

    public CadRegionEntity(CadEntity profile) : base("Region")
    {
        _profile = CadPlanarProfileGeometry.Snapshot(profile);
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Browsable(false)]
    public string ProfileType => _profile.EntityType;

    internal CadEntity ProfileSnapshot() =>
        _profile.Duplicate();

    internal override OcctShape BuildShape(OcctEngine engine) =>
        CadPlanarProfileGeometry.BuildFace(engine, _profile);

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
        _profile.GetSnapPoints()
            .Select((snap, index) =>
                new CadSnapPoint(
                    this,
                    snap.Position,
                    snap.Type,
                    index,
                    snap.WorkPlane))
            .ToArray();

    public override IReadOnlyList<CadGripPoint> GetGripPoints() =>
        _profile.GetGripPoints()
            .Select((grip, index) =>
                new CadGripPoint(
                    this,
                    index,
                    grip.Position,
                    grip.WorkPlane,
                    grip.ConstraintOrigin,
                    grip.PrecisionInputs,
                    grip.Kind))
            .ToArray();

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        var grips = _profile.GetGripPoints();
        if ((uint)index >= (uint)grips.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _profile.MoveGrip(grips[index].Index, targetPoint);
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadRegionEntity(_profile));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadRegionEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _profile = value._profile.Duplicate();
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        _profile.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _profile.Rotate(center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        _profile.Scale(center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadRegionEntity entity) =>
        new()
        {
            ["profile"] =
                CadPlanarProfileGeometry.Write(entity._profile)
        };

    internal static CadRegionEntity ReadGeometry(JsonObject data) =>
        new(
            CadPlanarProfileGeometry.Read(
                data["profile"] as JsonObject ??
                throw new FormatException(
                    "Region profile is missing.")));
}
