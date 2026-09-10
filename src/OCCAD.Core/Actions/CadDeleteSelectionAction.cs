namespace OCCAD;

/// <summary>
/// Deletes the current selectable entity selection as one undoable operation.
/// </summary>
public sealed class CadDeleteSelectionAction : CadAction
{
    public CadDeleteSelectionAction(CadWorkspace workspace) : base(workspace)
    {
    }

    public override string Id => "edit.delete";
    public override string DisplayName => "Delete";
    public override string Description => "Delete selected objects";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Selection.Selected.Any(Workspace.Document.IsEntitySelectable);

    public override void Execute()
    {
        var targets = Workspace.Selection.Selected
            .Where(Workspace.Document.IsEntitySelectable)
            .ToArray();
        if (targets.Length == 0)
            return;

        Workspace.DeleteEntities(targets);
    }
}
