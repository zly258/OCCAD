using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadCylinderEntity : CadEntity
{
    private OcctPoint3d _origin;
    private OcctVector3d _axis;
    private double _radius;
    private double _height;

    public CadCylinderEntity(OcctPoint3d origin, double radius, double height)
        : this(origin, OcctVector3d.UnitZ, radius, height)
    {
    }

    internal CadCylinderEntity(OcctPoint3d origin, OcctVector3d axis, double radius, double height) : base("Cylinder")
    {
        if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin));
        _axis = CadTransformMath.Normalize(axis, nameof(axis));
        ValidatePositive(radius, nameof(radius));
        ValidatePositive(height, nameof(height));
        _origin = origin;
        _radius = radius;
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
    [Category("Geometry")] public double Radius { get => _radius; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _radius, value); } }
    [Category("Geometry")] public double Height { get => _height; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _height, value); } }

    [Category("Geometry"), ReadOnly(true)] public double Diameter => 2.0 * _radius;
    [Category("Measurement"), ReadOnly(true)] public double SurfaceArea => 2.0 * Math.PI * _radius * (_radius + _height);
    [Category("Measurement"), ReadOnly(true)] public double Volume => Math.PI * _radius * _radius * _height;

    internal override OcctShape BuildShape(OcctEngine engine) =>
        engine.MakeCylinder(_origin, _axis, _radius, _height);

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
            new(this, CadTransformMath.Add(_origin, xAxis, _radius), CadSnapType.Quadrant, 2, bottomPlane),
            new(this, CadTransformMath.Add(_origin, yAxis, _radius), CadSnapType.Quadrant, 3, bottomPlane),
            new(this, CadTransformMath.Add(_origin, xAxis, -_radius), CadSnapType.Quadrant, 4, bottomPlane),
            new(this, CadTransformMath.Add(_origin, yAxis, -_radius), CadSnapType.Quadrant, 5, bottomPlane),
            new(this, CadTransformMath.Add(top, xAxis, _radius), CadSnapType.Quadrant, 6, topPlane),
            new(this, CadTransformMath.Add(top, yAxis, _radius), CadSnapType.Quadrant, 7, topPlane),
            new(this, CadTransformMath.Add(top, xAxis, -_radius), CadSnapType.Quadrant, 8, topPlane),
            new(this, CadTransformMath.Add(top, yAxis, -_radius), CadSnapType.Quadrant, 9, topPlane)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var top = CadTransformMath.Add(_origin, _axis, _height);
        var center = CadTransformMath.Add(_origin, _axis, _height * 0.5);
        var (xAxis, yAxis) = CadTransformMath.PerpendicularAxes(_axis);
        var bottomVertical = new CadGripWorkPlane(_origin, new OcctVector3d(-_axis.X, -_axis.Y, -_axis.Z), xAxis, true, 0.0);
        var topVertical = new CadGripWorkPlane(top, _axis, xAxis, true, 0.0);
        var topPlane = new CadGripWorkPlane(top, xAxis, yAxis);
        return
        [
            new(this, 0, center),
            new(this, 1, _origin, bottomVertical, top, CadPrecisionInputKind.Length),
            new(this, 2, top, topVertical, _origin, CadPrecisionInputKind.Length),
            new(this, 3, CadTransformMath.Add(top, xAxis, _radius), topPlane, top, CadPrecisionInputKind.Length),
            new(this, 4, CadTransformMath.Add(top, yAxis, _radius), topPlane, top, CadPrecisionInputKind.Length),
            new(this, 5, CadTransformMath.Add(top, xAxis, -_radius), topPlane, top, CadPrecisionInputKind.Length),
            new(this, 6, CadTransformMath.Add(top, yAxis, -_radius), topPlane, top, CadPrecisionInputKind.Length)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        var grips = GetGripPoints();
        if (index < 0 || index >= grips.Count) throw new ArgumentOutOfRangeException(nameof(index));

        switch (index)
        {
            case 0:
                Translate(CadTransformMath.Between(grips[0].Position, targetPoint));
                return;
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
                    var top = CadTransformMath.Add(_origin, _axis, _height);
                    var height = _height + CadTransformMath.Dot(CadTransformMath.Between(top, targetPoint), _axis);
                    if (height <= 1e-9) return;
                    _height = height;
                    break;
                }
            case >= 3 and <= 6:
                {
                    var top = CadTransformMath.Add(_origin, _axis, _height);
                    var delta = CadTransformMath.Between(top, targetPoint);
                    var axial = CadTransformMath.Dot(delta, _axis);
                    var planar = new OcctVector3d(delta.X - _axis.X * axial, delta.Y - _axis.Y * axial, delta.Z - _axis.Z * axial);
                    var radius = Math.Sqrt(planar.LengthSquared);
                    if (radius <= 1e-9) return;
                    _radius = radius;
                    break;
                }
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() => CopyPropertiesTo(new CadCylinderEntity(_origin, _axis, _radius, _height));
    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadCylinderEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _origin = value._origin;
        _axis = value._axis;
        _radius = value._radius;
        _height = value._height;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }
    public override void Translate(OcctVector3d displacement) { ValidateDisplacement(displacement); _origin = Translated(_origin, displacement); RaiseGeometryChanged(nameof(Translate)); }
    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees) { _origin = CadTransformMath.RotatePoint(_origin, center, axis, angleDegrees); _axis = CadTransformMath.RotateVector(_axis, axis, angleDegrees).Normalized(); RaiseGeometryChanged(nameof(Rotate)); }
    public override void Scale(OcctPoint3d center, double factor) { CadTransformMath.ValidateScale(factor); _origin = CadTransformMath.ScalePoint(_origin, center, factor); _radius *= factor; _height *= factor; RaiseGeometryChanged(nameof(Scale)); }
    private void SetOrigin(double x, double y, double z) { ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); SetGeometry(ref _origin, new OcctPoint3d(x, y, z)); }

    internal static JsonObject WriteGeometry(CadCylinderEntity entity) => new() { ["origin"] = CadEntityJson.Point(entity.Origin), ["axis"] = CadEntityJson.Vector(entity.Axis), ["radius"] = entity.Radius, ["height"] = entity.Height };
    internal static CadCylinderEntity ReadGeometry(JsonObject data) => new(CadEntityJson.ReadPoint(data, "origin"), CadEntityJson.ReadVector(data, "axis"), CadEntityJson.ReadDouble(data, "radius"), CadEntityJson.ReadDouble(data, "height"));
}
