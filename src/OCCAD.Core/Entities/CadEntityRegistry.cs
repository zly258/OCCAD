using System.Text.Json.Nodes;

namespace OCCAD;

public sealed record CadEntityDescriptor(
    string Id,
    string DisplayName,
    string LocalizationKey,
    Type EntityType,
    Func<CadEntity>? Factory,
    Func<CadEntity, JsonObject>? WriteGeometry,
    Func<JsonObject, CadEntity>? ReadGeometry)
{
    public bool SupportsCreation => Factory is not null;
    public bool SupportsPersistence =>
        WriteGeometry is not null && ReadGeometry is not null;
}

public sealed class CadEntityRegistry
{
    private readonly Dictionary<string, CadEntityDescriptor> _descriptors =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CadEntityDescriptor> Descriptors => _descriptors.Values;

    public void Register<T>(
        string id,
        string displayName,
        Func<T> factory,
        Func<T, JsonObject>? writeGeometry = null,
        Func<JsonObject, T>? readGeometry = null)
        where T : CadEntity
    {
        ArgumentNullException.ThrowIfNull(factory);
        RegisterCore(
            id,
            displayName,
            typeof(T),
            () => factory() ?? throw new InvalidOperationException(
                $"Entity factory '{id}' returned null."),
            writeGeometry is null ? null : entity => writeGeometry((T)entity),
            readGeometry is null ? null : data => readGeometry(data));
    }

    public void RegisterPersistent<T>(
        string id,
        string displayName,
        Func<T, JsonObject> writeGeometry,
        Func<JsonObject, T> readGeometry)
        where T : CadEntity
    {
        ArgumentNullException.ThrowIfNull(writeGeometry);
        ArgumentNullException.ThrowIfNull(readGeometry);
        RegisterCore(
            id,
            displayName,
            typeof(T),
            factory: null,
            entity => writeGeometry((T)entity),
            data => readGeometry(data));
    }

    public bool Contains(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _descriptors.ContainsKey(id.Trim());
    }

    public CadEntityDescriptor GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var normalized = id.Trim();
        return _descriptors.TryGetValue(normalized, out var descriptor)
            ? descriptor
            : throw new KeyNotFoundException(
                $"Entity '{normalized}' is not registered.");
    }

    public CadEntityDescriptor GetRequired(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var matches = _descriptors.Values
            .Where(descriptor => descriptor.EntityType == entity.GetType())
            .Take(2)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException(
                $"Entity type '{entity.GetType().Name}' is not registered."),
            _ => throw new InvalidOperationException(
                $"Entity type '{entity.GetType().Name}' has multiple registrations.")
        };
    }

    public CadEntity Create(string id)
    {
        var descriptor = GetRequired(id);
        if (descriptor.Factory is null)
            throw new InvalidOperationException(
                $"Entity '{descriptor.Id}' requires document context and cannot be created by the registry.");
        return ValidateCreated(descriptor, descriptor.Factory());
    }

    public JsonObject WriteGeometry(CadEntity entity)
    {
        var descriptor = GetRequired(entity);
        if (descriptor.WriteGeometry is null)
            throw new InvalidOperationException(
                $"Entity '{descriptor.Id}' does not define persistence.");
        return descriptor.WriteGeometry(entity);
    }

    public CadEntity ReadGeometry(string id, JsonObject geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var descriptor = GetRequired(id);
        if (descriptor.ReadGeometry is null)
            throw new InvalidOperationException(
                $"Entity '{descriptor.Id}' does not define persistence.");
        return ValidateCreated(descriptor, descriptor.ReadGeometry(geometry));
    }

    public bool StateEquals(CadEntity left, CadEntity right) =>
        left.Name == right.Name && left.Layer == right.Layer &&
        left.Visible == right.Visible && left.Selectable == right.Selectable &&
        left.Color == right.Color && left.ColorByLayer == right.ColorByLayer &&
        left.LineWidth == right.LineWidth && left.LineWidthByLayer == right.LineWidthByLayer &&
        left.LineStyle == right.LineStyle && left.LineStyleByLayer == right.LineStyleByLayer &&
        left.Transparency == right.Transparency && left.DisplayMode == right.DisplayMode &&
        left.Material == right.Material && GeometryEquals(left, right);

    public bool GeometryEquals(
        CadEntity left,
        CadEntity right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.GetType() != right.GetType())
            return false;

        return left.Placement == right.Placement &&
               JsonNode.DeepEquals(
                   WriteGeometry(left),
                   WriteGeometry(right));
    }

    private void RegisterCore(
        string id,
        string displayName,
        Type entityType,
        Func<CadEntity>? factory,
        Func<CadEntity, JsonObject>? writeGeometry,
        Func<JsonObject, CadEntity>? readGeometry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(entityType);
        if ((writeGeometry is null) != (readGeometry is null))
            throw new ArgumentException(
                "Entity persistence requires both write and read geometry callbacks.");

        var normalizedId = id.Trim();
        var normalizedDisplayName = displayName.Trim();
        var descriptor = new CadEntityDescriptor(
            normalizedId,
            normalizedDisplayName,
            $"Cad.Text.{normalizedDisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)}",
            entityType,
            factory,
            writeGeometry,
            readGeometry);

        if (!_descriptors.TryAdd(normalizedId, descriptor))
            throw new InvalidOperationException(
                $"Entity '{normalizedId}' is already registered.");
    }

    private static CadEntity ValidateCreated(
        CadEntityDescriptor descriptor,
        CadEntity entity)
    {
        if (entity.GetType() != descriptor.EntityType)
            throw new InvalidOperationException(
                $"Entity persistence '{descriptor.Id}' returned " +
                $"'{entity.GetType().Name}', expected " +
                $"'{descriptor.EntityType.Name}'.");

        if (!string.Equals(
                entity.EntityType,
                descriptor.DisplayName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Entity registry display name '{descriptor.DisplayName}' " +
                $"does not match entity type '{entity.EntityType}'.");

        return entity;
    }
}
