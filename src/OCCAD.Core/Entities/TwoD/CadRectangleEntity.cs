using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadRectangleEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private double _width;
    private double _height;

    public CadRectangleEntity(
        OcctPoint3d center,
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        double width,
        double height) : base("Rectangle")
    {
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        _xAxis = CadTransformMath.Normalize(xAxis, nameof(xAxis));
        var normal = _xAxis.Cross(yAxis);
        if (!normal.TryNormalize(out var n))
            throw new ArgumentException("Rectangle axes must not be parallel.", nameof(yAxis));
        _yAxis = n.Cross(_xAxis).Normalized();
        ValidatePositive(width, nameof(width));
        ValidatePositive(height, nameof(height));
        _center = center;
        _width = width;
        _height = height;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d XAxis => _xAxis;
    [Browsable(false)] public OcctVector3d YAxis => _yAxis;
    [Category("Orientation"), ReadOnly(true)] public double XAxisX => _xAxis.X;
    [Category("Orientation"), ReadOnly(true)] public double XAxisY => _xAxis.Y;
    [Category("Orientation"), ReadOnly(true)] public double XAxisZ => _xAxis.Z;
    [Category("Orientation"), ReadOnly(true)] public double YAxisX => _yAxis.X;
    [Category("Orientation"), ReadOnly(true)] public double YAxisY => _yAxis.Y;
    [Category("Orientation"), ReadOnly(true)] public double YAxisZ => _yAxis.Z;

    [Category("Geometry")] public double CenterX { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double CenterY { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double CenterZ { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }
    [Category("Geometry")] public double Width { get => _width; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _width, value); } }
    [Category("Geometry")] public double Height { get => _height; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _height, value); } }

    [Category("Measurement"), ReadOnly(true)] public double Perimeter => 2.0 * (_width + _height);
    [Category("Measurement"), ReadOnly(true)] public double Area => _width * _height;
    [Browsable(false)] public IReadOnlyList<OcctPoint3d> CornerPoints => Corners();

    internal override OcctShape BuildShape(OcctEngine engine) => engine.MakePolyline(Corners(), true);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        var corners = CornerPoints;
        var result = new List<CadSnapCurve>(corners.Count);
        for (var index = 0; index < corners.Count; index++)
        {
            if (CadPrecisionSnapGeometry.TryCreateSegment(
                    this, index, corners[index], corners[(index + 1) % corners.Count], workPlane, out var curve))
                result.Add(curve);
        }
        return result;
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var c = Corners();
        var plane = new CadSnapWorkPlane(_center, _xAxis, _yAxis);
        return
        [
            new(this, _center, CadSnapType.Center, 0, plane),
            new(this, Midpoint(c[3], c[0]), CadSnapType.Midpoint, 1, plane),
            new(this, Midpoint(c[1], c[2]), CadSnapType.Midpoint, 2, plane),
            new(this, Midpoint(c[0], c[1]), CadSnapType.Midpoint, 3, plane),
            new(this, Midpoint(c[2], c[3]), CadSnapType.Midpoint, 4, plane),
            new(this, c[0], CadSnapType.Endpoint, 5, plane),
            new(this, c[1], CadSnapType.Endpoint, 6, plane),
            new(this, c[2], CadSnapType.Endpoint, 7, plane),
            new(this, c[3], CadSnapType.Endpoint, 8, plane)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var c = Corners();
        var plane = new CadGripWorkPlane(_center, _xAxis, _yAxis);
        var left = Midpoint(c[3], c[0]);
        var right = Midpoint(c[1], c[2]);
        var top = Midpoint(c[0], c[1]);
        var bottom = Midpoint(c[2], c[3]);
        return
        [
            new(this, 0, _center, plane, Kind: CadGripKind.Center),
            new(this, 1, c[0], plane, c[2], Kind: CadGripKind.Vertex),
            new(this, 2, c[1], plane, c[3], Kind: CadGripKind.Vertex),
            new(this, 3, c[2], plane, c[0], Kind: CadGripKind.Vertex),
            new(this, 4, c[3], plane, c[1], Kind: CadGripKind.Vertex),
            new(this, 5, left,
                new CadGripWorkPlane(_center, _xAxis, _yAxis, true, 0.0),
                right, CadPrecisionInputKind.Length, CadGripKind.Midpoint),
            new(this, 6, right,
                new CadGripWorkPlane(_center, _xAxis, _yAxis, true, 0.0),
                left, CadPrecisionInputKind.Length, CadGripKind.Midpoint),
            new(this, 7, top,
                new CadGripWorkPlane(_center, _xAxis, _yAxis, true, 90.0),
                bottom, CadPrecisionInputKind.Length, CadGripKind.Midpoint),
            new(this, 8, bottom,
                new CadGripWorkPlane(_center, _xAxis, _yAxis, true, -90.0),
                top, CadPrecisionInputKind.Length, CadGripKind.Midpoint)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        var grips = GetGripPoints();
        if (index < 0 || index >= grips.Count) throw new ArgumentOutOfRangeException(nameof(index));

        if (index == 0)
        {
            _center = targetPoint;
            RaiseGeometryChanged(nameof(MoveGrip));
            return;
        }

        if (index is >= 1 and <= 4)
        {
            var opposite = grips[index switch { 1 => 3, 2 => 4, 3 => 1, _ => 2 }].Position;
            var delta = CadTransformMath.Between(opposite, targetPoint);
            var dx = CadTransformMath.Dot(delta, _xAxis);
            var dy = CadTransformMath.Dot(delta, _yAxis);
            var width = Math.Abs(dx);
            var height = Math.Abs(dy);
            if (width <= 1e-9 || height <= 1e-9) return;

            _center = opposite + _xAxis * (dx * 0.5) + _yAxis * (dy * 0.5);
            _width = width;
            _height = height;
        }
        else if (index is 5 or 6)
        {
            var opposite = grips[index == 5 ? 6 : 5].Position;
            var dx = CadTransformMath.Dot(
                CadTransformMath.Between(opposite, targetPoint),
                _xAxis);
            var width = Math.Abs(dx);
            if (width <= 1e-9) return;

            _center = opposite + _xAxis * (dx * 0.5);
            _width = width;
        }
        else
        {
            var opposite = grips[index == 7 ? 8 : 7].Position;
            var dy = CadTransformMath.Dot(
                CadTransformMath.Between(opposite, targetPoint),
                _yAxis);
            var height = Math.Abs(dy);
            if (height <= 1e-9) return;

            _center = opposite + _yAxis * (dy * 0.5);
            _height = height;
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadRectangleEntity(_center, _xAxis, _yAxis, _width, _height));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadRectangleEntity value)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _center = value._center;
        _xAxis = value._xAxis;
        _yAxis = value._yAxis;
        _width = value._width;
        _height = value._height;
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
        _xAxis = CadTransformMath.RotateVector(_xAxis, axis, angleDegrees).Normalized();
        _yAxis = CadTransformMath.RotateVector(_yAxis, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _center = CadTransformMath.ScalePoint(_center, center, factor);
        _width *= factor;
        _height *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private IReadOnlyList<OcctPoint3d> Corners()
    {
        var hx = _width * 0.5;
        var hy = _height * 0.5;
        return
        [
            _center - _xAxis * hx + _yAxis * hy,
            _center + _xAxis * hx + _yAxis * hy,
            _center + _xAxis * hx - _yAxis * hy,
            _center - _xAxis * hx - _yAxis * hy
        ];
    }

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    private static OcctPoint3d Midpoint(OcctPoint3d a, OcctPoint3d b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5, (a.Z + b.Z) * 0.5);

    internal static JsonObject WriteGeometry(CadRectangleEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["xAxis"] = CadEntityJson.Vector(entity.XAxis),
            ["yAxis"] = CadEntityJson.Vector(entity.YAxis),
            ["width"] = entity.Width,
            ["height"] = entity.Height
        };

    internal static CadRectangleEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "xAxis"),
            CadEntityJson.ReadVector(data, "yAxis"),
            CadEntityJson.ReadDouble(data, "width"),
            CadEntityJson.ReadDouble(data, "height"));
}
