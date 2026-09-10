using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadBoxEntity : CadEntity
{
    private const double FrameTolerance = 1e-9;
    private OcctPoint3d _origin;
    private OcctVector3d _xAxis = OcctVector3d.UnitX;
    private OcctVector3d _yAxis = OcctVector3d.UnitY;
    private OcctVector3d _zAxis = OcctVector3d.UnitZ;
    private double _length;
    private double _width;
    private double _height;

    public CadBoxEntity(OcctPoint3d origin, double length, double width, double height)
        : this(origin, OcctVector3d.UnitX, OcctVector3d.UnitY, OcctVector3d.UnitZ, length, width, height)
    {
    }

    internal CadBoxEntity(OcctPoint3d origin, OcctVector3d xAxis, OcctVector3d yAxis, OcctVector3d zAxis, double length, double width, double height) : base("Box")
    {
        if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin));
        ValidatePositive(length, nameof(length));
        ValidatePositive(width, nameof(width));
        ValidatePositive(height, nameof(height));
        _origin = origin;
        _xAxis = CadTransformMath.Normalize(xAxis, nameof(xAxis));
        _yAxis = CadTransformMath.Normalize(yAxis, nameof(yAxis));
        _zAxis = CadTransformMath.Normalize(zAxis, nameof(zAxis));
        ValidateFrame(_xAxis, _yAxis, _zAxis);
        _length = length;
        _width = width;
        _height = height;
    }

    [Browsable(false)] public OcctPoint3d Origin => _origin;
    [Browsable(false)] public OcctVector3d XAxis => _xAxis;
    [Browsable(false)] public OcctVector3d YAxis => _yAxis;
    [Browsable(false)] public OcctVector3d ZAxis => _zAxis;
    [Category("Orientation"), ReadOnly(true)] public double XAxisX => _xAxis.X;
    [Category("Orientation"), ReadOnly(true)] public double XAxisY => _xAxis.Y;
    [Category("Orientation"), ReadOnly(true)] public double XAxisZ => _xAxis.Z;
    [Category("Orientation"), ReadOnly(true)] public double YAxisX => _yAxis.X;
    [Category("Orientation"), ReadOnly(true)] public double YAxisY => _yAxis.Y;
    [Category("Orientation"), ReadOnly(true)] public double YAxisZ => _yAxis.Z;
    [Category("Orientation"), ReadOnly(true)] public double ZAxisX => _zAxis.X;
    [Category("Orientation"), ReadOnly(true)] public double ZAxisY => _zAxis.Y;
    [Category("Orientation"), ReadOnly(true)] public double ZAxisZ => _zAxis.Z;
    [Category("Geometry")] public double X { get => _origin.X; set => SetOrigin(value, _origin.Y, _origin.Z); }
    [Category("Geometry")] public double Y { get => _origin.Y; set => SetOrigin(_origin.X, value, _origin.Z); }
    [Category("Geometry")] public double Z { get => _origin.Z; set => SetOrigin(_origin.X, _origin.Y, value); }
    [Category("Geometry")] public double Length { get => _length; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _length, value); } }
    [Category("Geometry")] public double Width { get => _width; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _width, value); } }
    [Category("Geometry")] public double Height { get => _height; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _height, value); } }
    [Category("Measurement"), ReadOnly(true)] public double SurfaceArea => 2.0 * (_length * _width + _length * _height + _width * _height);
    [Category("Measurement"), ReadOnly(true)] public double Volume => _length * _width * _height;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var shape = engine.MakeBox(_length, _width, _height, _origin.X, _origin.Y, _origin.Z);
        if (!CadTransformMath.TryGetAxisAngle(_xAxis, _yAxis, _zAxis, out var axis, out var angle)) return shape;
        var rotated = engine.Rotate(shape, _origin, axis, angle);
        engine.Delete(shape);
        return rotated;
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var corners = Corners();
        var result = new List<CadSnapPoint>(27);
        for (var index = 0; index < corners.Length; index++) result.Add(new CadSnapPoint(this, corners[index], CadSnapType.Endpoint, index));
        var edgePairs = new (int A, int B)[] { (0, 1), (0, 2), (1, 3), (2, 3), (4, 5), (4, 6), (5, 7), (6, 7), (0, 4), (1, 5), (2, 6), (3, 7) };
        for (var index = 0; index < edgePairs.Length; index++)
        {
            var (a, b) = edgePairs[index];
            result.Add(new CadSnapPoint(this, Midpoint(corners[a], corners[b]), CadSnapType.Midpoint, 8 + index));
        }
        var center = LocalPoint(_length * 0.5, _width * 0.5, _height * 0.5);
        result.Add(new CadSnapPoint(this, center, CadSnapType.Center, 20));
        var faceCenters = new[] { LocalPoint(_length * 0.5, _width * 0.5, 0), LocalPoint(_length * 0.5, _width * 0.5, _height), LocalPoint(0, _width * 0.5, _height * 0.5), LocalPoint(_length, _width * 0.5, _height * 0.5), LocalPoint(_length * 0.5, 0, _height * 0.5), LocalPoint(_length * 0.5, _width, _height * 0.5) };
        var facePlanes = new[] { new CadSnapWorkPlane(faceCenters[0], _xAxis, _yAxis), new CadSnapWorkPlane(faceCenters[1], _xAxis, _yAxis), new CadSnapWorkPlane(faceCenters[2], _yAxis, _zAxis), new CadSnapWorkPlane(faceCenters[3], _yAxis, _zAxis), new CadSnapWorkPlane(faceCenters[4], _xAxis, _zAxis), new CadSnapWorkPlane(faceCenters[5], _xAxis, _zAxis) };
        for (var index = 0; index < faceCenters.Length; index++) result.Add(new CadSnapPoint(this, faceCenters[index], CadSnapType.Center, 21 + index, facePlanes[index]));
        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var corners = Corners();
        var bottomCenter = LocalPoint(_length * 0.5, _width * 0.5, 0);
        var topCenter = LocalPoint(_length * 0.5, _width * 0.5, _height);
        var negativeZ = new OcctVector3d(-_zAxis.X, -_zAxis.Y, -_zAxis.Z);
        return
        [
            new(this, 0, corners[0], new CadGripWorkPlane(corners[0], _xAxis, _yAxis), corners[3], Kind: CadGripKind.Vertex),
            new(this, 1, corners[1], new CadGripWorkPlane(corners[1], _xAxis, _yAxis), corners[2], Kind: CadGripKind.Vertex),
            new(this, 2, corners[2], new CadGripWorkPlane(corners[2], _xAxis, _yAxis), corners[1], Kind: CadGripKind.Vertex),
            new(this, 3, corners[3], new CadGripWorkPlane(corners[3], _xAxis, _yAxis), corners[0], Kind: CadGripKind.Vertex),
            new(this, 4, bottomCenter, new CadGripWorkPlane(bottomCenter, negativeZ, _xAxis, true, 0.0), topCenter, CadPrecisionInputKind.Length, CadGripKind.Height),
            new(this, 5, topCenter, new CadGripWorkPlane(topCenter, _zAxis, _xAxis, true, 0.0), bottomCenter, CadPrecisionInputKind.Length, CadGripKind.Height)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        var grips = GetGripPoints();
        if (index < 0 || index >= grips.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var offset = CadTransformMath.Between(grips[index].Position, targetPoint);
        var ox = CadTransformMath.Dot(offset, _xAxis);
        var oy = CadTransformMath.Dot(offset, _yAxis);
        var oz = CadTransformMath.Dot(offset, _zAxis);
        var origin = _origin; var length = _length; var width = _width; var height = _height;
        switch (index)
        {
            case 0: origin = CadTransformMath.Add(CadTransformMath.Add(origin, _xAxis, ox), _yAxis, oy); length -= ox; width -= oy; break;
            case 1: origin = CadTransformMath.Add(origin, _yAxis, oy); length += ox; width -= oy; break;
            case 2: origin = CadTransformMath.Add(origin, _xAxis, ox); length -= ox; width += oy; break;
            case 3: length += ox; width += oy; break;
            case 4: origin = CadTransformMath.Add(origin, _zAxis, oz); height -= oz; break;
            case 5: height += oz; break;
        }
        if (length <= 1e-9 || width <= 1e-9 || height <= 1e-9) return;
        _origin = origin; _length = length; _width = width; _height = height;
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() => CopyPropertiesTo(new CadBoxEntity(_origin, _xAxis, _yAxis, _zAxis, _length, _width, _height));
    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadBoxEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _origin = value._origin; _xAxis = value._xAxis; _yAxis = value._yAxis; _zAxis = value._zAxis; _length = value._length; _width = value._width; _height = value._height;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }
    public override void Translate(OcctVector3d displacement) { ValidateDisplacement(displacement); _origin = Translated(_origin, displacement); RaiseGeometryChanged(nameof(Translate)); }
    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        _origin = CadTransformMath.RotatePoint(_origin, center, axis, angleDegrees);
        _xAxis = CadTransformMath.RotateVector(_xAxis, axis, angleDegrees).Normalized();
        _yAxis = CadTransformMath.RotateVector(_yAxis, axis, angleDegrees).Normalized();
        _zAxis = CadTransformMath.RotateVector(_zAxis, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }
    public override void Scale(OcctPoint3d center, double factor) { CadTransformMath.ValidateScale(factor); _origin = CadTransformMath.ScalePoint(_origin, center, factor); _length *= factor; _width *= factor; _height *= factor; RaiseGeometryChanged(nameof(Scale)); }

    private OcctPoint3d LocalPoint(double x, double y, double z) => CadTransformMath.Add(CadTransformMath.Add(CadTransformMath.Add(_origin, _xAxis, x), _yAxis, y), _zAxis, z);
    private static OcctPoint3d Midpoint(OcctPoint3d a, OcctPoint3d b) => new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5, (a.Z + b.Z) * 0.5);
    private OcctPoint3d[] Corners()
    {
        var x = CadTransformMath.Add(_origin, _xAxis, _length); var y = CadTransformMath.Add(_origin, _yAxis, _width); var z = CadTransformMath.Add(_origin, _zAxis, _height);
        var xy = CadTransformMath.Add(x, _yAxis, _width); var xz = CadTransformMath.Add(x, _zAxis, _height); var yz = CadTransformMath.Add(y, _zAxis, _height); var xyz = CadTransformMath.Add(xy, _zAxis, _height);
        return [_origin, x, y, xy, z, xz, yz, xyz];
    }
    private void SetOrigin(double x, double y, double z) { ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); SetGeometry(ref _origin, new OcctPoint3d(x, y, z)); }
    private static void ValidateFrame(OcctVector3d xAxis, OcctVector3d yAxis, OcctVector3d zAxis)
    {
        if (Math.Abs(CadTransformMath.Dot(xAxis, yAxis)) > FrameTolerance || Math.Abs(CadTransformMath.Dot(xAxis, zAxis)) > FrameTolerance || Math.Abs(CadTransformMath.Dot(yAxis, zAxis)) > FrameTolerance)
            throw new ArgumentException("Box axes must be mutually orthogonal.");
        var cross = xAxis.Cross(yAxis);
        if (!cross.TryNormalize(out var expectedZ) || CadTransformMath.Dot(expectedZ, zAxis) < 1.0 - FrameTolerance)
            throw new ArgumentException("Box axes must form a right-handed coordinate frame.");
    }

    internal static JsonObject WriteGeometry(CadBoxEntity entity) => new() { ["origin"] = CadEntityJson.Point(entity.Origin), ["xAxis"] = CadEntityJson.Vector(entity.XAxis), ["yAxis"] = CadEntityJson.Vector(entity.YAxis), ["zAxis"] = CadEntityJson.Vector(entity.ZAxis), ["length"] = entity.Length, ["width"] = entity.Width, ["height"] = entity.Height };
    internal static CadBoxEntity ReadGeometry(JsonObject data) => new(CadEntityJson.ReadPoint(data, "origin"), CadEntityJson.ReadVector(data, "xAxis"), CadEntityJson.ReadVector(data, "yAxis"), CadEntityJson.ReadVector(data, "zAxis"), CadEntityJson.ReadDouble(data, "length"), CadEntityJson.ReadDouble(data, "width"), CadEntityJson.ReadDouble(data, "height"));
}
