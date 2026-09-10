namespace OCCAD;

public sealed class CadToolRegistry
{
    private readonly Dictionary<string, Func<CadTool>> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Ids => _factories.Keys;

    public void Register<TTool>(string id)
        where TTool : CadTool, new() =>
        Register(id, static () => new TTool());

    public void Register(string id, Func<CadTool> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(factory);

        var normalized = id.Trim();
        if (!_factories.TryAdd(normalized, factory))
            throw new InvalidOperationException($"Tool '{normalized}' is already registered.");
    }

    public bool Contains(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _factories.ContainsKey(id.Trim());
    }

    internal bool TryCreate(string id, out CadTool? tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var normalized = id.Trim();
        if (!_factories.TryGetValue(normalized, out var factory))
        {
            tool = null;
            return false;
        }

        tool = factory() ??
            throw new InvalidOperationException($"Tool factory '{normalized}' returned null.");

        if (!string.Equals(tool.Id, normalized, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Tool registry id '{normalized}' does not match tool id '{tool.Id}'.");

        return true;
    }
}
