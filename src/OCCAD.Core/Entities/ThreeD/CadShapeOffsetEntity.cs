using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadShapeOffsetEntity : CadEntity
{
    private CadEntity _source;
    private double _offset;

    public CadShapeOffsetEntity(
        CadEntity source,
        double offset) : base("Shape Offset")
    {
        _source = CadSolidFeatureGeometry.Snapshot(source);
        ValidateOffset(offset);
        _offset = offset;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Geometry")]
    public double Offset
    {
        get => _offset;
        set
        {
            ValidateOffset(value);
            SetGeometry(ref _offset, value);
        }
    }

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var source = _source.BuildShape(engine);
        try
        {
            return engine.Offset(
                source,
                _offset,
                tolerance: 1e-4,
                hideInput: true);
        }
        finally
        {
            if (engine.ContainsObject(source.Id))
                engine.Delete(source);
        }
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var center =
            CadSolidFeatureGeometry.ApproximateCenter(_source);
        return [new(this, center, CadSnapType.Center, 0)];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var center =
            CadSolidFeatureGeometry.ApproximateCenter(_source);
        return [new(this, 0, center, Kind: CadGripKind.Center)];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (index != 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var center =
            CadSolidFeatureGeometry.ApproximateCenter(_source);
        Translate(
            CadTransformMath.Between(
                center,
                targetPoint));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadShapeOffsetEntity(
                _source,
                _offset));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadShapeOffsetEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _source = value._source.Duplicate();
        _offset = value._offset;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        _source.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _source.Rotate(center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _source.Scale(center, factor);
        _offset *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadShapeOffsetEntity entity) =>
        new()
        {
            ["source"] =
                CadSolidFeatureGeometry.Write(entity._source),
            ["offset"] = entity._offset
        };

    internal static CadShapeOffsetEntity ReadGeometry(JsonObject data) =>
        new(
            CadSolidFeatureGeometry.Read(
                data["source"] as JsonObject ??
                throw new FormatException(
                    "Shape offset source is missing.")),
            CadEntityJson.ReadDouble(data, "offset"));

    private static void ValidateOffset(double value)
    {
        if (!double.IsFinite(value) ||
            Math.Abs(value) <= 1e-9)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Offset must be finite and non-zero.");
    }
}
