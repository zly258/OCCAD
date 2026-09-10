namespace OCCAD;

public sealed class CadActionManager
{
    private readonly Dictionary<string, CadAction> _actions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CadAction> _shortcuts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _history = [];
    private const int HistoryLimit = 20;

    public IReadOnlyCollection<CadAction> Actions => _actions.Values;
    public IReadOnlyList<string> History => _history;

    public event EventHandler<CadActionEventArgs>? ActionStarted;
    public event EventHandler<CadActionEventArgs>? ActionFinished;
    public event EventHandler<CadActionFailedEventArgs>? ActionFailed;

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
        if (!action.CanExecute()) return false;

        ActionStarted?.Invoke(this, new CadActionEventArgs(action));
        try
        {
            action.Execute();
            if (action.IsRepeatable)
                AddHistory(action.Id);
            return true;
        }
        catch (Exception exception)
        {
            ActionFailed?.Invoke(this, new CadActionFailedEventArgs(action, exception));
            return false;
        }
        finally
        {
            ActionFinished?.Invoke(this, new CadActionEventArgs(action));
        }
    }

    public bool ExecuteLast()
    {
        if (_history.Count == 0)
            return false;

        var action = GetRequired(_history[0]);
        return action.IsRepeatable && Execute(action.Id);
    }

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
        _history.RemoveAll(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase));
        _history.Insert(0, id);
        if (_history.Count > HistoryLimit)
            _history.RemoveRange(HistoryLimit, _history.Count - HistoryLimit);
    }
}
