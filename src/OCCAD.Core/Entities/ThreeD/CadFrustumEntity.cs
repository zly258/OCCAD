using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadFrustumEntity : CadEntity
{
    private OcctPoint3d _origin;
    private OcctVector3d _axis;
    private double _baseRadius;
    private double _topRadius;
    private double _height;

    public CadFrustumEntity(OcctPoint3d origin, double baseRadius, double topRadius, double height)
        : this(origin, OcctVector3d.UnitZ, baseRadius, topRadius, height)
    {
    }

    internal CadFrustumEntity(OcctPoint3d origin, OcctVector3d axis, double baseRadius, double topRadius, double height) : base("Frustum")
    {
        if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin));
        _axis = CadTransformMath.Normalize(axis, nameof(axis));
        ValidateRadius(baseRadius, nameof(baseRadius));
        ValidateRadius(topRadius, nameof(topRadius));
        ValidatePositive(height, nameof(height));
        _origin = origin;
        _baseRadius = baseRadius;
        _topRadius = topRadius;
        _height = height;
    }

    [Browsable(false)] public OcctPoint3d Origin => _origin;
    [Browsable(false)] public OcctVector3d Axis => _axis;
    [Category("Orientation"), ReadOnly(true)] public double AxisX => _axis.X;
    [Category("Orientation"), ReadOnly(true)] public double AxisY => _axis.Y;
    [Category("Orientation"), ReadOnly(true)] public double AxisZ => _axis.Z;
    [Category("Geometry")] public double X { get => _origin.X; set => SetOrigin(value, _origin.Y, _origin.Z); }
    [Category("Geometry")] public double Y { get => _origin.Y; set => SetOrigin(_origin.X, value, _origin.Z); }
    [Category("Geometry")] public double Z { get => _origin.Z; set => SetOrigin(_origin.X, _origin.Y, value); }
    [Category("Geometry")] public double BaseRadius { get => _baseRadius; set { ValidateRadius(value, nameof(value)); SetGeometry(ref _baseRadius, value); } }
    [Category("Geometry")] public double TopRadius { get => _topRadius; set { ValidateRadius(value, nameof(value)); SetGeometry(ref _topRadius, value); } }
    [Category("Geometry")] public double Height { get => _height; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _height, value); } }

    [Category("Measurement"), ReadOnly(true)]
    public double SurfaceArea
    {
        get
        {
            var dr = _baseRadius - _topRadius;
            var slant = Math.Sqrt(_height * _height + dr * dr);
            return Math.PI * ((_baseRadius + _topRadius) * slant + _baseRadius * _baseRadius + _topRadius * _topRadius);
        }
    }

    [Category("Measurement"), ReadOnly(true)]
    public double Volume => Math.PI * _height * (_baseRadius * _baseRadius + _baseRadius * _topRadius + _topRadius * _topRadius) / 3.0;

    internal override OcctShape BuildShape(OcctEngine engine) => engine.MakeCone(_origin, _axis, _baseRadius, _topRadius, _height);

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var top = CadTransformMath.Add(_origin, _axis, _height);
        var (xAxis, yAxis) = CadTransformMath.PerpendicularAxes(_axis);
        var bottomPlane = new CadSnapWorkPlane(_origin, xAxis, yAxis);
        var topPlane = new CadSnapWorkPlane(top, xAxis, yAxis);
        return
        [
            new(this, _origin, CadSnapType.Center, 0, bottomPlane),
            new(this, top, CadSnapType.Center, 1, topPlane),
            new(this, CadTransformMath.Add(_origin, xAxis, _baseRadius), CadSnapType.Quadrant, 2, bottomPlane),
            new(this, CadTransformMath.Add(_origin, yAxis, _baseRadius), CadSnapType.Quadrant, 3, bottomPlane),
            new(this, CadTransformMath.Add(_origin, xAxis, -_baseRadius), CadSnapType.Quadrant, 4, bottomPlane),
            new(this, CadTransformMath.Add(_origin, yAxis, -_baseRadius), CadSnapType.Quadrant, 5, bottomPlane),
            new(this, CadTransformMath.Add(top, xAxis, _topRadius), CadSnapType.Quadrant, 6, topPlane),
            new(this, CadTransformMath.Add(top, yAxis, _topRadius), CadSnapType.Quadrant, 7, topPlane),
            new(this, CadTransformMath.Add(top, xAxis, -_topRadius), CadSnapType.Quadrant, 8, topPlane),
            new(this, CadTransformMath.Add(top, yAxis, -_topRadius), CadSnapType.Quadrant, 9, topPlane)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var top = CadTransformMath.Add(_origin, _axis, _height);
        var center = CadTransformMath.Add(_origin, _axis, _height * 0.5);
        var (xAxis, yAxis) = CadTransformMath.PerpendicularAxes(_axis);
        var bottomVertical = new CadGripWorkPlane(_origin, new OcctVector3d(-_axis.X, -_axis.Y, -_axis.Z), xAxis, true, 0.0);
        var topVertical = new CadGripWorkPlane(top, _axis, xAxis, true, 0.0);
        var bottomPlane = new CadGripWorkPlane(_origin, xAxis, yAxis);
        var topPlane = new CadGripWorkPlane(top, xAxis, yAxis);
        return
        [
            new(this, 0, center),
            new(this, 1, _origin, bottomVertical, top, CadPrecisionInputKind.Length),
            new(this, 2, top, topVertical, _origin, CadPrecisionInputKind.Length),
            new(this, 3, CadTransformMath.Add(_origin, xAxis, _baseRadius), bottomPlane, _origin, CadPrecisionInputKind.Length),
            new(this, 4, CadTransformMath.Add(_origin, yAxis, _baseRadius), bottomPlane, _origin, CadPrecisionInputKind.Length),
            new(this, 5, CadTransformMath.Add(_origin, xAxis, -_baseRadius), bottomPlane, _origin, CadPrecisionInputKind.Length),
            new(this, 6, CadTransformMath.Add(_origin, yAxis, -_baseRadius), bottomPlane, _origin, CadPrecisionInputKind.Length),
            new(this, 7, CadTransformMath.Add(top, xAxis, _topRadius), topPlane, top, CadPrecisionInputKind.Length),
            new(this, 8, CadTransformMath.Add(top, yAxis, _topRadius), topPlane, top, CadPrecisionInputKind.Length),
            new(this, 9, CadTransformMath.Add(top, xAxis, -_topRadius), topPlane, top, CadPrecisionInputKind.Length),
            new(this, 10, CadTransformMath.Add(top, yAxis, -_topRadius), topPlane, top, CadPrecisionInputKind.Length)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        var top = CadTransformMath.Add(_origin, _axis, _height);
        switch (index)
        {
            case 0: Translate(CadTransformMath.Between(CadTransformMath.Add(_origin, _axis, _height * 0.5), targetPoint)); return;
            case 1:
                {
                    var offset = CadTransformMath.Dot(CadTransformMath.Between(_origin, targetPoint), _axis);
                    var height = _height - offset;
                    if (height <= 1e-9) return;
                    _origin = CadTransformMath.Add(_origin, _axis, offset);
                    _height = height;
                    break;
                }
            case 2:
                {
                    var height = _height + CadTransformMath.Dot(CadTransformMath.Between(top, targetPoint), _axis);
                    if (height <= 1e-9) return;
                    _height = height;
                    break;
                }
            case >= 3 and <= 6:
                {
                    var radius = RadiusFromAxis(_origin, targetPoint, _axis);
                    if (radius <= 1e-9) return;
                    _baseRadius = radius;
                    break;
                }
            case >= 7 and <= 10:
                {
                    var radius = RadiusFromAxis(top, targetPoint, _axis);
                    if (radius <= 1e-9) return;
                    _topRadius = radius;
                    break;
                }
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() => CopyPropertiesTo(new CadFrustumEntity(_origin, _axis, _baseRadius, _topRadius, _height));
    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadFrustumEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _origin = value._origin; _axis = value._axis; _baseRadius = value._baseRadius; _topRadius = value._topRadius; _height = value._height;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }
    public override void Translate(OcctVector3d displacement) { ValidateDisplacement(displacement); _origin = Translated(_origin, displacement); RaiseGeometryChanged(nameof(Translate)); }
    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees) { _origin = CadTransformMath.RotatePoint(_origin, center, axis, angleDegrees); _axis = CadTransformMath.RotateVector(_axis, axis, angleDegrees).Normalized(); RaiseGeometryChanged(nameof(Rotate)); }
    public override void Scale(OcctPoint3d center, double factor) { CadTransformMath.ValidateScale(factor); _origin = CadTransformMath.ScalePoint(_origin, center, factor); _baseRadius *= factor; _topRadius *= factor; _height *= factor; RaiseGeometryChanged(nameof(Scale)); }
    private void SetOrigin(double x, double y, double z) { ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); SetGeometry(ref _origin, new OcctPoint3d(x, y, z)); }
    private static double RadiusFromAxis(OcctPoint3d center, OcctPoint3d point, OcctVector3d axis)
    {
        var delta = CadTransformMath.Between(center, point); var axial = CadTransformMath.Dot(delta, axis);
        var perpendicular = new OcctVector3d(delta.X - axis.X * axial, delta.Y - axis.Y * axial, delta.Z - axis.Z * axial);
        return Math.Sqrt(perpendicular.LengthSquared);
    }
    private static void ValidateRadius(double value, string parameterName) { if (!double.IsFinite(value) || value <= 1e-12) throw new ArgumentOutOfRangeException(parameterName); }
    internal static JsonObject WriteGeometry(CadFrustumEntity entity) => new() { ["origin"] = CadEntityJson.Point(entity.Origin), ["axis"] = CadEntityJson.Vector(entity.Axis), ["baseRadius"] = entity.BaseRadius, ["topRadius"] = entity.TopRadius, ["height"] = entity.Height };
    internal static CadFrustumEntity ReadGeometry(JsonObject data) => new(CadEntityJson.ReadPoint(data, "origin"), CadEntityJson.ReadVector(data, "axis"), CadEntityJson.ReadDouble(data, "baseRadius"), CadEntityJson.ReadDouble(data, "topRadius"), CadEntityJson.ReadDouble(data, "height"));
}
