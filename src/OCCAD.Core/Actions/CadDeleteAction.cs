namespace OCCAD;

public sealed class CadDeleteAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "edit.delete";
    public override string DisplayName => "Delete";
    public override string Description => "Delete selected entities";
    public override string? Shortcut => "Delete";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Selection.Selected.Count > 0;

    public override void Execute() =>
        Workspace.DeleteEntities(Workspace.Selection.Selected.ToArray());
}
