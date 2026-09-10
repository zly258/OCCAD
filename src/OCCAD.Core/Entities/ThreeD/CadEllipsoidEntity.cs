using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;
namespace OCCAD;

public sealed class CadEllipsoidEntity : CadEntity
{
    private OcctPoint3d _center; private OcctVector3d _xAxis, _yAxis, _zAxis; private double _rx, _ry, _rz;
    public CadEllipsoidEntity(OcctPoint3d center, double xRadius, double yRadius, double zRadius) : this(center, OcctVector3d.UnitX, OcctVector3d.UnitY, OcctVector3d.UnitZ, xRadius, yRadius, zRadius) { }
    internal CadEllipsoidEntity(OcctPoint3d c, OcctVector3d x, OcctVector3d y, OcctVector3d z, double rx, double ry, double rz) : base("Ellipsoid")
    { if (!c.IsFinite) throw new ArgumentOutOfRangeException(nameof(c)); ValidateFrame(x, y, z); ValidateRadii(rx, ry, rz); _center = c; _xAxis = x.Normalized(); _yAxis = y.Normalized(); _zAxis = z.Normalized(); _rx = rx; _ry = ry; _rz = rz; }
    [Browsable(false)] public OcctPoint3d Center => _center; [Browsable(false)] public OcctVector3d XAxis => _xAxis; [Browsable(false)] public OcctVector3d YAxis => _yAxis; [Browsable(false)] public OcctVector3d ZAxis => _zAxis;
    [Category("Geometry")] public double X { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double Y { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double Z { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }
    [Category("Geometry")] public double XRadius { get => _rx; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _rx, value); } }
    [Category("Geometry")] public double YRadius { get => _ry; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _ry, value); } }
    [Category("Geometry")] public double ZRadius { get => _rz; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _rz, value); } }
    [Category("Measurement"), ReadOnly(true)] public double Volume => 4 * Math.PI * _rx * _ry * _rz / 3;
    internal override OcctShape BuildShape(OcctEngine engine) { using var model = new OcctModelingSession(); var shape = model.MakeEllipsoid(OcctPoint3d.Origin, _rx, _ry, _rz); if (CadTransformMath.TryGetAxisAngle(_xAxis, _yAxis, _zAxis, out var axis, out var angle)) shape = model.Rotate(shape, OcctPoint3d.Origin, axis, angle); shape = model.Translate(shape, _center - OcctPoint3d.Origin); return engine.CreateShapeFromModel(model, shape); }
    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() => [new(this, _center, CadSnapType.Center, 0), new(this, _center + _xAxis * _rx, CadSnapType.Quadrant, 1), new(this, _center - _xAxis * _rx, CadSnapType.Quadrant, 2), new(this, _center + _yAxis * _ry, CadSnapType.Quadrant, 3), new(this, _center - _yAxis * _ry, CadSnapType.Quadrant, 4), new(this, _center + _zAxis * _rz, CadSnapType.Quadrant, 5), new(this, _center - _zAxis * _rz, CadSnapType.Quadrant, 6)];
    public override IReadOnlyList<CadGripPoint> GetGripPoints() { var p = GetSnapPoints(); return [new(this, 0, _center), new(this, 1, p[1].Position, new(_center, _xAxis, _yAxis, true), _center, CadPrecisionInputKind.Length), new(this, 2, p[2].Position, new(_center, _xAxis * -1, _yAxis, true), _center, CadPrecisionInputKind.Length), new(this, 3, p[3].Position, new(_center, _yAxis, _zAxis, true), _center, CadPrecisionInputKind.Length), new(this, 4, p[4].Position, new(_center, _yAxis * -1, _zAxis, true), _center, CadPrecisionInputKind.Length), new(this, 5, p[5].Position, new(_center, _zAxis, _xAxis, true), _center, CadPrecisionInputKind.Length), new(this, 6, p[6].Position, new(_center, _zAxis * -1, _xAxis, true), _center, CadPrecisionInputKind.Length)]; }
    public override void MoveGrip(int i, OcctPoint3d p) { if (!p.IsFinite) throw new ArgumentOutOfRangeException(nameof(p)); if (i == 0) _center = p; else if (i is >= 1 and <= 6) { var r = _center.DistanceTo(p); if (r <= 1e-9) return; if (i <= 2) _rx = r; else if (i <= 4) _ry = r; else _rz = r; } else throw new ArgumentOutOfRangeException(nameof(i)); RaiseGeometryChanged(nameof(MoveGrip)); }
    public override CadEntity Duplicate() => CopyPropertiesTo(new CadEllipsoidEntity(_center, _xAxis, _yAxis, _zAxis, _rx, _ry, _rz));
    public override void RestoreGeometry(CadEntity s) { if (s is not CadEllipsoidEntity e) throw new ArgumentException("Snapshot type does not match."); _center = e._center; _xAxis = e._xAxis; _yAxis = e._yAxis; _zAxis = e._zAxis; _rx = e._rx; _ry = e._ry; _rz = e._rz; RaiseGeometryChanged(nameof(RestoreGeometry)); }
    public override void Translate(OcctVector3d d) { ValidateDisplacement(d); _center = Translated(_center, d); RaiseGeometryChanged(nameof(Translate)); }
    public override void Rotate(OcctPoint3d c, OcctVector3d a, double q) { _center = CadTransformMath.RotatePoint(_center, c, a, q); _xAxis = CadTransformMath.RotateVector(_xAxis, a, q); _yAxis = CadTransformMath.RotateVector(_yAxis, a, q); _zAxis = CadTransformMath.RotateVector(_zAxis, a, q); RaiseGeometryChanged(nameof(Rotate)); }
    public override void Scale(OcctPoint3d c, double f) { CadTransformMath.ValidateScale(f); _center = CadTransformMath.ScalePoint(_center, c, f); _rx *= f; _ry *= f; _rz *= f; RaiseGeometryChanged(nameof(Scale)); }
    private void SetCenter(double x, double y, double z) { ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); SetGeometry(ref _center, new(x, y, z)); }
    private static void ValidateRadii(double x, double y, double z) { ValidatePositive(x, nameof(x)); ValidatePositive(y, nameof(y)); ValidatePositive(z, nameof(z)); }
    private static void ValidateFrame(OcctVector3d x, OcctVector3d y, OcctVector3d z) { if (!x.TryNormalize(out x) || !y.TryNormalize(out y) || !z.TryNormalize(out z) || Math.Abs(x.Dot(y)) > 1e-8 || Math.Abs(x.Dot(z)) > 1e-8 || Math.Abs(y.Dot(z)) > 1e-8 || x.Cross(y).Dot(z) < .999999) throw new ArgumentException("Axes must form a right-handed orthonormal frame."); }
    internal static JsonObject WriteGeometry(CadEllipsoidEntity e) => new() { ["center"] = CadEntityJson.Point(e.Center), ["xAxis"] = CadEntityJson.Vector(e.XAxis), ["yAxis"] = CadEntityJson.Vector(e.YAxis), ["zAxis"] = CadEntityJson.Vector(e.ZAxis), ["xRadius"] = e.XRadius, ["yRadius"] = e.YRadius, ["zRadius"] = e.ZRadius };
    internal static CadEllipsoidEntity ReadGeometry(JsonObject d) => new(CadEntityJson.ReadPoint(d, "center"), CadEntityJson.ReadVector(d, "xAxis"), CadEntityJson.ReadVector(d, "yAxis"), CadEntityJson.ReadVector(d, "zAxis"), CadEntityJson.ReadDouble(d, "xRadius"), CadEntityJson.ReadDouble(d, "yRadius"), CadEntityJson.ReadDouble(d, "zRadius"));
}
