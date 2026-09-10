using OCCAD;

namespace OCCAD.Avalonia;

internal enum CadPropertyEditorKind
{
    ReadOnly,
    Layer,
    ByLayer,
    Boolean,
    Choice,
    Color,
    Text
}

/// <summary>
/// Resolves editor behavior from ordered property rules. The inspector owns
/// Avalonia control instances and transactions; Core owns value conversion.
/// </summary>
internal static class CadPropertyEditorFactory
{
    private static readonly IReadOnlyList<EditorRule> Rules =
    [
        new(
            static context => context.Descriptor.IsReadOnly,
            CadPropertyEditorKind.ReadOnly),
        new(
            static context =>
                context.EntityContext &&
                context.Descriptor.Value.Semantic == CadValueSemantic.Layer,
            CadPropertyEditorKind.Layer),
        new(
            static context =>
                context.EntityContext &&
                ByLayerProperty(context.Descriptor) is not null,
            CadPropertyEditorKind.ByLayer),
        new(
            static context =>
                context.Descriptor.Value.Semantic == CadValueSemantic.Boolean,
            CadPropertyEditorKind.Boolean),
        new(
            static context =>
                context.Descriptor.Value.Semantic is
                    CadValueSemantic.Enum or CadValueSemantic.Choice,
            CadPropertyEditorKind.Choice),
        new(
            static context =>
                context.Descriptor.Value.Semantic == CadValueSemantic.Color,
            CadPropertyEditorKind.Color),
        new(
            static context =>
                context.Descriptor.Value.Semantic is
                    CadValueSemantic.Point or CadValueSemantic.Vector,
            CadPropertyEditorKind.Text),
        new(
            static context => CanEditAsText(context.Descriptor),
            CadPropertyEditorKind.Text)
    ];

    public static CadPropertyEditorKind Resolve(
        CadPropertyDescriptor descriptor,
        bool entityContext)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var context = new EditorContext(descriptor, entityContext);
        foreach (var rule in Rules)
        {
            if (rule.Matches(context))
                return rule.Kind;
        }

        return CadPropertyEditorKind.ReadOnly;
    }

    public static string? ByLayerProperty(
        CadPropertyDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Name switch
        {
            nameof(CadEntity.Color) => nameof(CadEntity.ColorByLayer),
            nameof(CadEntity.LineStyle) => nameof(CadEntity.LineStyleByLayer),
            nameof(CadEntity.LineWidth) => nameof(CadEntity.LineWidthByLayer),
            _ => null
        };
    }

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

    private readonly record struct EditorContext(
        CadPropertyDescriptor Descriptor,
        bool EntityContext);

    private sealed record EditorRule(
        Func<EditorContext, bool> Matches,
        CadPropertyEditorKind Kind);
}
