namespace OCCAD;

public sealed class CadIsolateAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "view.isolate";
    public override string DisplayName => "Isolate";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Selection.Selected.Count > 0;

    public override void Execute() =>
        Workspace.IsolateEntities(Workspace.Selection.Selected.ToArray());
}
