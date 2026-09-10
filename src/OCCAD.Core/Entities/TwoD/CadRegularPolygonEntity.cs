using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadRegularPolygonEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private OcctVector3d _xAxis;
    private double _radius;
    private int _sides;

    public CadRegularPolygonEntity(
        OcctPoint3d center,
        OcctVector3d normal,
        OcctVector3d xAxis,
        double radius,
        int sides) : base("Regular Polygon")
    {
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        _normal = CadTransformMath.Normalize(normal, nameof(normal));
        _xAxis = OrthogonalizeX(xAxis, _normal);
        ValidatePositive(radius, nameof(radius));
        ValidateSides(sides);

        _center = center;
        _radius = radius;
        _sides = sides;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d Normal => _normal;
    [Browsable(false)] public OcctVector3d XAxis => _xAxis;
    [Browsable(false)] public IReadOnlyList<OcctPoint3d> VertexPoints => Vertices();

    [Category("Geometry")] public double CenterX { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double CenterY { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double CenterZ { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }

    [Category("Geometry")]
    public double Radius
    {
        get => _radius;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _radius, value);
        }
    }

    [Category("Geometry")]
    public int Sides
    {
        get => _sides;
        set
        {
            ValidateSides(value);
            SetGeometry(ref _sides, value);
        }
    }

    [Browsable(false)] public double NormalX => _normal.X;
    [Browsable(false)] public double NormalY => _normal.Y;
    [Browsable(false)] public double NormalZ => _normal.Z;
    [Browsable(false)] public double XAxisX => _xAxis.X;
    [Browsable(false)] public double XAxisY => _xAxis.Y;
    [Browsable(false)] public double XAxisZ => _xAxis.Z;

    [Category("Measurement"), ReadOnly(true)]
    public double Perimeter =>
        2.0 * _sides * _radius * Math.Sin(Math.PI / _sides);

    [Category("Measurement"), ReadOnly(true)]
    public double Area =>
        0.5 * _sides * _radius * _radius * Math.Sin(2.0 * Math.PI / _sides);

    internal override OcctShape BuildShape(OcctEngine engine) =>
        engine.MakePolyline(Vertices(), true);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        var vertices = VertexPoints;
        var result = new List<CadSnapCurve>(vertices.Count);

        for (var index = 0; index < vertices.Count; index++)
        {
            if (CadPrecisionSnapGeometry.TryCreateSegment(
                    this,
                    index,
                    vertices[index],
                    vertices[(index + 1) % vertices.Count],
                    workPlane,
                    out var curve))
                result.Add(curve);
        }

        return result;
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var vertices = Vertices();
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var plane = new CadSnapWorkPlane(_center, _xAxis, yAxis);
        var result = new List<CadSnapPoint>(_sides * 2 + 1)
        {
            new(this, _center, CadSnapType.Center, 0, plane)
        };

        for (var index = 0; index < vertices.Count; index++)
        {
            var next = (index + 1) % vertices.Count;
            result.Add(new CadSnapPoint(this, vertices[index], CadSnapType.Endpoint, index, plane));
            result.Add(new CadSnapPoint(this, Midpoint(vertices[index], vertices[next]), CadSnapType.Midpoint, index, plane));
        }

        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var vertices = Vertices();
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var plane = new CadGripWorkPlane(_center, _xAxis, yAxis);
        var result = new CadGripPoint[vertices.Count + 1];
        result[0] = new CadGripPoint(
            this,
            0,
            _center,
            plane,
            Kind: CadGripKind.Center);
        for (var index = 0; index < vertices.Count; index++)
        {
            result[index + 1] = new CadGripPoint(
                this,
                index + 1,
                vertices[index],
                plane,
                _center,
                CadPrecisionInputKind.LengthAndAngle,
                Kind: CadGripKind.Vertex);
        }
        return result;
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        if (index == 0)
        {
            Translate(CadTransformMath.Between(_center, targetPoint));
            return;
        }

        var vertexIndex = index - 1;
        if ((uint)vertexIndex >= (uint)_sides)
            throw new ArgumentOutOfRangeException(nameof(index));

        var delta = CadTransformMath.Between(_center, targetPoint);
        var axial = CadTransformMath.Dot(delta, _normal);
        var planar = new OcctVector3d(
            delta.X - _normal.X * axial,
            delta.Y - _normal.Y * axial,
            delta.Z - _normal.Z * axial);
        var radius = Math.Sqrt(planar.LengthSquared);
        if (radius <= 1e-9 || !planar.TryNormalize(out var direction))
            return;

        var vertexAngle = vertexIndex * 360.0 / _sides;
        _xAxis = CadTransformMath.RotateVector(
            direction,
            _normal,
            -vertexAngle).Normalized();
        _radius = radius;
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadRegularPolygonEntity(
            _center,
            _normal,
            _xAxis,
            _radius,
            _sides));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadRegularPolygonEntity value)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));

        _center = value._center;
        _normal = value._normal;
        _xAxis = value._xAxis;
        _radius = value._radius;
        _sides = value._sides;
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
        _radius *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private IReadOnlyList<OcctPoint3d> Vertices()
    {
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var result = new OcctPoint3d[_sides];
        for (var index = 0; index < _sides; index++)
        {
            var angle = 2.0 * Math.PI * index / _sides;
            result[index] = _center
                + _xAxis * (Math.Cos(angle) * _radius)
                + yAxis * (Math.Sin(angle) * _radius);
        }

        return result;
    }

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    private static OcctVector3d OrthogonalizeX(
        OcctVector3d xAxis,
        OcctVector3d normal)
    {
        var x = CadTransformMath.Normalize(xAxis, nameof(xAxis));
        var dot = CadTransformMath.Dot(x, normal);
        var projected = new OcctVector3d(
            x.X - normal.X * dot,
            x.Y - normal.Y * dot,
            x.Z - normal.Z * dot);
        return CadTransformMath.Normalize(projected, nameof(xAxis));
    }

    private static void ValidateSides(int value)
    {
        if (value < 3 || value > 360)
            throw new ArgumentOutOfRangeException(nameof(value), "Sides must be between 3 and 360.");
    }

    private static OcctPoint3d Midpoint(OcctPoint3d a, OcctPoint3d b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5, (a.Z + b.Z) * 0.5);

    internal static JsonObject WriteGeometry(CadRegularPolygonEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["normal"] = CadEntityJson.Vector(entity.Normal),
            ["xAxis"] = CadEntityJson.Vector(entity.XAxis),
            ["radius"] = entity.Radius,
            ["sides"] = entity.Sides
        };

    internal static CadRegularPolygonEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "normal"),
            CadEntityJson.ReadVector(data, "xAxis"),
            CadEntityJson.ReadDouble(data, "radius"),
            CadEntityJson.ReadInt(data, "sides"));
}
