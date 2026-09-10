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

public sealed class CadTransparencyAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "display.transparent";
    public override string DisplayName => "Transparent";
    public override string Description => "Toggle transparent display for entities";
    public override bool CanExecute() => Workspace.Tools.ActiveTool is null && Workspace.Document.Entities.Count > 0;
    public override void Execute()
    {
        var primary = Workspace.Selection.Primary ?? Workspace.Document.Entities.FirstOrDefault();
        var targetAlpha = primary is not null && Math.Abs(primary.Transparency - 0.5) < 0.05 ? 0.0 : 0.5;
        Workspace.SetTransparency(targetAlpha);
    }
}

public sealed class CadHiddenLineAction(CadWorkspace workspace) : CadAction(workspace)
{
    public override string Id => "display.hiddenline";
    public override string DisplayName => "Hidden Line";
    public override string Description => "Display models with hidden line mode";
    public override bool CanExecute() => Workspace.Tools.ActiveTool is null && Workspace.Document.Entities.Count > 0;
    public override void Execute() => Workspace.SetHiddenLineMode();
}
