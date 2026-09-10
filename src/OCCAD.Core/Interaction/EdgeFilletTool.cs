namespace OCCAD;

public sealed class EdgeFilletTool : CadEdgeFeatureToolBase
{
    public override string Id => "edgefillet";
    public override string DisplayName => "Edge Fillet";

    protected override string ValueId => "Radius";
    protected override string ValueLabel => "Radius";

    protected override CadEntity CreateFeature(
        CadEntity source,
        IReadOnlyList<int> edgeIndices,
        double value) =>
        new CadEdgeFilletEntity(
            source,
            edgeIndices,
            value);
}
