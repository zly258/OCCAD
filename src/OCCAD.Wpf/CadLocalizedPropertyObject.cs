using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using OCCAD;
using OcctNet;

namespace OCCAD.Wpf;

internal sealed class CadLocalizedPropertyObject : ICustomTypeDescriptor
{
    private readonly object _target;
    private readonly CadWorkspace? _workspace;
    private readonly IReadOnlyList<CadEntity> _entitySelection;

    public CadLocalizedPropertyObject(
        object target,
        CadWorkspace? workspace = null,
        IReadOnlyList<CadEntity>? entitySelection = null)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _workspace = workspace;
        _entitySelection = entitySelection ?? Array.Empty<CadEntity>();
    }

    internal object Target => _target;

    public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(_target, true);
    public string? GetClassName() => TypeDescriptor.GetClassName(_target, true);
    public string? GetComponentName() => TypeDescriptor.GetComponentName(_target, true);
    public TypeConverter GetConverter() => TypeDescriptor.GetConverter(_target, true);
    public EventDescriptor? GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(_target, true);
    public PropertyDescriptor? GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(_target, true);
    public object? GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(_target, editorBaseType, true);
    public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(_target, true);
    public EventDescriptorCollection GetEvents(Attribute[]? attributes) => TypeDescriptor.GetEvents(_target, attributes, true);
    public object GetPropertyOwner(PropertyDescriptor? pd) => _target;

    public PropertyDescriptorCollection GetProperties() =>
        Wrap(TypeDescriptor.GetProperties(_target, true));

    public PropertyDescriptorCollection GetProperties(Attribute[]? attributes) =>
        Wrap(TypeDescriptor.GetProperties(_target, attributes, true));

    private PropertyDescriptorCollection Wrap(PropertyDescriptorCollection source)
    {
        var hideOrientation = _target is
            CadCircleEntity or
            CadArcEntity or
            CadEllipseEntity or
            CadRectangleEntity;

        var descriptors = source
            .Cast<PropertyDescriptor>()
            .Where(descriptor =>
                !hideOrientation ||
                !string.Equals(
                    descriptor.Category,
                    "Orientation",
                    StringComparison.OrdinalIgnoreCase))
            .Select(descriptor => new LocalizedPropertyDescriptor(
                descriptor,
                _workspace,
                _entitySelection))
            .ToArray();

        return new PropertyDescriptorCollection(descriptors, true);
    }

    private sealed class LocalizedPropertyDescriptor(
        PropertyDescriptor inner,
        CadWorkspace? workspace,
        IReadOnlyList<CadEntity> entitySelection)
        : PropertyDescriptor(inner)
    {
        private bool IsEntityLayer =>
            inner.Name == nameof(CadEntity.Layer) &&
            workspace is not null &&
            entitySelection.Count > 0;

        public override Type ComponentType => inner.ComponentType;
        public override bool IsReadOnly => IsEntityLayer ? false : inner.IsReadOnly;
        public override Type PropertyType => inner.PropertyType;
        public override TypeConverter Converter =>
            PropertyType == typeof(double)
                ? CadThreeDecimalDoubleConverter.Instance
                : PropertyType == typeof(bool)
                    ? CadBooleanConverter.Instance
                    : PropertyType == typeof(OcctLineStyle)
                        ? CadEnumConverter<OcctLineStyle>.Instance
                        : PropertyType == typeof(OcctDisplayMode)
                            ? CadEnumConverter<OcctDisplayMode>.Instance
                            : PropertyType == typeof(OcctMaterial)
                                ? CadEnumConverter<OcctMaterial>.Instance
                                : inner.Name == nameof(CadEntity.EntityType) && inner.IsReadOnly
                                    ? CadEntityTypeConverter.Instance
                                    : IsEntityLayer
                                        ? new CadLayerNameConverter(workspace!)
                                        : inner.Converter;
        public override bool CanResetValue(object component) => inner.CanResetValue(UnwrapRequired(component));
        public override object? GetValue(object? component) => inner.GetValue(Unwrap(component));
        public override void ResetValue(object component) => inner.ResetValue(UnwrapRequired(component));
        public override void SetValue(object? component, object? value)
        {
            if (IsEntityLayer)
            {
                if (value is not string layerName)
                    throw new ArgumentException(
                        "Layer value must be a layer name.",
                        nameof(value));

                var normalized = layerName.Trim();
                if (normalized.Length == 0)
                    throw new ArgumentException(
                        "Layer name cannot be empty.",
                        nameof(value));

                if (entitySelection.All(entity =>
                        string.Equals(
                            entity.Layer,
                            normalized,
                            StringComparison.OrdinalIgnoreCase)))
                    return;

                workspace!.AssignEntitiesToLayer(
                    entitySelection,
                    normalized);
                OnValueChanged(component, EventArgs.Empty);
                return;
            }

            inner.SetValue(Unwrap(component), value);
        }
        public override bool ShouldSerializeValue(object component) => inner.ShouldSerializeValue(UnwrapRequired(component));

        public override string DisplayName =>
            CadLanguageManager.Text($"Cad.Property.{inner.Name}", inner.DisplayName);

        public override string Category =>
            CadLanguageManager.Text($"Cad.Category.{inner.Category}", inner.Category);

        public override string Description =>
            string.IsNullOrWhiteSpace(inner.Description)
                ? string.Empty
                : CadLanguageManager.Text($"Cad.Description.{inner.Name}", inner.Description);

        public override object? GetEditor(Type editorBaseType)
        {
            if (PropertyType == typeof(Color) &&
                editorBaseType == typeof(UITypeEditor))
                return new CadColorEditor();

            return inner.GetEditor(editorBaseType);
        }

        private static object? Unwrap(object? component) =>
            component is CadLocalizedPropertyObject wrapper ? wrapper._target : component;

        private static object UnwrapRequired(object component) =>
            component is CadLocalizedPropertyObject wrapper ? wrapper._target : component;
    }
}

internal sealed class CadThreeDecimalDoubleConverter : DoubleConverter
{
    public static CadThreeDecimalDoubleConverter Instance { get; } = new();

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (destinationType == typeof(string) && value is double number)
            return number.ToString("0.000", culture ?? CultureInfo.CurrentCulture);

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is string text &&
            double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                culture ?? CultureInfo.CurrentCulture,
                out var number))
            return number;

        return base.ConvertFrom(context, culture, value);
    }
}

internal sealed class CadBooleanConverter : BooleanConverter
{
    public static CadBooleanConverter Instance { get; } = new();

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is bool flag)
            return flag
                ? CadLanguageManager.Text("Cad.Value.True", "True")
                : CadLanguageManager.Text("Cad.Value.False", "False");
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            if (text == "是" || text.Equals(CadLanguageManager.Text("Cad.Value.True", "True"), StringComparison.CurrentCultureIgnoreCase)) return true;
            if (text == "否" || text.Equals(CadLanguageManager.Text("Cad.Value.False", "False"), StringComparison.CurrentCultureIgnoreCase)) return false;
        }
        return base.ConvertFrom(context, culture, value);
    }
}
internal sealed class CadEnumConverter<T>() : EnumConverter(typeof(T)) where T : struct, Enum
{
    public static CadEnumConverter<T> Instance { get; } = new();

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is T item)
            return Label(item);
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            foreach (var item in Enum.GetValues<T>())
                if (text.Equals(Label(item), StringComparison.CurrentCultureIgnoreCase)) return item;
        }
        // Retain canonical English enum input and normal PropertyGrid validation.
        return base.ConvertFrom(context, culture, value);
    }

    private static string Label(T value) =>
        CadLanguageManager.Text($"Cad.Value.{typeof(T).Name}.{value}", value.ToString());
}

internal sealed class CadEntityTypeConverter : StringConverter
{
    public static CadEntityTypeConverter Instance { get; } = new();

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is string type)
            return CadLanguageManager.Text(
                $"Cad.Text.{type.Replace(" ", string.Empty, StringComparison.Ordinal)}",
                type);
        return base.ConvertTo(context, culture, value, destinationType);
    }
}



internal sealed class CadLayerNameConverter(CadWorkspace workspace) : StringConverter
{
    private readonly CadWorkspace _workspace =
        workspace ?? throw new ArgumentNullException(nameof(workspace));

    public override bool GetStandardValuesSupported(
        ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(
        ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(
        ITypeDescriptorContext? context) =>
        new(_workspace.Layers.Layers
            .Select(static layer => layer.Name)
            .ToArray());
}
