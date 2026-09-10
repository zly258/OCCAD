namespace OCCAD;

public sealed class CadClearModelAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "file.clear";
    public override string DisplayName => "Clear Model";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Document.Entities.Count > 0;

    public override void Execute() =>
        Workspace.ClearModel();
}
