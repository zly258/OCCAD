namespace OCCAD;

/// <summary>
/// Legacy command placeholder kept only until command registration is fully
/// migrated to CadDocumentSession. New/open/save are application-session
/// operations because they require unsaved-change policy and platform storage.
/// </summary>
[Obsolete("Use CadDocumentSession through the application shell. This action cannot execute.")]
public sealed class CadNewDocumentAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "file.new";
    public override string DisplayName => "New";
    public override string Description => "Create a new CAD document through the document session";
    public override string? Shortcut => null;

    public override bool CanExecute() => false;

    public override void Execute() =>
        throw new InvalidOperationException(
            "New document is owned by CadDocumentSession and cannot bypass the application document lifecycle.");
}
