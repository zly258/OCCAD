using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadLengthDimensionEntity : CadEntity
{
    private OcctPoint3d _start;
    private OcctPoint3d _end;
    private OcctVector3d _normal;
    private double _offset;
    private double _textHeight;
    private double _arrowSize;
    private string _fontName;

    public CadLengthDimensionEntity(
        OcctPoint3d start,
        OcctPoint3d end,
        OcctVector3d normal,
        double offset,
        double textHeight = 4,
        double arrowSize = 2.5,
        string fontName = "Arial") : base("Length Dimension")
    {
        ValidatePoints(start, end);
        _normal = CadTransformMath.Normalize(normal, nameof(normal));
        ValidatePositive(textHeight, nameof(textHeight));
        ValidatePositive(arrowSize, nameof(arrowSize));
        ValidateFinite(offset, nameof(offset));
        ArgumentException.ThrowIfNullOrWhiteSpace(fontName);
        _start = start;
        _end = end;
        _offset = offset;
        _textHeight = textHeight;
        _arrowSize = arrowSize;
        _fontName = fontName.Trim();
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Browsable(false)] public OcctPoint3d Start => _start;
    [Browsable(false)] public OcctPoint3d End => _end;
    [Browsable(false)] public OcctVector3d Normal => _normal;
    [Category("Measurement"), ReadOnly(true)] public double MeasuredLength => (_end - _start).Length;

    [Category("Dimension")]
    public double Offset
    {
        get => _offset;
        set
        {
            ValidateFinite(value, nameof(value));
            SetGeometry(ref _offset, value);
        }
    }

    [Category("Dimension")]
    public double TextHeight
    {
        get => _textHeight;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _textHeight, value);
        }
    }

    [Category("Dimension")]
    public double ArrowSize
    {
        get => _arrowSize;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _arrowSize, value);
        }
    }

    [Category("Dimension")]
    public string FontName
    {
        get => _fontName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            SetGeometry(ref _fontName, value.Trim());
        }
    }

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var axis = (_end - _start).Normalized();
        var desiredOffset = _normal.Cross(axis).Normalized();
        var reference = Math.Abs(axis.Dot(OcctVector3d.UnitZ)) > 0.95
            ? OcctVector3d.UnitX
            : OcctVector3d.UnitZ;
        var defaultOffset = axis.Cross(reference).Normalized().Cross(axis).Normalized();
        var angle = Math.Atan2(
            axis.Dot(defaultOffset.Cross(desiredOffset)),
            defaultOffset.Dot(desiredOffset)) * 180.0 / Math.PI;

        using var model = new OcctModelingSession();
        var edge = model.MakeLine(_start, _end);
        var annotation = model.MakeLengthAnnotation(
            edge,
            new OcctBRepAnnotationOptions(
                _offset,
                _textHeight,
                _arrowSize,
                _fontName));
        if (Math.Abs(angle) > 1e-10)
            annotation = model.Rotate(annotation, _start, axis, angle);
        return engine.CreateShapeFromModel(model, annotation);
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var axis = (_end - _start).Normalized();
        var offsetVector = _normal.Cross(axis).Normalized() * _offset;
        var first = _start + offsetVector;
        var second = _end + offsetVector;
        return
        [
            new(this, _start, CadSnapType.Endpoint, 0),
            new(this, _end, CadSnapType.Endpoint, 1),
            new(this, first, CadSnapType.Vertex, 2),
            new(this, second, CadSnapType.Vertex, 3),
            new(this, first + (second - first) * 0.5, CadSnapType.Midpoint, 4)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var axis = (_end - _start).Normalized();
        var offsetDirection = _normal.Cross(axis).Normalized();
        var middle = _start + (_end - _start) * 0.5;
        var dimensionMiddle = middle + offsetDirection * _offset;
        var plane = new CadGripWorkPlane(
            _start,
            axis,
            offsetDirection);
        var offsetPlane = new CadGripWorkPlane(
            middle,
            axis,
            offsetDirection,
            LockPlane: true,
            LockedAngleDegrees: 90.0);

        return
        [
            new(
                this,
                0,
                _start,
                plane with { Origin = _end },
                _end,
                CadPrecisionInputKind.LengthAndAngle,
                CadGripKind.Vertex),
            new(
                this,
                1,
                _end,
                plane,
                _start,
                CadPrecisionInputKind.LengthAndAngle,
                CadGripKind.Vertex),
            new(
                this,
                2,
                dimensionMiddle,
                offsetPlane,
                middle,
                CadPrecisionInputKind.Length,
                CadGripKind.Midpoint)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        switch (index)
        {
            case 0:
                if ((_end - targetPoint).Length <= 1e-9)
                    return;
                _start = targetPoint;
                break;

            case 1:
                if ((targetPoint - _start).Length <= 1e-9)
                    return;
                _end = targetPoint;
                break;

            case 2:
            {
                var axis = (_end - _start).Normalized();
                var middle = _start + (_end - _start) * 0.5;
                var offsetDirection = _normal.Cross(axis).Normalized();
                var offset = (targetPoint - middle).Dot(offsetDirection);
                if (!double.IsFinite(offset))
                    return;
                _offset = offset;
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        ValidatePoints(_start, _end);
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadLengthDimensionEntity(
            _start,
            _end,
            _normal,
            _offset,
            _textHeight,
            _arrowSize,
            _fontName));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadLengthDimensionEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));
        _start = value._start;
        _end = value._end;
        _normal = value._normal;
        _offset = value._offset;
        _textHeight = value._textHeight;
        _arrowSize = value._arrowSize;
        _fontName = value._fontName;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        _start = Translated(_start, displacement);
        _end = Translated(_end, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _start = CadTransformMath.RotatePoint(_start, center, axis, angleDegrees);
        _end = CadTransformMath.RotatePoint(_end, center, axis, angleDegrees);
        _normal = CadTransformMath.RotateVector(_normal, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _start = CadTransformMath.ScalePoint(_start, center, factor);
        _end = CadTransformMath.ScalePoint(_end, center, factor);
        _offset *= factor;
        _textHeight *= factor;
        _arrowSize *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private static void ValidatePoints(OcctPoint3d start, OcctPoint3d end)
    {
        if (!start.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (!end.IsFinite || (end - start).Length <= 1e-9)
            throw new ArgumentOutOfRangeException(nameof(end));
    }

    internal static JsonObject WriteGeometry(CadLengthDimensionEntity entity) =>
        new()
        {
            ["start"] = CadEntityJson.Point(entity.Start),
            ["end"] = CadEntityJson.Point(entity.End),
            ["normal"] = CadEntityJson.Vector(entity.Normal),
            ["offset"] = entity.Offset,
            ["textHeight"] = entity.TextHeight,
            ["arrowSize"] = entity.ArrowSize,
            ["fontName"] = entity.FontName
        };

    internal static CadLengthDimensionEntity ReadGeometry(JsonObject data)
    {
        var fontName = data["fontName"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fontName))
            throw new InvalidDataException(
                "CAD dimension font cannot be empty.");
        return new CadLengthDimensionEntity(
            CadEntityJson.ReadPoint(data, "start"),
            CadEntityJson.ReadPoint(data, "end"),
            CadEntityJson.ReadVector(data, "normal"),
            CadEntityJson.ReadDouble(data, "offset"),
            CadEntityJson.ReadDouble(data, "textHeight"),
            CadEntityJson.ReadDouble(data, "arrowSize"),
            fontName);
    }
}
