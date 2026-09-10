using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadEdgeChamferEntity : CadFeatureEntity
{
    private CadEntity _source;
    private int[] _edgeIndices;
    private double _distance;

    public CadEdgeChamferEntity(
        CadEntity source,
        IEnumerable<int> edgeIndices,
        double distance) : base("Edge Chamfer")
    {
        _source = CadSolidFeatureGeometry.Snapshot(source);
        _edgeIndices =
            CadEdgeFeatureGeometry.NormalizeIndices(edgeIndices);
        ValidatePositive(distance, nameof(distance));
        _distance = distance;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Geometry")]
    public double Distance
    {
        get => _distance;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _distance, value);
        }
    }

    [Category("Geometry"), ReadOnly(true)]
    public int EdgeCount => _edgeIndices.Length;

    [Browsable(false)]
    public override IReadOnlyList<CadFeatureInputDescriptor> Inputs =>
        [CapturedInput("Source", _source)];

    [Browsable(false)]
    public IReadOnlyList<int> EdgeIndices => _edgeIndices;

    protected override OcctShape BuildFeatureResult(OcctEngine engine)
    {
        var source = _source.BuildShape(engine);
        try
        {
            return engine.ChamferEdges(
                source,
                _edgeIndices,
                _distance,
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

    public override void MoveGrip(
        int index,
        OcctPoint3d targetPoint)
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
            new CadEdgeChamferEntity(
                _source,
                _edgeIndices,
                _distance));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadEdgeChamferEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _source = value._source.Duplicate();
        _edgeIndices = value._edgeIndices.ToArray();
        _distance = value._distance;
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
        _distance *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadEdgeChamferEntity entity) =>
        new()
        {
            ["source"] =
                CadSolidFeatureGeometry.Write(entity._source),
            ["edgeIndices"] =
                CadEdgeFeatureGeometry.WriteIndices(entity._edgeIndices),
            ["distance"] = entity._distance
        };

    internal static CadEdgeChamferEntity ReadGeometry(
        JsonObject data) =>
        new(
            CadSolidFeatureGeometry.Read(
                data["source"] as JsonObject ??
                throw new FormatException(
                    "Edge chamfer source is missing.")),
            CadEdgeFeatureGeometry.ReadIndices(data),
            CadEntityJson.ReadDouble(data, "distance"));
}
