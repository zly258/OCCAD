using OcctNet;

namespace OCCAD;

public sealed class CadViewAction : CadAction
{
    private readonly string _id;
    private readonly string _displayName;
    private readonly OcctViewOrientation? _orientation;

    public CadViewAction(
        CadWorkspace workspace,
        string id,
        string displayName,
        OcctViewOrientation? orientation = null) : base(workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (orientation is { } value && !Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(orientation));

        _id = id.Trim();
        _displayName = displayName.Trim();
        _orientation = orientation;
    }

    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string Description =>
        _orientation is null
            ? "Fit the current model in the viewport"
            : $"Set viewport to {DisplayName}";

    public override bool CanExecute() =>
        Workspace.Engine is { IsInitialized: true };

    public override void Execute()
    {
        var engine = Workspace.Engine ??
            throw new InvalidOperationException(
                "No OCCT engine is attached.");

        if (_orientation is { } orientation)
            engine.SetView(orientation);
        else
            engine.FitAll();
    }
}
