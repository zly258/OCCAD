namespace OCCAD;

public sealed class CadRegionAction(CadWorkspace workspace)
    : CadAction(workspace)
{
    public override string Id => "model.region";
    public override string DisplayName => "Region";

    public override bool CanExecute()
    {
        var selected = Workspace.Selection.Selected;
        return selected.Count >= 1 &&
               selected.All(CadPlanarProfileGeometry.IsSource) &&
               ResolveOuter(selected) is not null;
    }

    public override void Execute()
    {
        var selected = Workspace.Selection.Selected.ToArray();
        var outer = ResolveOuter(selected);
        if (outer is null ||
            !selected.All(CadPlanarProfileGeometry.IsSource))
        {
            throw new InvalidOperationException(
                "Region requires one outer closed profile and optional coplanar hole profiles.");
        }

        var holes = selected
            .Where(entity => !ReferenceEquals(entity, outer))
            .ToArray();

        var outerGeometry =
            outer.CreateWorldGeometrySnapshot();
        var holeGeometry = holes
            .Select(static hole =>
                hole.CreateWorldGeometrySnapshot())
            .ToArray();

        if (!CadPlanarProfileGeometry.AreCoplanar(
                outerGeometry,
                holeGeometry))
        {
            throw new InvalidOperationException(
                "Region outer and hole profiles must be coplanar.");
        }

        var region = new CadRegionEntity(
            outer,
            holes);

        Workspace.ReplaceEntities(
            selected,
            [region],
            "Region");
    }

    private CadEntity? ResolveOuter(
        IReadOnlyList<CadEntity> selected)
    {
        var primary = Workspace.Selection.Primary;
        if (primary is not null &&
            selected.Contains(primary))
            return primary;

        return selected.Count > 0
            ? selected[0]
            : null;
    }
}
