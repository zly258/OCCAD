namespace OCCAD;

public sealed class CadToolAction : CadAction
{
    private readonly string _id;
    private readonly string _displayName;
    private readonly string _toolId;
    private readonly IReadOnlyDictionary<string, string> _initialParameters;

    public CadToolAction(
        CadWorkspace workspace,
        string id,
        string displayName,
        string toolId,
        IReadOnlyDictionary<string, string>? initialParameters = null) : base(workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        _id = id;
        _displayName = displayName;
        _toolId = toolId;
        _initialParameters = initialParameters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                initialParameters,
                StringComparer.OrdinalIgnoreCase);
    }

    public string ToolId => _toolId;
    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string Description => $"Start {DisplayName} tool";
    public override bool IsRepeatable => true;

    public override bool CanExecute() =>
        Workspace.Engine is { IsInitialized: true } &&
        Workspace.Tools.IsRegistered(_toolId);

    public override void Execute()
    {
        if (!Workspace.Tools.Activate(_toolId))
            throw new InvalidOperationException($"Tool '{_toolId}' is not registered.");

        foreach (var parameter in _initialParameters)
        {
            if (Workspace.Tools.ActiveTool?.TrySetParameter(
                    parameter.Key,
                    parameter.Value) != true)
            {
                Workspace.Tools.CancelCurrent();
                throw new InvalidOperationException(
                    $"Tool '{_toolId}' rejected initial parameter '{parameter.Key}'.");
            }
        }
    }
}
