using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadExtrudeEntity : CadEntity
{
    private CadEntity _profile;
    private OcctVector3d _vector;

    public CadExtrudeEntity(
        CadEntity profile,
        OcctVector3d vector) : base("Extrude")
    {
        _profile = CadPlanarProfileGeometry.Snapshot(profile);
        if (!vector.TryNormalize(out _))
            throw new ArgumentOutOfRangeException(
                nameof(vector),
                "Extrusion vector must be non-zero.");
        _vector = vector;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Browsable(false)]
    public string ProfileType => _profile.EntityType;

    [Browsable(false)]
    public OcctVector3d Vector => _vector;

    [Category("Geometry"), ReadOnly(true)]
    public double Length => Math.Sqrt(_vector.LengthSquared);

    [Category("Geometry"), ReadOnly(true)]
    public double VectorX => _vector.X;

    [Category("Geometry"), ReadOnly(true)]
    public double VectorY => _vector.Y;

    [Category("Geometry"), ReadOnly(true)]
    public double VectorZ => _vector.Z;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var face =
            CadPlanarProfileGeometry.BuildFace(
                engine,
                _profile);
        try
        {
            return engine.Extrude(
                face,
                _vector,
                hideInput: true);
        }
        finally
        {
            if (engine.ContainsObject(face.Id))
                engine.Delete(face);
        }
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var center =
            CadPlanarProfileGeometry.Center(_profile);
        var top = center + _vector;
        return
        [
            new(this, center, CadSnapType.Center, 0),
            new(this, top, CadSnapType.Center, 1)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var center =
            CadPlanarProfileGeometry.Center(_profile);
        var top = center + _vector;
        return
        [
            new(this, 0, center, Kind: CadGripKind.Center),
            new(
                this,
                1,
                top,
                ConstraintOrigin: center,
                PrecisionInputs: CadPrecisionInputKind.Length,
                Kind: CadGripKind.Height)
        ];
    }

    public override void MoveGrip(
        int index,
        OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var center =
            CadPlanarProfileGeometry.Center(_profile);

        switch (index)
        {
            case 0:
                _profile.Translate(
                    CadTransformMath.Between(
                        center,
                        targetPoint));
                break;

            case 1:
                var vector =
                    CadTransformMath.Between(
                        center,
                        targetPoint);
                if (!vector.TryNormalize(out _))
                    return;
                _vector = vector;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadExtrudeEntity(
                _profile,
                _vector));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadExtrudeEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _profile = value._profile.Duplicate();
        _vector = value._vector;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(
        OcctVector3d displacement)
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
        _vector =
            CadTransformMath.RotateVector(
                _vector,
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
        _vector = _vector * factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadExtrudeEntity entity) =>
        new()
        {
            ["profile"] =
                CadPlanarProfileGeometry.Write(entity._profile),
            ["vector"] =
                CadEntityJson.Vector(entity._vector)
        };

    internal static CadExtrudeEntity ReadGeometry(JsonObject data) =>
        new(
            CadPlanarProfileGeometry.Read(
                data["profile"] as JsonObject ??
                throw new FormatException(
                    "Extrude profile is missing.")),
            CadEntityJson.ReadVector(data, "vector"));
}
