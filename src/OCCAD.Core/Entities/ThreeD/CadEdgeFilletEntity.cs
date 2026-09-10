using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadEdgeFilletEntity : CadEntity
{
    private CadEntity _source;
    private int[] _edgeIndices;
    private double _radius;

    public CadEdgeFilletEntity(
        CadEntity source,
        IEnumerable<int> edgeIndices,
        double radius) : base("Edge Fillet")
    {
        _source = CadSolidFeatureGeometry.Snapshot(source);
        _edgeIndices =
            CadEdgeFeatureGeometry.NormalizeIndices(edgeIndices);
        ValidatePositive(radius, nameof(radius));
        _radius = radius;
        DisplayMode = OcctDisplayMode.Shaded;
    }

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

    [Category("Geometry"), ReadOnly(true)]
    public int EdgeCount => _edgeIndices.Length;

    [Browsable(false)]
    public IReadOnlyList<int> EdgeIndices => _edgeIndices;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var source = _source.BuildShape(engine);
        try
        {
            return engine.FilletEdges(
                source,
                _edgeIndices,
                _radius,
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
            new CadEdgeFilletEntity(
                _source,
                _edgeIndices,
                _radius));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadEdgeFilletEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _source = value._source.Duplicate();
        _edgeIndices = value._edgeIndices.ToArray();
        _radius = value._radius;
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
        _radius *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadEdgeFilletEntity entity) =>
        new()
        {
            ["source"] =
                CadSolidFeatureGeometry.Write(entity._source),
            ["edgeIndices"] =
                CadEdgeFeatureGeometry.WriteIndices(entity._edgeIndices),
            ["radius"] = entity._radius
        };

    internal static CadEdgeFilletEntity ReadGeometry(
        JsonObject data) =>
        new(
            CadSolidFeatureGeometry.Read(
                data["source"] as JsonObject ??
                throw new FormatException(
                    "Edge fillet source is missing.")),
            CadEdgeFeatureGeometry.ReadIndices(data),
            CadEntityJson.ReadDouble(data, "radius"));
}
