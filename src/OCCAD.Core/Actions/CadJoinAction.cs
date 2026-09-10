namespace OCCAD;

public sealed class CadJoinAction(CadWorkspace workspace)
    : CadAction(workspace)
{
    public override string Id => "modify.join";
    public override string DisplayName => "Join";
    public override string Description =>
        "Join a connected linear chain or compatible arc chain.";

    public override bool CanExecute() =>
        Workspace.Selection.Selected.Count >= 2 &&
        IsSupportedSelection(
            Workspace.Selection.Selected);

    public override void Execute()
    {
        var sources = Workspace.Selection.Selected
            .Where(Workspace.Document.IsEntitySelectable)
            .ToArray();

        if (!CadJoinGeometry.TryJoin(
                sources,
                out var joined))
        {
            throw new InvalidOperationException(
                "Selected curves must form one compatible connected, non-branching chain.");
        }

        Workspace.ReplaceEntities(
            sources,
            [joined],
            "Join");
    }

    private static bool IsSupportedSelection(
        IReadOnlyList<CadEntity> selection)
    {
        if (selection.Count < 2)
            return false;

        return selection.All(
            static entity =>
                entity is CadLineEntity or
                CadArcEntity or
                CadPolylineEntity { Closed: false } or
                CadPathEntity { Closed: false });
    }
}
