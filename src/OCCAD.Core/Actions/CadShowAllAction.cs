namespace OCCAD;

public sealed class CadShowAllAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "view.showall";
    public override string DisplayName => "Show All";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Document.Entities.Any(static entity => !entity.Visible);

    public override void Execute() =>
        Workspace.ShowAllEntities();
}
