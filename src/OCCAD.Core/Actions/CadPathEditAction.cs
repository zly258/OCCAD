namespace OCCAD;

public enum CadPathEditKind
{
    Reverse,
    Open,
    Close
}

public sealed class CadPathEditAction(
    CadWorkspace workspace,
    string id,
    string displayName,
    CadPathEditKind kind)
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

    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string Description => $"{DisplayName} for the selected Path.";
    public override bool IsRepeatable => true;

    public override bool CanExecute()
    {
        var path = SelectedPath();
        if (path is null)
            return false;

        return kind switch
        {
            CadPathEditKind.Reverse => true,
            CadPathEditKind.Open => path.Closed && path.SegmentCount > 1,
            CadPathEditKind.Close => !path.Closed,
            _ => false
        };
    }

    public override void Execute()
    {
        var path = SelectedPath() ??
            throw new InvalidOperationException(
                $"{DisplayName} requires exactly one editable Path.");

        if (!CanExecute())
            throw new InvalidOperationException(
                $"{DisplayName} is not valid for the selected Path.");

        Workspace.ApplyGeneratedGeometryChange(
            [path],
            DisplayName,
            entity =>
            {
                var target = (CadPathEntity)entity;
                switch (kind)
                {
                    case CadPathEditKind.Reverse:
                        target.Reverse();
                        break;
                    case CadPathEditKind.Open:
                        target.Open();
                        break;
                    case CadPathEditKind.Close:
                        target.Close();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind));
                }
            });
    }

    private CadPathEntity? SelectedPath()
    {
        if (Workspace.Selection.Selected.Count != 1 ||
            Workspace.Selection.Selected[0] is not CadPathEntity path ||
            !Workspace.Document.IsEntitySelectable(path))
            return null;

        return path;
    }
}
