namespace OCCAD;

public sealed class CadRegionAction(CadWorkspace workspace)
    : CadAction(workspace)
{
    public override string Id => "model.region";
    public override string DisplayName => "Region";

    public override bool CanExecute() =>
        Workspace.Selection.Selected.Count == 1 &&
        CadPlanarProfileGeometry.IsSource(
            Workspace.Selection.Selected[0]);

    public override void Execute()
    {
        if (!CanExecute())
            throw new InvalidOperationException(
                "Region requires exactly one supported closed planar profile.");

        var source = Workspace.Selection.Selected[0];
        var region = new CadRegionEntity(source);
        Workspace.ReplaceEntities(
            [source],
            [region],
            "Region");
    }
}
