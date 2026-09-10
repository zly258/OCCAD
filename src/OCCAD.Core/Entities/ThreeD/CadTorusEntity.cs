using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadTorusEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _axis;
    private double _majorRadius;
    private double _minorRadius;

    public CadTorusEntity(OcctPoint3d center, double majorRadius, double minorRadius)
        : this(center, OcctVector3d.UnitZ, majorRadius, minorRadius)
    {
    }

    internal CadTorusEntity(
        OcctPoint3d center,
        OcctVector3d axis,
        double majorRadius,
        double minorRadius) : base("Torus")
    {
        if (!center.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(center));
        _axis = CadTransformMath.Normalize(axis, nameof(axis));
        ValidateRadii(majorRadius, minorRadius);
        _center = center;
        _majorRadius = majorRadius;
        _minorRadius = minorRadius;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d Axis => _axis;
    [Category("Orientation"), ReadOnly(true)] public double AxisX => _axis.X;
    [Category("Orientation"), ReadOnly(true)] public double AxisY => _axis.Y;
    [Category("Orientation"), ReadOnly(true)] public double AxisZ => _axis.Z;
    [Category("Geometry")] public double X { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double Y { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double Z { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }
    [Category("Geometry")] public double MajorRadius { get => _majorRadius; set { ValidateRadii(value, _minorRadius); SetGeometry(ref _majorRadius, value); } }
    [Category("Geometry")] public double MinorRadius { get => _minorRadius; set { ValidateRadii(_majorRadius, value); SetGeometry(ref _minorRadius, value); } }
    [Category("Measurement"), ReadOnly(true)] public double SurfaceArea => 4.0 * Math.PI * Math.PI * _majorRadius * _minorRadius;
    [Category("Measurement"), ReadOnly(true)] public double Volume => 2.0 * Math.PI * Math.PI * _majorRadius * _minorRadius * _minorRadius;

    internal override OcctShape BuildShape(OcctEngine engine) =>
        engine.MakeTorus(_majorRadius, _minorRadius, _center, _axis);

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var (xAxis, yAxis) = CadTransformMath.PerpendicularAxes(_axis);
        var plane = new CadSnapWorkPlane(_center, xAxis, yAxis);
        return [new(this, _center, CadSnapType.Center, 0, plane)];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var (xAxis, yAxis) = CadTransformMath.PerpendicularAxes(_axis);
        var negativeX = new OcctVector3d(-xAxis.X, -xAxis.Y, -xAxis.Z);
        var negativeY = new OcctVector3d(-yAxis.X, -yAxis.Y, -yAxis.Z);

        var majorX = CadTransformMath.Add(_center, xAxis, _majorRadius);
        var majorY = CadTransformMath.Add(_center, yAxis, _majorRadius);
        var majorNegativeX = CadTransformMath.Add(_center, xAxis, -_majorRadius);
        var majorNegativeY = CadTransformMath.Add(_center, yAxis, -_majorRadius);

        var majorPositiveXPlane = new CadGripWorkPlane(_center, xAxis, yAxis, true, 0.0);
        var majorPositiveYPlane = new CadGripWorkPlane(_center, yAxis, _axis, true, 0.0);
        var majorNegativeXPlane = new CadGripWorkPlane(_center, negativeX, yAxis, true, 0.0);
        var majorNegativeYPlane = new CadGripWorkPlane(_center, negativeY, _axis, true, 0.0);

        return
        [
            new(this, 0, _center, Kind: CadGripKind.Center),
            new(this, 1, majorX, majorPositiveXPlane, _center, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 2, majorY, majorPositiveYPlane, _center, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 3, majorNegativeX, majorNegativeXPlane, _center, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 4, majorNegativeY, majorNegativeYPlane, _center, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 5, CadTransformMath.Add(majorX, xAxis, _minorRadius), new CadGripWorkPlane(majorX, xAxis, _axis, true, 0.0), majorX, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 6, CadTransformMath.Add(majorY, yAxis, _minorRadius), new CadGripWorkPlane(majorY, yAxis, _axis, true, 0.0), majorY, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 7, CadTransformMath.Add(majorNegativeX, negativeX, _minorRadius), new CadGripWorkPlane(majorNegativeX, negativeX, _axis, true, 0.0), majorNegativeX, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 8, CadTransformMath.Add(majorNegativeY, negativeY, _minorRadius), new CadGripWorkPlane(majorNegativeY, negativeY, _axis, true, 0.0), majorNegativeY, CadPrecisionInputKind.Length, CadGripKind.Radius)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var (xAxis, yAxis) = CadTransformMath.PerpendicularAxes(_axis);
        switch (index)
        {
            case 0:
                _center = targetPoint;
                break;

            case >= 1 and <= 4:
                {
                    var radius = RadiusInEquatorialPlane(targetPoint);
                    if (radius <= _minorRadius + 1e-9)
                        return;
                    _majorRadius = radius;
                    break;
                }

            case >= 5 and <= 8:
                {
                    var radial = index switch
                    {
                        5 => xAxis,
                        6 => yAxis,
                        7 => new OcctVector3d(-xAxis.X, -xAxis.Y, -xAxis.Z),
                        _ => new OcctVector3d(-yAxis.X, -yAxis.Y, -yAxis.Z)
                    };
                    var ringCenter = CadTransformMath.Add(
                        _center,
                        radial,
                        _majorRadius);
                    var delta = CadTransformMath.Between(
                        ringCenter,
                        targetPoint);
                    var radialDistance = CadTransformMath.Dot(delta, radial);
                    var axialDistance = CadTransformMath.Dot(delta, _axis);
                    var minor = Math.Sqrt(
                        radialDistance * radialDistance +
                        axialDistance * axialDistance);
                    if (minor <= 1e-9 || minor >= _majorRadius)
                        return;
                    _minorRadius = minor;
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadTorusEntity(
                _center,
                _axis,
                _majorRadius,
                _minorRadius));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadTorusEntity value)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _center = value._center;
        _axis = value._axis;
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
        _axis = CadTransformMath.RotateVector(_axis, axis, angleDegrees).Normalized();
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

    private double RadiusInEquatorialPlane(OcctPoint3d point)
    {
        var delta = CadTransformMath.Between(_center, point);
        var axial = CadTransformMath.Dot(delta, _axis);
        var planar = new OcctVector3d(
            delta.X - _axis.X * axial,
            delta.Y - _axis.Y * axial,
            delta.Z - _axis.Z * axial);
        return Math.Sqrt(planar.LengthSquared);
    }

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    private static void ValidateRadii(double majorRadius, double minorRadius)
    {
        ValidatePositive(majorRadius, nameof(majorRadius));
        ValidatePositive(minorRadius, nameof(minorRadius));
        if (minorRadius >= majorRadius)
            throw new ArgumentException("Minor radius must be less than major radius.");
    }

    internal static JsonObject WriteGeometry(CadTorusEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["axis"] = CadEntityJson.Vector(entity.Axis),
            ["majorRadius"] = entity.MajorRadius,
            ["minorRadius"] = entity.MinorRadius
        };

    internal static CadTorusEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "axis"),
            CadEntityJson.ReadDouble(data, "majorRadius"),
            CadEntityJson.ReadDouble(data, "minorRadius"));
}
