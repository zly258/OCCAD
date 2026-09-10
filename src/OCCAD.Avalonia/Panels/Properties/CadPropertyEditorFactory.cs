using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Resolves editor behavior from ordered property rules. The inspector owns
/// Avalonia control instances and transactions; Core owns value conversion.
/// </summary>
internal static class CadPropertyEditorFactory
{
    public static CadPropertyEditorKind Resolve(CadPropertyDescriptor descriptor, bool entityContext) => descriptor.Editor;
    public static string? ByLayerProperty(CadPropertyDescriptor descriptor) => descriptor.ByLayerProperty;

    public static bool CanEditAsText(
        CadPropertyDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Value.Semantic is
                   CadValueSemantic.Point or CadValueSemantic.Vector ||
               descriptor.PropertyType == typeof(string) ||
               CadValueTextConverter.IsNumericType(descriptor.PropertyType) ||
               descriptor.Converter.CanConvertFrom(typeof(string));
    }

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
