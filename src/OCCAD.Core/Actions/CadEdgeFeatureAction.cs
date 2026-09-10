namespace OCCAD;

public sealed class CadEdgeFeatureAction(
    CadWorkspace workspace,
    string id,
    string displayName,
    string toolId)
    : CadAction(workspace)
{
    private readonly string _id =
        string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Action id is required.", nameof(id))
            : id.Trim();

    private readonly string _displayName =
        string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Display name is required.", nameof(displayName))
            : displayName.Trim();

    private readonly string _toolId =
        string.IsNullOrWhiteSpace(toolId)
            ? throw new ArgumentException("Tool id is required.", nameof(toolId))
            : toolId.Trim();

    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string Description =>
        $"Start {DisplayName} for selected solid edges.";
    public override bool IsRepeatable => true;

    public override bool CanExecute() =>
        CadEdgeFeatureGeometry.CurrentEdges(Workspace).Count > 0;

    public override void Execute()
    {
        if (!CanExecute())
            throw new InvalidOperationException(
                $"{DisplayName} requires one or more selected edges on the same solid.");

        if (!Workspace.Tools.Activate(_toolId))
            throw new InvalidOperationException(
                $"Tool '{_toolId}' is not registered.");
    }
}
