namespace OCCAD;

/// <summary>
/// UI-independent property projection corresponding to OCCTBIM-Source's
/// EntityProperty contract. It is metadata over canonical domain state, not a
/// second value store.
/// </summary>
public sealed record CadEntityProperty(
    string Name,
    string DisplayName,
    object? Value,
    object? DefaultValue,
    string Group,
    bool ReadOnly,
    int Order,
    CadPropertyEditorKind EditorType,
    IReadOnlyDictionary<string, object?> EditorParams);

public static class CadPropertyService
{
    public static IReadOnlyList<CadEntityProperty> Describe(
        CadWorkspace workspace,
        object target)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(target);

        return CadPropertyCatalog
            .Describe(target)
            .Select(descriptor => Project(workspace, target, descriptor))
            .ToArray();
    }

    public static bool TryApply(
        CadWorkspace workspace,
        IReadOnlyList<CadEntity> targets,
        string propertyName,
        object? value,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var applied = CadPropertyTransaction.TryApply(
            workspace,
            targets,
            propertyName.Trim(),
            value,
            out var failure);
        error = failure?.Message;
        return applied;
    }

    public static bool TryApply(
        CadWorkspace workspace,
        IReadOnlyList<CadEntity> targets,
        IReadOnlyDictionary<string, object?> values,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(values);

        var applied = CadPropertyTransaction.TryApply(
            workspace,
            targets,
            values,
            out var failure);
        error = failure?.Message;
        return applied;
    }

    private static CadEntityProperty Project(
        CadWorkspace workspace,
        object target,
        CadPropertyDescriptor descriptor)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["semantic"] = descriptor.Value.Semantic,
            ["valueType"] = descriptor.PropertyType
        };

        if (descriptor.Value.Minimum is { } minimum)
            parameters["minimum"] = minimum;
        if (descriptor.Value.Maximum is { } maximum)
            parameters["maximum"] = maximum;
        if (descriptor.Unit is { } unit)
            parameters["unit"] = unit;
        if (descriptor.ByLayerProperty is { } byLayerProperty)
            parameters["byLayerProperty"] = byLayerProperty;

        var choices = descriptor.GetChoices(workspace);
        if (choices.Count > 0)
            parameters["choices"] = choices.ToArray();

        var value = descriptor.GetValue(target);
        if (descriptor.Value.Semantic == CadValueSemantic.Layer &&
            value is string layerReference)
        {
            value = workspace.Layers.GetRequired(layerReference).Name;
        }

        return new CadEntityProperty(
            descriptor.Name,
            descriptor.DisplayName,
            value,
            DefaultValue: value,
            descriptor.Category,
            descriptor.IsReadOnly,
            descriptor.Order,
            descriptor.Editor,
            parameters);
    }
}
