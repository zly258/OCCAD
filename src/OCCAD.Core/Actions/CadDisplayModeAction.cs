using OcctNet;

namespace OCCAD;

public sealed class CadDisplayModeAction : CadAction
{
    private readonly string _id;
    private readonly string _displayName;
    private readonly OcctDisplayMode _mode;

    public CadDisplayModeAction(
        CadWorkspace workspace,
        string id,
        string displayName,
        OcctDisplayMode mode) : base(workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        _id = id.Trim();
        _displayName = displayName.Trim();
        _mode = mode;
    }

    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string Description =>
        $"Set selected entities or the full document to {DisplayName}";

    public override bool CanExecute() =>
        Workspace.Tools.ActiveTool is null &&
        Workspace.Document.Entities.Count > 0;

    public override void Execute() =>
        Workspace.SetDisplayMode(_mode);
}
