using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadRevolveEntity : CadFeatureEntity
{
    private CadEntity _profile;
    private Guid? _profileSourceId;
    private Guid? _axisSourceId;
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

    public override IReadOnlyList<CadFeatureInputDescriptor> Inputs
    {
        get
        {
            var result = new List<CadFeatureInputDescriptor>(2)
            {
                _profileSourceId is { } profileId
                    ? SourceInput("Profile", _profile, profileId)
                    : CapturedInput("Profile", _profile)
            };

            if (_axisSourceId is { } axisId)
            {
                result.Add(
                    new CadFeatureInputDescriptor(
                        "Axis",
                        "Line",
                        CadFeatureInputMode.SourceReference,
                        axisId));
            }

            return result;
        }
    }

    [Category("Geometry"), ReadOnly(true)]
    public int ProfileHoleCount =>
        _profile is CadRegionEntity region
            ? region.HoleCount
            : 0;

    [Browsable(false)]
    public OcctPoint3d AxisPoint => _axisPoint;

    [Browsable(false)]
    public OcctVector3d AxisDirection => _axisDirection;

    [Category("Geometry"), ReadOnly(true)]
    public double AxisPointX => _axisPoint.X;

    [Category("Geometry"), ReadOnly(true)]
    public double AxisPointY => _axisPoint.Y;

    [Category("Geometry"), ReadOnly(true)]
    public double AxisPointZ => _axisPoint.Z;

    [Category("Orientation"), ReadOnly(true)]
    public double AxisDirectionX => _axisDirection.X;

    [Category("Orientation"), ReadOnly(true)]
    public double AxisDirectionY => _axisDirection.Y;

    [Category("Orientation"), ReadOnly(true)]
    public double AxisDirectionZ => _axisDirection.Z;

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

    internal void BindSources(
        CadEntity profile,
        CadLineEntity axis)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(axis);
        if (!CadPlanarProfileGeometry.IsSupported(profile))
            throw new ArgumentException(
                "Revolve profile must be a supported planar profile.",
                nameof(profile));

        _profileSourceId = profile.Id;
        _axisSourceId = axis.Id;
    }

    internal override bool RefreshSourceReferences(
        CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_profileSourceId is not { } profileId ||
            _axisSourceId is not { } axisId ||
            document.FindById(profileId) is not { } profile ||
            document.FindById(axisId) is not CadLineEntity axis ||
            !CadPlanarProfileGeometry.IsSupported(profile))
            return false;

        var axisStart =
            axis.ToWorldPoint(axis.Start);
        var axisEnd =
            axis.ToWorldPoint(axis.End);
        var direction =
            CadTransformMath.Between(
                axisStart,
                axisEnd);
        if (!direction.TryNormalize(out var normalized))
            return false;

        _profile =
            CadPlanarProfileGeometry.Snapshot(profile);
        _axisPoint = axisStart;
        _axisDirection = normalized;
        RaiseGeometryChanged(nameof(Inputs));
        return true;
    }

    protected override OcctShape BuildFeatureResult(OcctEngine engine)
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
                _profileSourceId = null;
                _axisSourceId = null;
                var center = CadPlanarProfileGeometry.Center(_profile);
                var displacement =
                    CadTransformMath.Between(center, targetPoint);
                _profile.Translate(displacement);
                _axisPoint += displacement;
                break;
            }

            case 1:
                _axisSourceId = null;
                _axisPoint = targetPoint;
                break;

            case 2:
            {
                _axisSourceId = null;
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

    public override CadEntity Duplicate()
    {
        var copy = new CadRevolveEntity(
            _profile,
            _axisPoint,
            _axisDirection,
            _angleDegrees)
        {
            _profileSourceId = _profileSourceId,
            _axisSourceId = _axisSourceId
        };
        return CopyPropertiesTo(copy);
    }

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadRevolveEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _profile = value._profile.Duplicate();
        _profileSourceId = value._profileSourceId;
        _axisSourceId = value._axisSourceId;
        _axisPoint = value._axisPoint;
        _axisDirection = value._axisDirection;
        _angleDegrees = value._angleDegrees;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        _profileSourceId = null;
        _axisSourceId = null;
        _profile.Translate(displacement);
        _axisPoint += displacement;
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _profileSourceId = null;
        _axisSourceId = null;
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
        _profileSourceId = null;
        _axisSourceId = null;
        _profile.Scale(center, factor);
        _axisPoint =
            CadTransformMath.ScalePoint(
                _axisPoint,
                center,
                factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadRevolveEntity entity)
    {
        var data = new JsonObject
        {
            ["profile"] =
                CadPlanarProfileGeometry.Write(entity._profile),
            ["axisPoint"] =
                CadEntityJson.Point(entity._axisPoint),
            ["axisDirection"] =
                CadEntityJson.Vector(entity._axisDirection),
            ["angleDegrees"] = entity._angleDegrees
        };

        if (entity._profileSourceId is { } profileId)
            data["profileSourceId"] = JsonValue.Create(profileId);
        if (entity._axisSourceId is { } axisId)
            data["axisSourceId"] = JsonValue.Create(axisId);

        return data;
    }

    internal static CadRevolveEntity ReadGeometry(JsonObject data)
    {
        var entity = new CadRevolveEntity(
            CadPlanarProfileGeometry.Read(
                data["profile"] as JsonObject ??
                throw new FormatException(
                    "Revolve profile is missing.")),
            CadEntityJson.ReadPoint(data, "axisPoint"),
            CadEntityJson.ReadVector(data, "axisDirection"),
            CadEntityJson.ReadDouble(data, "angleDegrees"));
        entity._profileSourceId =
            ReadSourceId(
                data,
                "profileSourceId");
        entity._axisSourceId =
            ReadSourceId(
                data,
                "axisSourceId");
        return entity;
    }
}
