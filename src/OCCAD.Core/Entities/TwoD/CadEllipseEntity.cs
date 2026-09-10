using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadEllipseEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private OcctVector3d _xAxis;
    private double _majorRadius;
    private double _minorRadius;

    public CadEllipseEntity(OcctPoint3d center, OcctVector3d normal, double majorRadius, double minorRadius)
        : this(center, normal, CadTransformMath.PerpendicularAxes(normal).XAxis, majorRadius, minorRadius)
    {
    }

    internal CadEllipseEntity(
        OcctPoint3d center,
        OcctVector3d normal,
        OcctVector3d xAxis,
        double majorRadius,
        double minorRadius) : base("Ellipse")
    {
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        _normal = CadTransformMath.Normalize(normal, nameof(normal));
        _xAxis = OrthogonalizeX(xAxis, _normal);
        ValidateRadii(majorRadius, minorRadius);
        _center = center;
        _majorRadius = majorRadius;
        _minorRadius = minorRadius;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d Normal => _normal;
    [Browsable(false)] public OcctVector3d XAxis => _xAxis;

    [Category("Orientation"), ReadOnly(true)] public double NormalX => _normal.X;
    [Category("Orientation"), ReadOnly(true)] public double NormalY => _normal.Y;
    [Category("Orientation"), ReadOnly(true)] public double NormalZ => _normal.Z;
    [Category("Orientation"), ReadOnly(true)] public double MajorAxisX => _xAxis.X;
    [Category("Orientation"), ReadOnly(true)] public double MajorAxisY => _xAxis.Y;
    [Category("Orientation"), ReadOnly(true)] public double MajorAxisZ => _xAxis.Z;

    [Category("Geometry")] public double CenterX { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double CenterY { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double CenterZ { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }

    [Category("Geometry")]
    public double MajorRadius
    {
        get => _majorRadius;
        set
        {
            ValidateRadii(value, _minorRadius);
            SetGeometry(ref _majorRadius, value);
        }
    }

    [Category("Geometry")]
    public double MinorRadius
    {
        get => _minorRadius;
        set
        {
            ValidateRadii(_majorRadius, value);
            SetGeometry(ref _minorRadius, value);
        }
    }

    [Category("Measurement"), ReadOnly(true)]
    public double Area => Math.PI * _majorRadius * _minorRadius;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var shape = engine.MakeEllipse(OcctPoint3d.Origin, OcctVector3d.UnitZ, _majorRadius, _minorRadius);
        var yAxis = _normal.Cross(_xAxis).Normalized();

        if (CadTransformMath.TryGetAxisAngle(_xAxis, yAxis, _normal, out var axis, out var angle))
        {
            var rotated = engine.Rotate(shape, OcctPoint3d.Origin, axis, angle);
            engine.Delete(shape);
            shape = rotated;
        }

        if (_center != OcctPoint3d.Origin)
        {
            var translated = engine.Translate(shape, new OcctVector3d(_center.X, _center.Y, _center.Z));
            engine.Delete(shape);
            shape = translated;
        }

        return shape;
    }

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        return CadPrecisionSnapGeometry.TryCreateEllipse(
            this,
            workPlane,
            out var curve)
            ? [curve]
            : Array.Empty<CadSnapCurve>();
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var plane = new CadSnapWorkPlane(_center, _xAxis, yAxis);
        return [new(this, _center, CadSnapType.Center, 0, plane)];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var yAxis = _normal.Cross(_xAxis).Normalized();
        return
        [
            new(this, 0, _center, Kind: CadGripKind.Center),
            new(
                this,
                1,
                _center + _xAxis * _majorRadius,
                new CadGripWorkPlane(
                    _center,
                    _xAxis,
                    yAxis,
                    true,
                    0.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                2,
                _center - _xAxis * _majorRadius,
                new CadGripWorkPlane(
                    _center,
                    _xAxis,
                    yAxis,
                    true,
                    180.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                3,
                _center + yAxis * _minorRadius,
                new CadGripWorkPlane(
                    _center,
                    _xAxis,
                    yAxis,
                    true,
                    90.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                4,
                _center - yAxis * _minorRadius,
                new CadGripWorkPlane(
                    _center,
                    _xAxis,
                    yAxis,
                    true,
                    -90.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        switch (index)
        {
            case 0:
                _center = targetPoint;
                break;

            case 1:
            case 2:
                {
                    var major = DistanceAlongAxis(targetPoint, _xAxis);
                    if (major <= 1e-9 || major < _minorRadius) return;
                    _majorRadius = major;
                    break;
                }

            case 3:
            case 4:
                {
                    var yAxis = _normal.Cross(_xAxis).Normalized();
                    var minor = DistanceAlongAxis(targetPoint, yAxis);
                    if (minor <= 1e-9 || minor > _majorRadius) return;
                    _minorRadius = minor;
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadEllipseEntity(_center, _normal, _xAxis, _majorRadius, _minorRadius));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadEllipseEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _center = value._center;
        _normal = value._normal;
        _xAxis = value._xAxis;
        _majorRadius = value._majorRadius;
        _minorRadius = value._minorRadius;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        _center = Translated(_center, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        _center = CadTransformMath.RotatePoint(_center, center, axis, angleDegrees);
        _normal = CadTransformMath.RotateVector(_normal, axis, angleDegrees).Normalized();
        _xAxis = CadTransformMath.RotateVector(_xAxis, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _center = CadTransformMath.ScalePoint(_center, center, factor);
        _majorRadius *= factor;
        _minorRadius *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private double DistanceAlongAxis(OcctPoint3d point, OcctVector3d axis) =>
        Math.Abs(CadTransformMath.Dot(CadTransformMath.Between(_center, point), axis));

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    private static OcctVector3d OrthogonalizeX(OcctVector3d xAxis, OcctVector3d normal)
    {
        var x = CadTransformMath.Normalize(xAxis, nameof(xAxis));
        var dot = CadTransformMath.Dot(x, normal);
        var projected = new OcctVector3d(
            x.X - normal.X * dot,
            x.Y - normal.Y * dot,
            x.Z - normal.Z * dot);
        return CadTransformMath.Normalize(projected, nameof(xAxis));
    }

    private static void ValidateRadii(double major, double minor)
    {
        ValidatePositive(major, nameof(major));
        ValidatePositive(minor, nameof(minor));
        if (minor > major) throw new ArgumentException("Minor radius must not exceed major radius.");
    }

    internal static JsonObject WriteGeometry(CadEllipseEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["normal"] = CadEntityJson.Vector(entity.Normal),
            ["xAxis"] = CadEntityJson.Vector(entity.XAxis),
            ["majorRadius"] = entity.MajorRadius,
            ["minorRadius"] = entity.MinorRadius
        };

    internal static CadEllipseEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "normal"),
            CadEntityJson.ReadVector(data, "xAxis"),
            CadEntityJson.ReadDouble(data, "majorRadius"),
            CadEntityJson.ReadDouble(data, "minorRadius"));
}
