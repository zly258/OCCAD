using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadTextEntity : CadEntity
{
    private string _text;
    private OcctPoint3d _position;
    private OcctVector3d _normal;
    private OcctVector3d _xAxis;
    private double _height;
    private double _angleDegrees;
    private string _fontName;

    public CadTextEntity(
        string text,
        OcctPoint3d position,
        OcctVector3d normal,
        OcctVector3d xAxis,
        double height,
        double angleDegrees = 0,
        string fontName = "Arial") : base("Text")
    {
        ValidateText(text);
        if (!position.IsFinite) throw new ArgumentOutOfRangeException(nameof(position));
        _normal = CadTransformMath.Normalize(normal, nameof(normal));
        _xAxis = Orthogonalize(xAxis, _normal);
        ValidatePositive(height, nameof(height));
        ValidateFinite(angleDegrees, nameof(angleDegrees));
        ArgumentException.ThrowIfNullOrWhiteSpace(fontName);

        _text = text;
        _position = position;
        _height = height;
        _angleDegrees = angleDegrees;
        _fontName = fontName.Trim();
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Category("Text")]
    public string Text
    {
        get => _text;
        set
        {
            ValidateText(value);
            SetGeometry(ref _text, value);
        }
    }

    [Browsable(false)] public OcctPoint3d Position => _position;
    [Browsable(false)] public OcctVector3d Normal => _normal;
    [Browsable(false)] public OcctVector3d XAxis => _xAxis;

    [Category("Geometry")] public double X { get => _position.X; set => SetPosition(value, _position.Y, _position.Z); }
    [Category("Geometry")] public double Y { get => _position.Y; set => SetPosition(_position.X, value, _position.Z); }
    [Category("Geometry")] public double Z { get => _position.Z; set => SetPosition(_position.X, _position.Y, value); }

    [Category("Text")]
    public double Height
    {
        get => _height;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _height, value);
        }
    }

    [Category("Text")]
    public double AngleDegrees
    {
        get => _angleDegrees;
        set
        {
            ValidateFinite(value, nameof(value));
            SetGeometry(ref _angleDegrees, value);
        }
    }


    [Category("Text")]
    public string FontName
    {
        get => _fontName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            SetGeometry(ref _fontName, value.Trim());
        }
    }

    [Category("Orientation"), ReadOnly(true)] public double NormalX => _normal.X;
    [Category("Orientation"), ReadOnly(true)] public double NormalY => _normal.Y;
    [Category("Orientation"), ReadOnly(true)] public double NormalZ => _normal.Z;
    [Category("Orientation"), ReadOnly(true)] public double TextAxisX => _xAxis.X;
    [Category("Orientation"), ReadOnly(true)] public double TextAxisY => _xAxis.Y;
    [Category("Orientation"), ReadOnly(true)] public double TextAxisZ => _xAxis.Z;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var radians = _angleDegrees * Math.PI / 180.0;
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var textAxis = (_xAxis * Math.Cos(radians) + yAxis * Math.Sin(radians)).Normalized();
        using var model = new OcctModelingSession();
        var shape = model.MakeBRepText(
            _text,
            new OcctBRepTextOptions(
                _position,
                _normal,
                textAxis,
                _height,
                0,
                _fontName,
                false,
                false,
                OcctTextHorizontalAlignment.Left,
                OcctTextVerticalAlignment.Bottom));
        return engine.CreateShapeFromModel(model, shape);
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
        [new(this, _position, CadSnapType.Vertex, 0)];

    public override IReadOnlyList<CadGripPoint> GetGripPoints() =>
        [new(this, 0, _position, Kind: CadGripKind.Center)];

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        SetGeometry(ref _position, targetPoint, nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadTextEntity(
            _text, _position, _normal, _xAxis, _height, _angleDegrees, _fontName));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadTextEntity value)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _text = value._text;
        _position = value._position;
        _normal = value._normal;
        _xAxis = value._xAxis;
        _height = value._height;
        _angleDegrees = value._angleDegrees;
        _fontName = value._fontName;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        _position = Translated(_position, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        _position = CadTransformMath.RotatePoint(_position, center, axis, angleDegrees);
        _normal = CadTransformMath.RotateVector(_normal, axis, angleDegrees).Normalized();
        _xAxis = CadTransformMath.RotateVector(_xAxis, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _position = CadTransformMath.ScalePoint(_position, center, factor);
        _height *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private void SetPosition(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _position, new OcctPoint3d(x, y, z));
    }

    private static OcctVector3d Orthogonalize(OcctVector3d xAxis, OcctVector3d normal)
    {
        var projected = xAxis - normal * xAxis.Dot(normal);
        if (!projected.TryNormalize(out var normalized))
            throw new ArgumentOutOfRangeException(nameof(xAxis));
        return normalized;
    }

    private static void ValidateText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
    }

    internal static JsonObject WriteGeometry(CadTextEntity entity) =>
        new()
        {
            ["text"] = entity.Text,
            ["position"] = CadEntityJson.Point(entity.Position),
            ["normal"] = CadEntityJson.Vector(entity.Normal),
            ["xAxis"] = CadEntityJson.Vector(entity.XAxis),
            ["height"] = entity.Height,
            ["angleDegrees"] = entity.AngleDegrees,
            ["fontName"] = entity.FontName
        };

    internal static CadTextEntity ReadGeometry(JsonObject data)
    {
        var text = data["text"]?.GetValue<string>();
        var fontName = data["fontName"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("CAD text field 'text' cannot be empty.");
        if (string.IsNullOrWhiteSpace(fontName))
            throw new InvalidDataException("CAD text field 'fontName' cannot be empty.");
        return new CadTextEntity(
            text,
            CadEntityJson.ReadPoint(data, "position"),
            CadEntityJson.ReadVector(data, "normal"),
            CadEntityJson.ReadVector(data, "xAxis"),
            CadEntityJson.ReadDouble(data, "height"),
            CadEntityJson.ReadDouble(data, "angleDegrees"),
            fontName);
    }
}


