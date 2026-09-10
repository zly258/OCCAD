namespace OCCAD;

public sealed class CadToolAction : CadAction
{
    private readonly string _id;
    private readonly string _displayName;
    private readonly string _toolId;
    private readonly bool _requiresSelection;
    private readonly IReadOnlyDictionary<string, string> _initialParameters;

    public CadToolAction(
        CadWorkspace workspace,
        string id,
        string displayName,
        string toolId,
        bool requiresSelection = false,
        IReadOnlyDictionary<string, string>? initialParameters = null) : base(workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        _id = id;
        _displayName = displayName;
        _toolId = toolId;
        _requiresSelection = requiresSelection;
        _initialParameters = initialParameters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                initialParameters,
                StringComparer.OrdinalIgnoreCase);
    }

    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string Description => $"Start {DisplayName} tool";
    public override bool IsRepeatable => true;
    public bool RequiresSelection => _requiresSelection;

    public override bool CanExecute() =>
        !_requiresSelection || Workspace.Selection.Selected.Count > 0;

    public override void Execute()
    {
        if (!CanExecute())
            throw new InvalidOperationException($"Action '{Id}' requires a selection.");

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
