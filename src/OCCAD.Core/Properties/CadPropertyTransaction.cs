using System.ComponentModel;

namespace OCCAD;

/// <summary>
/// Applies property changes to document entities as one logical transaction.
/// Either every target reaches the requested state and one history entry is
/// recorded, or all targets are restored to their captured state.
/// </summary>
public static class CadPropertyTransaction
{
    public static bool TryApply(
        CadWorkspace workspace,
        IEnumerable<CadEntity> entities,
        string propertyName,
        object? value,
        out Exception? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return TryApply(
            workspace,
            entities,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [propertyName.Trim()] = value
            },
            $"Property {propertyName.Trim()}",
            out error);
    }

    public static bool TryApply(
        CadWorkspace workspace,
        IEnumerable<CadEntity> entities,
        IReadOnlyDictionary<string, object?> values,
        out Exception? error) =>
        TryApply(
            workspace,
            entities,
            values,
            "Properties",
            out error);

    private static bool TryApply(
        CadWorkspace workspace,
        IEnumerable<CadEntity> entities,
        IReadOnlyDictionary<string, object?> values,
        string historyName,
        out Exception? error)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(values);

        var requested = NormalizeValues(workspace, values);
        if (requested.Count == 0)
        {
            error = null;
            return false;
        }

        var targets = entities
            .Distinct()
            .Where(workspace.Document.Entities.Contains)
            .ToArray();
        if (targets.Length == 0)
        {
            error = null;
            return false;
        }

        var descriptors = new Dictionary<CadEntity, Dictionary<string, PropertyDescriptor>>();
        var anyChange = false;

        foreach (var entity in targets)
        {
            var entityDescriptors = new Dictionary<string, PropertyDescriptor>(StringComparer.Ordinal);
            var properties = TypeDescriptor.GetProperties(entity, true);

            foreach (var (propertyName, value) in requested)
            {
                var descriptor = properties.Find(propertyName, ignoreCase: false);
                if (descriptor is null)
                {
                    error = new InvalidOperationException(
                        $"Property '{propertyName}' is not available on '{entity.EntityType}'.");
                    return false;
                }
                if (descriptor.IsReadOnly)
                {
                    error = new InvalidOperationException(
                        $"Property '{propertyName}' is read-only on '{entity.EntityType}'.");
                    return false;
                }

                entityDescriptors.Add(propertyName, descriptor);
                anyChange |=
                    !Equals(descriptor.GetValue(entity), value) ||
                    RequiresAppearanceOverride(entity, propertyName);
            }

            descriptors.Add(entity, entityDescriptors);
        }

        if (!anyChange)
        {
            error = null;
            return true;
        }

        try
        {
            CadTransaction.ApplyEntities(
                workspace,
                targets,
                historyName,
                entity =>
                {
                    foreach (var (propertyName, value) in requested)
                    {
                        ApplyAppearanceOverride(entity, propertyName);
                        descriptors[entity][propertyName].SetValue(entity, value);
                    }
                });
            error = null;
            return true;
        }
        catch (Exception failure)
        {
            error = failure;
            return false;
        }
    }

    private static IReadOnlyDictionary<string, object?> NormalizeValues(
        CadWorkspace workspace,
        IReadOnlyDictionary<string, object?> values)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (rawName, rawValue) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rawName);
            var name = rawName.Trim();
            object? value = rawValue;

            if ((string.Equals(name, nameof(CadEntity.LayerId), StringComparison.Ordinal) ||
                 string.Equals(name, nameof(CadEntity.Layer), StringComparison.Ordinal)) &&
                value is string layerReference)
            {
                value = workspace.Layers.GetRequired(layerReference).Id;
            }

            if (!normalized.TryAdd(name, value))
                throw new ArgumentException(
                    $"Property '{name}' is duplicated.",
                    nameof(values));
        }

        return normalized;
    }

    private static bool RequiresAppearanceOverride(
        CadEntity entity,
        string propertyName) =>
        propertyName switch
        {
            nameof(CadEntity.Color) => entity.ColorByLayer,
            nameof(CadEntity.LineWidth) => entity.LineWidthByLayer,
            nameof(CadEntity.LineStyle) => entity.LineStyleByLayer,
            _ => false
        };

    private static void ApplyAppearanceOverride(
        CadEntity entity,
        string propertyName)
    {
        switch (propertyName)
        {
            case nameof(CadEntity.Color):
                entity.ColorByLayer = false;
                break;
            case nameof(CadEntity.LineWidth):
                entity.LineWidthByLayer = false;
                break;
            case nameof(CadEntity.LineStyle):
                entity.LineStyleByLayer = false;
                break;
        }
    }
}
