namespace OCCAD;

public sealed class CadUndoAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "edit.undo";
    public override string DisplayName => "Undo";
    public override string Description => "Undo the last model change";
    public override string? Shortcut => "Ctrl+Z";

    public override bool CanExecute() => Workspace.History.CanUndo;

    public override void Execute() => Workspace.Undo();
}
