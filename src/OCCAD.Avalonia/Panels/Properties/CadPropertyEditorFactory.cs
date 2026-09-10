using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Resolves editor behavior from Core property descriptors. Avalonia owns the
/// control instances; Core owns property semantics, conversion, and validation.
/// </summary>
internal static class CadPropertyEditorFactory
{
    public static CadPropertyEditorKind Resolve(CadPropertyDescriptor descriptor, bool entityContext)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var editor = descriptor.Editor;

        // Entity appearance properties use a compound ByLayer editor. Its
        // nested value control must resolve the real value kind instead of
        // recursively producing another ByLayer editor.
        if (!entityContext && editor == CadPropertyEditorKind.ByLayer)
        {
            return descriptor.Value.Semantic switch
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

        return editor;
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
