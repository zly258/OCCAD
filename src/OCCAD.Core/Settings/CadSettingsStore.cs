using System.Text.Json;
using System.Text.Json.Nodes;

namespace OCCAD;

public sealed class CadSettingChangedEventArgs(
    string key,
    JsonNode? value) : EventArgs
{
    public string Key { get; } = key;
    public JsonNode? Value { get; } = value?.DeepClone();
}

/// <summary>
/// Application-level settings authority. The store is independent from any UI
/// framework; presentation layers edit keys, while Core bindings apply values
/// to the owning CAD services.
/// </summary>
public sealed class CadSettingsStore
{
    private readonly Dictionary<string, JsonNode?> _values =
        new(StringComparer.Ordinal);

    public event EventHandler<CadSettingChangedEventArgs>? Changed;

    public IReadOnlyCollection<string> Keys => _values.Keys;

    public bool Contains(string key) =>
        _values.ContainsKey(NormalizeKey(key));

    public T Get<T>(string key, T defaultValue = default!)
    {
        var normalized = NormalizeKey(key);
        if (!_values.TryGetValue(normalized, out var node) || node is null)
            return defaultValue;

        try
        {
            return node.Deserialize<T>() ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
        catch (NotSupportedException)
        {
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        var normalized = NormalizeKey(key);
        var node = JsonSerializer.SerializeToNode(value);
        if (_values.TryGetValue(normalized, out var current) &&
            JsonNode.DeepEquals(current, node))
            return;

        var hadPrevious = _values.TryGetValue(normalized, out var previous);
        var previousClone = previous?.DeepClone();
        _values[normalized] = node;

        try
        {
            PublishChanged(normalized, node);
        }
        catch (Exception failure)
        {
            if (hadPrevious)
                _values[normalized] = previousClone;
            else
                _values.Remove(normalized);

            var rollbackFailure = TryPublishRollback(
                normalized,
                hadPrevious ? previousClone : null);
            if (rollbackFailure is not null)
            {
                throw new AggregateException(
                    "Setting apply and rollback notification both failed.",
                    failure,
                    rollbackFailure);
            }

            throw;
        }
    }

    public void SetValues(IReadOnlyDictionary<string, JsonNode?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var requested = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            var normalized = NormalizeKey(key);
            if (!requested.TryAdd(normalized, value?.DeepClone()))
                throw new ArgumentException(
                    $"Setting key '{normalized}' is duplicated.",
                    nameof(values));
        }

        var changed = requested
            .Where(pair =>
                !_values.TryGetValue(pair.Key, out var current) ||
                !JsonNode.DeepEquals(current, pair.Value))
            .ToArray();
        if (changed.Length == 0)
            return;

        var before = SnapshotMutable();
        foreach (var pair in changed)
            _values[pair.Key] = pair.Value?.DeepClone();

        try
        {
            foreach (var pair in changed)
                PublishChanged(pair.Key, pair.Value);
        }
        catch (Exception failure)
        {
            Restore(before);
            var rollbackFailures = new List<Exception>();
            foreach (var pair in changed)
            {
                try
                {
                    before.TryGetValue(pair.Key, out var restored);
                    PublishChanged(pair.Key, restored);
                }
                catch (Exception rollbackFailure)
                {
                    rollbackFailures.Add(rollbackFailure);
                }
            }

            if (rollbackFailures.Count > 0)
            {
                throw new AggregateException(
                    "Settings batch apply failed and rollback notification was incomplete.",
                    new[] { failure }.Concat(rollbackFailures));
            }

            throw;
        }
    }

    public bool Remove(string key)
    {
        var normalized = NormalizeKey(key);
        if (!_values.TryGetValue(normalized, out var previous))
            return false;

        var previousClone = previous?.DeepClone();
        _values.Remove(normalized);
        try
        {
            PublishChanged(normalized, null);
            return true;
        }
        catch (Exception failure)
        {
            _values[normalized] = previousClone;
            var rollbackFailure = TryPublishRollback(normalized, previousClone);
            if (rollbackFailure is not null)
            {
                throw new AggregateException(
                    "Setting removal and rollback notification both failed.",
                    failure,
                    rollbackFailure);
            }

            throw;
        }
    }

    public IReadOnlyDictionary<string, JsonNode?> Snapshot() =>
        SnapshotMutable();

    public void Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Settings stream must be readable.", nameof(stream));

        var root = JsonNode.Parse(stream) as JsonObject ??
            throw new InvalidDataException("Settings root must be a JSON object.");

        _values.Clear();
        foreach (var (key, value) in root)
            _values[NormalizeKey(key)] = value?.DeepClone();
    }

    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite)
            throw new ArgumentException("Settings stream must be writable.", nameof(stream));

        var root = new JsonObject();
        foreach (var (key, value) in _values)
            root[key] = value?.DeepClone();

        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions { Indented = true });
        root.WriteTo(writer);
        writer.Flush();
    }

    public CadSettingsBinding Bind(CadWorkspace workspace) =>
        new(workspace, this);

    private Dictionary<string, JsonNode?> SnapshotMutable() =>
        _values.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.DeepClone(),
            StringComparer.Ordinal);

    private void Restore(IReadOnlyDictionary<string, JsonNode?> snapshot)
    {
        _values.Clear();
        foreach (var (key, value) in snapshot)
            _values[key] = value?.DeepClone();
    }

    private void PublishChanged(string key, JsonNode? value) =>
        Changed?.Invoke(this, new CadSettingChangedEventArgs(key, value));

    private Exception? TryPublishRollback(string key, JsonNode? value)
    {
        try
        {
            PublishChanged(key, value);
            return null;
        }
        catch (Exception failure)
        {
            return failure;
        }
    }

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key.Trim();
    }
}

public static class CadSettingKeys
{
    public const string SceneBackground = "scene.background";
    public const string PointerSensitivity = "interaction.pointerSensitivity";
    public const string GeometryPrecision = "geometry.precision";

    public const string SelectionTolerance = "selection.pixelTolerance";

    public const string GripSize = "grip.markerSize";
    public const string GripTolerance = "grip.pixelTolerance";

    public const string SnapEnabled = "snap.enabled";
    public const string SnapModes = "snap.modes";
    public const string SnapSize = "snap.markerSize";
    public const string SnapTolerance = "snap.pixelTolerance";

    public const string OrthogonalTrackingEnabled = "drafting.orthoEnabled";
    public const string PolarTrackingEnabled = "drafting.polarEnabled";
    public const string PolarIncrementDegrees = "drafting.polarIncrementDegrees";
    public const string TrackingToleranceDegrees = "drafting.trackingToleranceDegrees";
}
