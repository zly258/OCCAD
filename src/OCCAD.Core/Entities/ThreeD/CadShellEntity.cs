using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadShellEntity : CadFeatureEntity
{
    private CadEntity _source;
    private int _faceIndex;
    private double _thickness;

    public CadShellEntity(
        CadEntity source,
        int faceIndex,
        double thickness) : base("Shell")
    {
        _source = CadSolidFeatureGeometry.Snapshot(source);
        if (faceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        ValidateThickness(thickness);
        _faceIndex = faceIndex;
        _thickness = thickness;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Geometry"), ReadOnly(true)]
    public int RemovedFaceIndex => _faceIndex;

    [Category("Geometry")]
    public double Thickness
    {
        get => _thickness;
        set
        {
            ValidateThickness(value);
            SetGeometry(ref _thickness, value);
        }
    }

    public override IReadOnlyList<CadFeatureInputDescriptor> Inputs =>
        [CapturedInput("Source", _source)];

    protected override OcctShape BuildFeatureResult(OcctEngine engine)
    {
        var source = _source.BuildShape(engine);
        try
        {
            return engine.MakeThickSolid(
                source,
                _faceIndex,
                _thickness,
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
            new CadShellEntity(
                _source,
                _faceIndex,
                _thickness));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadShellEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _source = value._source.Duplicate();
        _faceIndex = value._faceIndex;
        _thickness = value._thickness;
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
        _thickness *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadShellEntity entity) =>
        new()
        {
            ["source"] =
                CadSolidFeatureGeometry.Write(entity._source),
            ["faceIndex"] = entity._faceIndex,
            ["thickness"] = entity._thickness
        };

    internal static CadShellEntity ReadGeometry(JsonObject data) =>
        new(
            CadSolidFeatureGeometry.Read(
                data["source"] as JsonObject ??
                throw new FormatException(
                    "Shell source is missing.")),
            CadEntityJson.ReadInt(data, "faceIndex"),
            CadEntityJson.ReadDouble(data, "thickness"));

    private static void ValidateThickness(double value)
    {
        if (!double.IsFinite(value) ||
            Math.Abs(value) <= 1e-9)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Thickness must be finite and non-zero.");
    }
}
