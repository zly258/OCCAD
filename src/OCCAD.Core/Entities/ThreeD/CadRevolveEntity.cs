using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadRevolveEntity : CadEntity
{
    private CadEntity _profile;
    private OcctPoint3d _axisPoint;
    private OcctVector3d _axisDirection;
    private double _angleDegrees;

    public CadRevolveEntity(
        CadEntity profile,
        OcctPoint3d axisPoint,
        OcctVector3d axisDirection,
        double angleDegrees) : base("Revolve")
    {
        _profile = CadPlanarProfileGeometry.Snapshot(profile);
        if (!axisPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(axisPoint));
        _axisDirection = CadTransformMath.Normalize(
            axisDirection,
            nameof(axisDirection));
        if (!double.IsFinite(angleDegrees) ||
            Math.Abs(angleDegrees) <= 1e-9 ||
            Math.Abs(angleDegrees) > 360.0)
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));

        _axisPoint = axisPoint;
        _angleDegrees = angleDegrees;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Browsable(false)]
    public string ProfileType => _profile.EntityType;

    [Browsable(false)]
    public OcctPoint3d AxisPoint => _axisPoint;

    [Browsable(false)]
    public OcctVector3d AxisDirection => _axisDirection;

    [Category("Geometry")]
    public double AngleDegrees
    {
        get => _angleDegrees;
        set
        {
            if (!double.IsFinite(value) ||
                Math.Abs(value) <= 1e-9 ||
                Math.Abs(value) > 360.0)
                throw new ArgumentOutOfRangeException(nameof(value));
            SetGeometry(ref _angleDegrees, value);
        }
    }

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var face =
            CadPlanarProfileGeometry.BuildFace(
                engine,
                _profile);
        try
        {
            return engine.Revolve(
                face,
                _axisPoint,
                _axisDirection,
                _angleDegrees,
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
        var center = CadPlanarProfileGeometry.Center(_profile);
        return
        [
            new(this, center, CadSnapType.Center, 0),
            new(this, _axisPoint, CadSnapType.Endpoint, 1)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var center = CadPlanarProfileGeometry.Center(_profile);
        return
        [
            new(this, 0, center, Kind: CadGripKind.Center),
            new(this, 1, _axisPoint, Kind: CadGripKind.Axis),
            new(
                this,
                2,
                _axisPoint + _axisDirection,
                ConstraintOrigin: _axisPoint,
                PrecisionInputs: CadPrecisionInputKind.Angle,
                Kind: CadGripKind.Axis)
        ];
    }

    public override void MoveGrip(
        int index,
        OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        switch (index)
        {
            case 0:
            {
                var center = CadPlanarProfileGeometry.Center(_profile);
                var displacement =
                    CadTransformMath.Between(center, targetPoint);
                _profile.Translate(displacement);
                _axisPoint += displacement;
                break;
            }

            case 1:
                _axisPoint = targetPoint;
                break;

            case 2:
            {
                var direction =
                    CadTransformMath.Between(
                        _axisPoint,
                        targetPoint);
                if (!direction.TryNormalize(out var normalized))
                    return;
                _axisDirection = normalized;
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadRevolveEntity(
                _profile,
                _axisPoint,
                _axisDirection,
                _angleDegrees));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadRevolveEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _profile = value._profile.Duplicate();
        _axisPoint = value._axisPoint;
        _axisDirection = value._axisDirection;
        _angleDegrees = value._angleDegrees;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        _profile.Translate(displacement);
        _axisPoint += displacement;
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _profile.Rotate(center, axis, angleDegrees);
        _axisPoint =
            CadTransformMath.RotatePoint(
                _axisPoint,
                center,
                axis,
                angleDegrees);
        _axisDirection =
            CadTransformMath.RotateVector(
                _axisDirection,
                axis,
                angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _profile.Scale(center, factor);
        _axisPoint =
            CadTransformMath.ScalePoint(
                _axisPoint,
                center,
                factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadRevolveEntity entity) =>
        new()
        {
            ["profile"] =
                CadPlanarProfileGeometry.Write(entity._profile),
            ["axisPoint"] =
                CadEntityJson.Point(entity._axisPoint),
            ["axisDirection"] =
                CadEntityJson.Vector(entity._axisDirection),
            ["angleDegrees"] = entity._angleDegrees
        };

    internal static CadRevolveEntity ReadGeometry(JsonObject data) =>
        new(
            CadPlanarProfileGeometry.Read(
                data["profile"] as JsonObject ??
                throw new FormatException(
                    "Revolve profile is missing.")),
            CadEntityJson.ReadPoint(data, "axisPoint"),
            CadEntityJson.ReadVector(data, "axisDirection"),
            CadEntityJson.ReadDouble(data, "angleDegrees"));
}
