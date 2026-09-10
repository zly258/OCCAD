using System.ComponentModel;

namespace OCCAD;

/// <summary>
/// Applies one editable property to a set of document entities as one logical
/// transaction. Either every entity reaches the requested value and one history
/// entry is recorded, or every entity is restored to its captured state.
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
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var targets = entities
            .Distinct()
            .Where(workspace.Document.Entities.Contains)
            .ToArray();
        if (targets.Length == 0)
        {
            error = null;
            return false;
        }

        var descriptors = new PropertyDescriptor[targets.Length];
        var anyChange = false;
        for (var index = 0; index < targets.Length; index++)
        {
            var descriptor = TypeDescriptor
                .GetProperties(targets[index], true)
                .Find(propertyName, ignoreCase: false);
            if (descriptor is null)
            {
                error = new InvalidOperationException(
                    $"Property '{propertyName}' is not available on '{targets[index].EntityType}'.");
                return false;
            }
            if (descriptor.IsReadOnly)
            {
                error = new InvalidOperationException(
                    $"Property '{propertyName}' is read-only on '{targets[index].EntityType}'.");
                return false;
            }

            descriptors[index] = descriptor;
            anyChange |=
                !Equals(descriptor.GetValue(targets[index]), value) ||
                RequiresAppearanceOverride(targets[index], propertyName);
        }

        if (!anyChange)
        {
            error = null;
            return true;
        }

        try
        {
            var byEntity = targets.Select((entity, index) => (entity, descriptor: descriptors[index]))
                .ToDictionary(pair => pair.entity, pair => pair.descriptor);
            CadTransaction.ApplyEntities(workspace, targets, $"Property {propertyName}", entity =>
            {
                ApplyAppearanceOverride(entity, propertyName);
                byEntity[entity].SetValue(entity, value);
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
