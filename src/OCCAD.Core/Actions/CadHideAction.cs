namespace OCCAD;

public sealed class CadHideAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "view.hide";
    public override string DisplayName => "Hide";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Selection.Selected.Count > 0;

    public override void Execute() =>
        Workspace.HideEntities(Workspace.Selection.Selected.ToArray());
}
