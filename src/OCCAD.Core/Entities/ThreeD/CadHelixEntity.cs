using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;
namespace OCCAD;

public sealed class CadHelixEntity : CadEntity
{
    private OcctPoint3d _origin; private OcctVector3d _axis, _xAxis; private double _radius, _pitch, _turns;
    public CadHelixEntity(OcctPoint3d origin, OcctVector3d axis, OcctVector3d xAxis, double radius, double pitch, double turns) : base("Helix")
    { if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin)); if (!axis.TryNormalize(out _axis) || !xAxis.TryNormalize(out _xAxis) || Math.Abs(_axis.Dot(_xAxis)) > 1e-8) throw new ArgumentException("Helix axis and X direction must be perpendicular."); Validate(radius, pitch, turns); _origin = origin; _radius = radius; _pitch = pitch; _turns = turns; DisplayMode = OcctDisplayMode.Wireframe; }
    [Browsable(false)] public OcctPoint3d Origin => _origin; [Browsable(false)] public OcctVector3d Axis => _axis; [Browsable(false)] public OcctVector3d XAxis => _xAxis;
    [Category("Geometry")] public double X { get => _origin.X; set => SetOrigin(value, _origin.Y, _origin.Z); }
    [Category("Geometry")] public double Y { get => _origin.Y; set => SetOrigin(_origin.X, value, _origin.Z); }
    [Category("Geometry")] public double Z { get => _origin.Z; set => SetOrigin(_origin.X, _origin.Y, value); }
    [Category("Geometry")] public double Radius { get => _radius; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _radius, value); } }
    [Category("Geometry")] public double Pitch { get => _pitch; set { ValidatePitch(value); SetGeometry(ref _pitch, value); } }
    [Category("Geometry")] public double Turns { get => _turns; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _turns, value); } }
    [Category("Measurement"), ReadOnly(true)] public double Height => Math.Abs(_pitch * _turns); [Category("Measurement"), ReadOnly(true)] public double Length => _turns * Math.Sqrt(Math.Pow(2 * Math.PI * _radius, 2) + _pitch * _pitch);
    [Browsable(false)] public OcctPoint3d StartPoint => _origin + _xAxis * _radius;
    [Browsable(false)] public OcctPoint3d EndPoint { get { var angle = _turns * 360; return _origin + _axis * (_pitch * _turns) + CadTransformMath.RotateVector(_xAxis, _axis, angle) * _radius; } }
    internal override OcctShape BuildShape(OcctEngine engine) { using var model = new OcctModelingSession(); var shape = model.MakeHelix(_radius, _pitch, _turns, _origin, _axis, _xAxis); return engine.CreateShapeFromModel(model, shape); }
    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() => [new(this, StartPoint, CadSnapType.Endpoint, 0), new(this, EndPoint, CadSnapType.Endpoint, 1), new(this, _origin, CadSnapType.Center, 2)];
    public override IReadOnlyList<CadGripPoint> GetGripPoints() => [new(this, 0, _origin), new(this, 1, StartPoint, new(_origin, _xAxis, _axis.Cross(_xAxis), true), _origin, CadPrecisionInputKind.Length)];
    public override void MoveGrip(int i, OcctPoint3d p) { if (!p.IsFinite) throw new ArgumentOutOfRangeException(nameof(p)); if (i == 0) _origin = p; else if (i == 1) { var d = p - _origin; var planar = d - _axis * d.Dot(_axis); var r = planar.Length; if (r <= 1e-9) return; _radius = r; _xAxis = planar.Normalized(); } else throw new ArgumentOutOfRangeException(nameof(i)); RaiseGeometryChanged(nameof(MoveGrip)); }
    public override CadEntity Duplicate() => CopyPropertiesTo(new CadHelixEntity(_origin, _axis, _xAxis, _radius, _pitch, _turns));
    public override void RestoreGeometry(CadEntity s) { if (s is not CadHelixEntity e) throw new ArgumentException("Snapshot type does not match."); _origin = e._origin; _axis = e._axis; _xAxis = e._xAxis; _radius = e._radius; _pitch = e._pitch; _turns = e._turns; RaiseGeometryChanged(nameof(RestoreGeometry)); }
    public override void Translate(OcctVector3d d) { ValidateDisplacement(d); _origin = Translated(_origin, d); RaiseGeometryChanged(nameof(Translate)); }
    public override void Rotate(OcctPoint3d c, OcctVector3d a, double q) { _origin = CadTransformMath.RotatePoint(_origin, c, a, q); _axis = CadTransformMath.RotateVector(_axis, a, q); _xAxis = CadTransformMath.RotateVector(_xAxis, a, q); RaiseGeometryChanged(nameof(Rotate)); }
    public override void Scale(OcctPoint3d c, double f) { CadTransformMath.ValidateScale(f); _origin = CadTransformMath.ScalePoint(_origin, c, f); _radius *= f; _pitch *= f; RaiseGeometryChanged(nameof(Scale)); }
    private void SetOrigin(double x, double y, double z) { ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); SetGeometry(ref _origin, new(x, y, z)); }
    private static void Validate(double r, double p, double t) { ValidatePositive(r, nameof(r)); ValidatePitch(p); ValidatePositive(t, nameof(t)); }
    private static void ValidatePitch(double p) { if (!double.IsFinite(p) || Math.Abs(p) <= 1e-12) throw new ArgumentOutOfRangeException(nameof(p)); }
    internal static JsonObject WriteGeometry(CadHelixEntity e) => new() { ["origin"] = CadEntityJson.Point(e.Origin), ["axis"] = CadEntityJson.Vector(e.Axis), ["xAxis"] = CadEntityJson.Vector(e.XAxis), ["radius"] = e.Radius, ["pitch"] = e.Pitch, ["turns"] = e.Turns };
    internal static CadHelixEntity ReadGeometry(JsonObject d) => new(CadEntityJson.ReadPoint(d, "origin"), CadEntityJson.ReadVector(d, "axis"), CadEntityJson.ReadVector(d, "xAxis"), CadEntityJson.ReadDouble(d, "radius"), CadEntityJson.ReadDouble(d, "pitch"), CadEntityJson.ReadDouble(d, "turns"));
}
