namespace OCCAD;

public sealed class CadNewDocumentAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "file.new";
    public override string DisplayName => "New";
    public override string Description => "Create a new CAD document";
    public override string? Shortcut => "Ctrl+N";

    public override void Execute() => Workspace.ResetDocument();
}
