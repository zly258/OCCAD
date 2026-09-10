namespace OCCAD;

public sealed class CadShellAction(
    CadWorkspace workspace)
    : CadAction(workspace)
{
    public override string Id => "solid.shell";
    public override string DisplayName => "Shell";
    public override bool IsRepeatable => true;

    public override bool CanExecute() =>
        TrySelection(out _, out _);

    public override void Execute()
    {
        if (!TrySelection(out _, out _))
            throw new InvalidOperationException(
                "Shell requires exactly one selected face on a solid.");

        if (!Workspace.Tools.Activate("shell"))
            throw new InvalidOperationException(
                "Tool 'shell' is not registered.");
    }

    private bool TrySelection(
        out CadEntity source,
        out int faceIndex)
    {
        source = null!;
        faceIndex = -1;

        var items = Workspace.Subobjects.Selected;
        if (items.Count != 1)
            return false;

        var item = items[0];
        if (item.SubshapeType != OcctNet.OcctShapeType.Face ||
            item.SubshapeIndex < 0 ||
            !CadSolidFeatureGeometry.IsSolid(item.Entity))
            return false;

        source = item.Entity;
        faceIndex = item.SubshapeIndex;
        return true;
    }
}
