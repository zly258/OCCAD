namespace OCCAD;

public sealed class CadSelectAllAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "select.all";
    public override string DisplayName => "Select All";
    public override string Description => "Select all selectable entities";
    public override string? Shortcut => "Ctrl+A";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Document.Entities.Any(Workspace.Document.IsEntitySelectable);

    public override void Execute()
    {
        var entities = Workspace.Document.Entities
            .Where(Workspace.Document.IsEntitySelectable)
            .ToArray();
        Workspace.Selection.Apply(
            entities,
            CadSelectionOperation.Replace,
            entities.LastOrDefault());
    }
}
