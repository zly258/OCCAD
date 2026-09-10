namespace OCCAD;

public sealed class CadSelectAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "select";
    public override string DisplayName => "Select";

    public override void Execute() => Workspace.Tools.CancelCurrent();
}
