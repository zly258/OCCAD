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

/// <summary>
/// Canonical layer registry. Layer identity and display name are deliberately
/// separate, matching the OCCTBIM-Source Layer/LayerManager contract.
/// </summary>
public sealed class CadLayerManager
{
    private readonly List<CadLayer> _layers = [];
    private readonly Dictionary<string, CadLayer> _layersById =
        new(StringComparer.OrdinalIgnoreCase);
    private CadLayer _current;

    public CadLayerManager()
    {
        _current = AddCore(CadLayer.DefaultId, "0");
    }

    public IReadOnlyList<CadLayer> Layers => _layers;
    public CadLayer Current => _current;

    public event EventHandler<CadLayerManagerChangedEventArgs>? Changed;

    public CadLayer Add(string name)
    {
        var normalized = NormalizeName(name);
        EnsureUniqueName(normalized, except: null);

        var layer = AddCore(CreateId(), normalized);
        Changed?.Invoke(
            this,
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.Added,
                layer));
        return layer;
    }

    internal CadLayer AddWithId(string id, string name)
    {
        var normalizedId = NormalizeId(id);
        var normalizedName = NormalizeName(name);
        EnsureUniqueId(normalizedId);
        EnsureUniqueName(normalizedName, except: null);

        var layer = AddCore(normalizedId, normalizedName);
        Changed?.Invoke(
            this,
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
            if (TryGetByName(name) is not null) continue;

            var layer = Add(name);
            SetCurrent(layer);
            return layer;
        }
    }

    public string GenerateUniqueName(string baseName)
    {
        var normalized = NormalizeName(baseName);
        if (TryGetByName(normalized) is null) return normalized;

        for (var index = 1; ; index++)
        {
            var candidate = $"{normalized}{index}";
            if (TryGetByName(candidate) is null) return candidate;
        }
    }

    /// <summary>
    /// Compatibility resolver during the LayerId migration. Stable id wins;
    /// display name is accepted only as a fallback.
    /// </summary>
    public CadLayer? TryGet(string reference)
    {
        var normalized = NormalizeName(reference);
        return TryGetById(normalized) ?? TryGetByName(normalized);
    }

    public CadLayer? TryGetById(string id)
    {
        var normalized = NormalizeId(id);
        return _layersById.GetValueOrDefault(normalized);
    }

    public CadLayer? TryGetByName(string name)
    {
        var normalized = NormalizeName(name);
        return _layers.FirstOrDefault(layer =>
            string.Equals(
                layer.Name,
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    public CadLayer GetRequired(string reference) =>
        TryGet(reference) ??
        throw new InvalidOperationException($"Layer '{reference}' does not exist.");

    public CadLayer GetRequiredById(string id) =>
        TryGetById(id) ??
        throw new InvalidOperationException($"Layer id '{id}' does not exist.");

    public CadLayer GetRequiredByName(string name) =>
        TryGetByName(name) ??
        throw new InvalidOperationException($"Layer '{name}' does not exist.");

    public void SetCurrent(string reference) =>
        SetCurrent(GetRequired(reference));

    public void SetCurrent(CadLayer layer)
    {
        EnsureOwned(layer);
        if (ReferenceEquals(_current, layer)) return;

        _current = layer;
        Changed?.Invoke(
            this,
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
        _layersById.Clear();
        _current = AddCore(CadLayer.DefaultId, "0");
        Changed?.Invoke(
            this,
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.Reset,
                _current));
    }

    internal void RestoreSnapshot(
        IReadOnlyList<CadLayer> layers,
        IReadOnlyList<CadLayerState> states,
        CadLayer current)
    {
        foreach (var layer in _layers)
            layer.Changed -= LayerChanged;

        _layers.Clear();
        _layersById.Clear();
        _layers.AddRange(layers);

        for (var i = 0; i < layers.Count; i++)
            layers[i].RenameCore(states[i].Name);
        for (var i = 0; i < layers.Count; i++)
            layers[i].RestoreState(states[i]);

        foreach (var layer in _layers)
        {
            EnsureUniqueId(layer.Id);
            _layersById.Add(layer.Id, layer);
            layer.Changed += LayerChanged;
        }

        _current = current;
        Changed?.Invoke(this, new(CadLayerManagerChangeKind.Reset, current));
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
            SetCurrent(GetRequiredById(CadLayer.DefaultId));

        layer.Changed -= LayerChanged;
        _layers.RemoveAt(index);
        _layersById.Remove(layer.Id);
        Changed?.Invoke(
            this,
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

        EnsureUniqueId(layer.Id);
        EnsureUniqueName(layer.Name, except: null);
        if (index < 0 || index > _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        layer.Changed += LayerChanged;
        _layers.Insert(index, layer);
        _layersById.Add(layer.Id, layer);
        Changed?.Invoke(
            this,
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.Added,
                layer));
    }

    private CadLayer AddCore(string id, string name)
    {
        var layer = new CadLayer(this, id, name);
        layer.Changed += LayerChanged;
        _layers.Add(layer);
        _layersById.Add(layer.Id, layer);
        return layer;
    }

    private void LayerChanged(object? sender, CadLayerChangedEventArgs e)
    {
        if (sender is not CadLayer layer) return;

        Changed?.Invoke(
            this,
            new CadLayerManagerChangedEventArgs(
                CadLayerManagerChangeKind.LayerChanged,
                layer,
                e.Kind,
                e.PreviousName));
    }

    private void EnsureOwned(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (!_layers.Contains(layer))
            throw new InvalidOperationException(
                "Layer does not belong to this manager.");
    }

    private void EnsureUniqueId(string id)
    {
        if (_layersById.ContainsKey(id))
            throw new InvalidOperationException($"Layer id '{id}' already exists.");
    }

    private void EnsureUniqueName(string name, CadLayer? except)
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

    private static string CreateId() => Guid.NewGuid().ToString("N");

    private static string NormalizeId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id.Trim();
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim();
    }
}
