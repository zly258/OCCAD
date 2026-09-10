namespace OCCAD;

public abstract class CadAction
{
    protected CadAction(CadWorkspace workspace)
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    protected CadWorkspace Workspace { get; }

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string Description => DisplayName;
    public virtual string? Shortcut => null;
    public virtual bool IsRepeatable => false;

    public virtual bool CanExecute() => true;

    public abstract void Execute();
}

public sealed class CadActionEventArgs(CadAction action) : EventArgs
{
    public CadAction Action { get; } = action ?? throw new ArgumentNullException(nameof(action));
}

public sealed class CadActionFailedEventArgs(
    CadAction action,
    Exception exception) : EventArgs
{
    public CadAction Action { get; } =
        action ?? throw new ArgumentNullException(nameof(action));

    public Exception Exception { get; } =
        exception ?? throw new ArgumentNullException(nameof(exception));
}
