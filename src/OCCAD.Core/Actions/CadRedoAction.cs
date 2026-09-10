namespace OCCAD;

public sealed class CadRedoAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "edit.redo";
    public override string DisplayName => "Redo";
    public override string Description => "Redo the last undone model change";
    public override string? Shortcut => "Ctrl+Y";

    public override bool CanExecute() => Workspace.History.CanRedo;

    public override void Execute() => Workspace.Redo();
}
