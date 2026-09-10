namespace OCCAD;

public sealed class CadSelectInvertAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "select.invert";
    public override string DisplayName => "Invert Selection";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Document.Entities.Any(Workspace.Document.IsEntitySelectable);

    public override void Execute()
    {
        var selected = Workspace.Selection.Selected.ToHashSet();
        var entities = Workspace.Document.Entities
            .Where(Workspace.Document.IsEntitySelectable)
            .Where(entity => !selected.Contains(entity))
            .ToArray();
        Workspace.Selection.Apply(
            entities,
            CadSelectionOperation.Replace,
            entities.LastOrDefault());
    }
}
