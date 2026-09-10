namespace OCCAD;

public sealed class CadActionManager
{
    private readonly Dictionary<string, CadAction> _actions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CadAction> _shortcuts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _history = [];
    private readonly Dictionary<string, CadCommandDescriptor> _commands =
        new(StringComparer.OrdinalIgnoreCase);
    private const int HistoryLimit = 20;

    public IReadOnlyCollection<CadCommandDescriptor> Commands => _commands.Values;
    public IReadOnlyCollection<CadAction> Actions => _actions.Values;
    public IReadOnlyList<string> History => _history;

    public event EventHandler<CadActionEventArgs>? ActionStarted;
    public event EventHandler<CadActionEventArgs>? ActionFinished;
    public event EventHandler<CadActionFailedEventArgs>? ActionFailed;

    public CadCommandDescriptor? Describe(string id) =>
        _commands.GetValueOrDefault(id);

    public CadCommandDescriptor? ResolveCommand(string text) =>
        Describe(text) ??
        Commands.FirstOrDefault(command =>
            command.Aliases.Contains(text, StringComparer.OrdinalIgnoreCase));

    public void Register(CadAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_actions.ContainsKey(action.Id))
            throw new InvalidOperationException(
                $"Action '{action.Id}' is already registered.");

        var shortcut = NormalizeShortcut(action.Shortcut);
        if (shortcut is not null &&
            _shortcuts.TryGetValue(shortcut, out var existing))
        {
            throw new InvalidOperationException(
                $"Shortcut '{shortcut}' is already assigned to action '{existing.Id}'.");
        }

        _actions.Add(action.Id, action);
        _commands.Add(action.Id, new CadCommandDescriptor(action));
        if (shortcut is not null)
            _shortcuts.Add(shortcut, action);
    }

    public CadAction? Find(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _actions.GetValueOrDefault(id);
    }

    public CadAction GetRequired(string id) =>
        Find(id) ?? throw new KeyNotFoundException(
            $"CAD action '{id}' is not registered.");

    public CadAction? FindByShortcut(string shortcut)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcut);
        return _shortcuts.GetValueOrDefault(
            NormalizeShortcut(shortcut)!);
    }

    public bool CanExecute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Find(id)?.CanExecute() == true;
    }

    public bool ExecuteShortcut(string shortcut)
    {
        var action = FindByShortcut(shortcut);
        return action is not null && Execute(action.Id);
    }

    public bool Execute(string id)
    {
        var action = GetRequired(id);
        if (!action.CanExecute())
            return false;

        PublishStarted(action);
        try
        {
            action.Execute();
            if (action.IsRepeatable)
                AddHistory(action.Id);
            return true;
        }
        catch (Exception exception) when (IsRecoverableActionFailure(exception))
        {
            PublishFailed(action, exception);
            return false;
        }
        finally
        {
            PublishFinished(action);
        }
    }

    public bool ExecuteLast()
    {
        if (_history.Count == 0)
            return false;

        var action = GetRequired(_history[0]);
        return action.IsRepeatable && Execute(action.Id);
    }

    private void PublishStarted(CadAction action) =>
        PublishObservers(
            ActionStarted,
            new CadActionEventArgs(action),
            "ActionStarted");

    private void PublishFinished(CadAction action) =>
        PublishObservers(
            ActionFinished,
            new CadActionEventArgs(action),
            "ActionFinished");

    private void PublishFailed(CadAction action, Exception failure) =>
        PublishObservers(
            ActionFailed,
            new CadActionFailedEventArgs(action, failure),
            "ActionFailed");

    private void PublishObservers<TEventArgs>(
        EventHandler<TEventArgs>? handlers,
        TEventArgs args,
        string eventName)
        where TEventArgs : EventArgs
    {
        if (handlers is null)
            return;

        foreach (EventHandler<TEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception) when (IsRecoverableObserverFailure(exception))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"{eventName} observer failed after action state changed: {exception}");
            }
        }
    }

    private static bool IsRecoverableActionFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static bool IsRecoverableObserverFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static string? NormalizeShortcut(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return null;

        return shortcut
            .Trim()
            .Replace(
                "Control+",
                "Ctrl+",
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal);
    }

    private void AddHistory(string id)
    {
        _history.RemoveAll(value =>
            string.Equals(value, id, StringComparison.OrdinalIgnoreCase));
        _history.Insert(0, id);
        if (_history.Count > HistoryLimit)
            _history.RemoveRange(HistoryLimit, _history.Count - HistoryLimit);
    }
}
