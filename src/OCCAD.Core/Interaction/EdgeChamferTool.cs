namespace OCCAD;

public sealed class EdgeChamferTool : CadEdgeFeatureToolBase
{
    public override string Id => "edgechamfer";
    public override string DisplayName => "Edge Chamfer";

    protected override string ValueId => "Distance";
    protected override string ValueLabel => "Distance";

    protected override CadEntity CreateFeature(
        CadEntity source,
        IReadOnlyList<int> edgeIndices,
        double value) =>
        new CadEdgeChamferEntity(
            source,
            edgeIndices,
            value);
}
