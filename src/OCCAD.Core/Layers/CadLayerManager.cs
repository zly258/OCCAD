namespace OCCAD;

public enum CadLayerManagerChangeKind
{
    Added,
    Removed,
    CurrentChanged,
    LayerChanged,
    Reset
}

public sealed class CadLayerManagerChangedEventArgs(
    CadLayerManagerChangeKind kind,
    CadLayer? layer,
    CadLayerChangeKind? layerChangeKind = null,
    string? previousName = null) : EventArgs
{
    public CadLayerManagerChangeKind Kind { get; } = kind;
    public CadLayer? Layer { get; } = layer;
    public CadLayerChangeKind? LayerChangeKind { get; } = layerChangeKind;
    public string? PreviousName { get; } = previousName;
}

public sealed class CadLayerManager
{
    private readonly List<CadLayer> _layers = [];
    private CadLayer _current;

    public CadLayerManager()
    {
        _current = AddCore("0");
    }

    public IReadOnlyList<CadLayer> Layers => _layers;
    public CadLayer Current => _current;

    public event EventHandler<CadLayerManagerChangedEventArgs>? Changed;

    public CadLayer Add(string name)
    {
        var normalized = NormalizeName(name);
        EnsureUniqueName(normalized, except: null);

        var layer = AddCore(normalized);
        PublishChanged(
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.Added,
                layer));
        return layer;
    }

    public CadLayer AddNext()
    {
        for (var index = 1; ; index++)
        {
            var name = $"Layer{index}";
            if (TryGet(name) is not null) continue;

            var layer = Add(name);
            SetCurrent(layer);
            return layer;
        }
    }

    public string GenerateUniqueName(string baseName)
    {
        var normalized = NormalizeName(baseName);
        if (TryGet(normalized) is null) return normalized;

        for (var index = 1; ; index++)
        {
            var candidate = $"{normalized}{index}";
            if (TryGet(candidate) is null) return candidate;
        }
    }

    public CadLayer? TryGet(string name)
    {
        var normalized = NormalizeName(name);
        return _layers.FirstOrDefault(layer =>
            string.Equals(
                layer.Name,
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    public CadLayer GetRequired(string name) =>
        TryGet(name) ??
        throw new InvalidOperationException($"Layer '{name}' does not exist.");

    public void SetCurrent(string name) =>
        SetCurrent(GetRequired(name));

    public void SetCurrent(CadLayer layer)
    {
        EnsureOwned(layer);
        if (ReferenceEquals(_current, layer)) return;

        _current = layer;
        PublishChanged(
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.CurrentChanged,
                layer));
    }

    public void Rename(CadLayer layer, string name)
    {
        EnsureOwned(layer);
        var normalized = NormalizeName(name);

        if (layer.IsDefault)
        {
            if (string.Equals(
                normalized,
                layer.Name,
                StringComparison.OrdinalIgnoreCase))
                return;

            throw new InvalidOperationException(
                "Default layer '0' cannot be renamed.");
        }

        if (string.Equals(
            layer.Name,
            normalized,
            StringComparison.Ordinal))
            return;

        EnsureUniqueName(normalized, layer);
        layer.RenameCore(normalized);
    }

    public void Reset()
    {
        foreach (var layer in _layers)
            layer.Changed -= LayerChanged;

        _layers.Clear();
        _current = AddCore("0");
        PublishChanged(
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.Reset,
                _current));
    }

    internal void RestoreSnapshot(IReadOnlyList<CadLayer> layers,
        IReadOnlyList<CadLayerState> states, CadLayer current)
    {
        foreach (var layer in _layers) layer.Changed -= LayerChanged;
        _layers.Clear();
        _layers.AddRange(layers);
        for (var i = 0; i < layers.Count; i++) layers[i].RenameCore(states[i].Name);
        for (var i = 0; i < layers.Count; i++) layers[i].RestoreState(states[i]);
        _current = current;
        foreach (var layer in _layers) layer.Changed += LayerChanged;
        PublishChanged(new(CadLayerManagerChangeKind.Reset, current));
    }

    internal int IndexOf(CadLayer layer)
    {
        EnsureOwned(layer);
        return _layers.IndexOf(layer);
    }

    internal int Remove(CadLayer layer)
    {
        EnsureOwned(layer);
        if (layer.IsDefault)
            throw new InvalidOperationException(
                "Default layer '0' cannot be removed.");

        var index = _layers.IndexOf(layer);
        if (ReferenceEquals(_current, layer))
            SetCurrent(GetRequired("0"));

        layer.Changed -= LayerChanged;
        _layers.RemoveAt(index);
        PublishChanged(
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.Removed,
                layer));
        return index;
    }

    internal void Insert(CadLayer layer, int index)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (_layers.Contains(layer))
            throw new InvalidOperationException(
                "Layer already belongs to this manager.");

        EnsureUniqueName(layer.Name, except: null);
        if (index < 0 || index > _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        layer.Changed += LayerChanged;
        _layers.Insert(index, layer);
        PublishChanged(
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.Added,
                layer));
    }

    private CadLayer AddCore(string name)
    {
        var layer = new CadLayer(this, name);
        layer.Changed += LayerChanged;
        _layers.Add(layer);
        return layer;
    }

    private void LayerChanged(object? sender, CadLayerChangedEventArgs e)
    {
        if (sender is not CadLayer layer) return;

        PublishChanged(
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.LayerChanged,
                layer,
                e.Kind,
                e.PreviousName));
    }

    private void PublishChanged(CadLayerManagerChangedEventArgs args)
    {
        var handlers = Changed;
        if (handlers is null)
            return;

        foreach (EventHandler<CadLayerManagerChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception) when (IsRecoverableObserverFailure(exception))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"CadLayerManager Changed observer failed after state changed: {exception}");
            }
        }
    }

    private static bool IsRecoverableObserverFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private void EnsureOwned(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!_layers.Contains(layer))
            throw new InvalidOperationException(
                "Layer does not belong to this manager.");
    }

    private void EnsureUniqueName(
        string name,
        CadLayer? except)
    {
        var existing = _layers.FirstOrDefault(layer =>
            !ReferenceEquals(layer, except) &&
            string.Equals(
                layer.Name,
                name,
                StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new InvalidOperationException(
                $"Layer '{name}' already exists.");
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }
}
