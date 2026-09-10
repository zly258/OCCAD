using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public enum CadCircularDimensionKind { Radius, Diameter }

public sealed class CadCircularDimensionEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private OcctVector3d _direction;
    private double _radius;
    private double _offset;
    private double _textHeight;
    private double _arrowSize;
    private string _fontName;

    public CadCircularDimensionEntity(
        CadCircularDimensionKind kind,
        OcctPoint3d center,
        OcctVector3d normal,
        OcctVector3d direction,
        double radius,
        double offset = 0,
        double textHeight = 4,
        double arrowSize = 2.5,
        string fontName = "Arial") : base("Circular Dimension")
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        _normal = CadTransformMath.Normalize(normal, nameof(normal));
        _direction = Orthogonalize(direction, _normal);
        ValidatePositive(radius, nameof(radius));
        ValidateFinite(offset, nameof(offset));
        ValidatePositive(textHeight, nameof(textHeight));
        ValidatePositive(arrowSize, nameof(arrowSize));
        ArgumentException.ThrowIfNullOrWhiteSpace(fontName);
        Kind = kind;
        _center = center;
        _radius = radius;
        _offset = offset;
        _textHeight = textHeight;
        _arrowSize = arrowSize;
        _fontName = fontName.Trim();
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Dimension"), ReadOnly(true)] public CadCircularDimensionKind Kind { get; }
    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d Normal => _normal;
    [Browsable(false)] public OcctVector3d Direction => _direction;
    [Category("Measurement"), ReadOnly(true)] public double Radius => _radius;
    [Category("Measurement"), ReadOnly(true)] public double Diameter => _radius * 2;
    [Category("Dimension")] public double Offset { get => _offset; set { ValidateFinite(value, nameof(value)); SetGeometry(ref _offset, value); } }
    [Category("Dimension")] public double TextHeight { get => _textHeight; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _textHeight, value); } }
    [Category("Dimension")] public double ArrowSize { get => _arrowSize; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _arrowSize, value); } }
    [Category("Dimension")] public string FontName { get => _fontName; set { ArgumentException.ThrowIfNullOrWhiteSpace(value); SetGeometry(ref _fontName, value.Trim()); } }

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        using var model = new OcctModelingSession();
        var source = model.MakeArc(_center, _normal, _direction, _radius, 0, 90);
        var options = new OcctBRepAnnotationOptions(_offset, _textHeight, _arrowSize, _fontName);
        var annotation = Kind == CadCircularDimensionKind.Radius
            ? model.MakeRadiusAnnotation(source, options)
            : model.MakeDiameterAnnotation(source, options);
        return engine.CreateShapeFromModel(model, annotation);
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var leader = _center + _direction * (_radius + _offset);
        return
        [
            new(this, _center, CadSnapType.Center, 0),
            new(this, _center + _direction * _radius, CadSnapType.Vertex, 1),
            new(this, leader, CadSnapType.Endpoint, 2)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var leader = _center + _direction * (_radius + _offset);
        var side = _normal.Cross(_direction);
        var plane = side.TryNormalize(out var yAxis)
            ? new CadGripWorkPlane(_center, _direction, yAxis)
            : null;
        return
        [
            new(
                this,
                0,
                leader,
                plane,
                _center,
                CadPrecisionInputKind.LengthAndAngle,
                CadGripKind.Radius)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var planar = targetPoint - _center;
        planar -= _normal * planar.Dot(_normal);
        var distance = Math.Sqrt(planar.LengthSquared);
        if (!double.IsFinite(distance) ||
            distance <= 1e-9 ||
            !planar.TryNormalize(out var direction))
            return;

        _direction = direction;
        _offset = distance - _radius;
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadCircularDimensionEntity(Kind, _center, _normal, _direction, _radius, _offset, _textHeight, _arrowSize, _fontName));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadCircularDimensionEntity value || value.Kind != Kind)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _center = value._center;
        _normal = value._normal;
        _direction = value._direction;
        _radius = value._radius;
        _offset = value._offset;
        _textHeight = value._textHeight;
        _arrowSize = value._arrowSize;
        _fontName = value._fontName;
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
        _direction = CadTransformMath.RotateVector(_direction, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _center = CadTransformMath.ScalePoint(_center, center, factor);
        _radius *= factor;
        _offset *= factor;
        _textHeight *= factor;
        _arrowSize *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private static OcctVector3d Orthogonalize(OcctVector3d direction, OcctVector3d normal)
    {
        var projected = direction - normal * direction.Dot(normal);
        if (!projected.TryNormalize(out var result))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return result;
    }

    internal static JsonObject WriteGeometry(CadCircularDimensionEntity e) => new()
    {
        ["kind"] = e.Kind.ToString(),
        ["center"] = CadEntityJson.Point(e.Center),
        ["normal"] = CadEntityJson.Vector(e.Normal),
        ["direction"] = CadEntityJson.Vector(e.Direction),
        ["radius"] = e.Radius,
        ["offset"] = e.Offset,
        ["textHeight"] = e.TextHeight,
        ["arrowSize"] = e.ArrowSize,
        ["fontName"] = e.FontName
    };

    internal static CadCircularDimensionEntity ReadGeometry(JsonObject data)
    {
        if (!Enum.TryParse<CadCircularDimensionKind>(data["kind"]?.GetValue<string>(), true, out var kind))
            throw new InvalidDataException("Invalid circular dimension kind.");
        var font = data["fontName"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(font))
            throw new InvalidDataException("CAD dimension font cannot be empty.");
        return new(
            kind,
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "normal"),
            CadEntityJson.ReadVector(data, "direction"),
            CadEntityJson.ReadDouble(data, "radius"),
            CadEntityJson.ReadDouble(data, "offset"),
            CadEntityJson.ReadDouble(data, "textHeight"),
            CadEntityJson.ReadDouble(data, "arrowSize"),
            font);
    }
}
