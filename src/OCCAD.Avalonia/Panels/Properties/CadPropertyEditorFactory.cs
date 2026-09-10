using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Resolves editor behavior from ordered property rules. The inspector owns
/// Avalonia control instances and transactions; Core owns value conversion.
/// </summary>
internal static class CadPropertyEditorFactory
{
    public static CadPropertyEditorKind Resolve(CadPropertyDescriptor descriptor, bool entityContext)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var editor = descriptor.Editor;

        // Entity appearance properties (Color / LineStyle / LineWidth) use a
        // compound ByLayer editor in the entity PropertyGrid. The value editor
        // nested inside that compound control resolves the underlying value
        // kind rather than recursively resolving to ByLayer again.
        if (!entityContext && editor == CadPropertyEditorKind.ByLayer)
        {
            editor = descriptor.Value.Semantic switch
            {
                CadValueSemantic.Boolean => CadPropertyEditorKind.Boolean,
                CadValueSemantic.Enum or CadValueSemantic.Choice => CadPropertyEditorKind.Choice,
                CadValueSemantic.Color => CadPropertyEditorKind.Color,
                CadValueSemantic.Point or CadValueSemantic.Vector => CadPropertyEditorKind.Text,
                _ => CadValueTextConverter.IsNumericType(descriptor.PropertyType)
                    ? CadPropertyEditorKind.Numeric
                    : descriptor.PropertyType == typeof(string) ||
                      descriptor.Converter.CanConvertFrom(typeof(string))
                        ? CadPropertyEditorKind.Text
                        : CadPropertyEditorKind.ReadOnly
            };
        }

        // Numeric values intentionally share the normal left-aligned TextBox
        // surface; parsing and validation remain in Core.
        return editor == CadPropertyEditorKind.Numeric
            ? CadPropertyEditorKind.Text
            : editor;
    }

    public static string? ByLayerProperty(CadPropertyDescriptor descriptor) => descriptor.ByLayerProperty;

    public static string FormatValue(
        CadPropertyDescriptor descriptor,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return CadValueTextConverter.Format(
            descriptor.Value,
            descriptor.PropertyType,
            descriptor.Converter,
            value);
    }

    public static bool TryParseValue(
        CadPropertyDescriptor descriptor,
        string text,
        out object? value)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(text);
        return CadValueTextConverter.TryParse(
            descriptor.Value,
            descriptor.PropertyType,
            descriptor.Converter,
            text,
            out value);
    }
}
