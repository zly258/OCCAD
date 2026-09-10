using System.ComponentModel;

namespace OCCAD;

public enum CadPropertyEditorKind
{
    ReadOnly,
    Layer,
    ByLayer,
    Boolean,
    Choice,
    Color,
    Numeric,
    Text
}



[AttributeUsage(
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = true)]
public sealed class CadPropertyAttribute(
    CadValueSemantic semantic) : Attribute
{
    public CadValueSemantic Semantic { get; } = semantic;
    public int Order { get; set; } = int.MaxValue;
    public double Minimum { get; set; } = double.NaN;
    public double Maximum { get; set; } = double.NaN;
}

public sealed class CadPropertyDescriptor
{
    internal CadPropertyDescriptor(
        PropertyDescriptor property,
        CadValueDescriptor value,
        int order)
    {
        Property = property ??
            throw new ArgumentNullException(nameof(property));
        Value = value ??
            throw new ArgumentNullException(nameof(value));
        Order = order;
    }

    internal PropertyDescriptor Property { get; }
    public CadValueDescriptor Value { get; }
    public string Name => Property.Name;
    public string DisplayName => Property.DisplayName;
    public string Category => NormalizeCategory(Property.Category, Property.Name);
    public string Description => Property.Description;
    public Type PropertyType => Property.PropertyType;
    public bool IsReadOnly => Property.IsReadOnly;
    public bool IsBrowsable => Property.IsBrowsable;
    public TypeConverter Converter => Property.Converter;
    public int Order { get; }
    public string DisplayNameKey => Value.DisplayKey;
    public string CategoryKey => $"Cad.Category.{Category}";
    public string? Unit => Value.Semantic switch
    {
        CadValueSemantic.Angle => "°",
        CadValueSemantic.Length => "drawing-unit",
        _ => null
    };
    public string? ByLayerProperty => typeof(CadEntity).IsAssignableFrom(Property.ComponentType) ? Name switch
    {
        nameof(CadEntity.Color) => nameof(CadEntity.ColorByLayer),
        nameof(CadEntity.LineWidth) => nameof(CadEntity.LineWidthByLayer),
        nameof(CadEntity.LineStyle) => nameof(CadEntity.LineStyleByLayer),
        _ => null
    } : null;
    public CadPropertyEditorKind Editor => IsReadOnly ? CadPropertyEditorKind.ReadOnly :
        ByLayerProperty is not null ? CadPropertyEditorKind.ByLayer : Value.Semantic switch
        {
            CadValueSemantic.Layer => CadPropertyEditorKind.Layer,
            CadValueSemantic.Boolean => CadPropertyEditorKind.Boolean,
            CadValueSemantic.Enum or CadValueSemantic.Choice => CadPropertyEditorKind.Choice,
            CadValueSemantic.Color => CadPropertyEditorKind.Color,
            CadValueSemantic.Point or CadValueSemantic.Vector => CadPropertyEditorKind.Text,
            _ => CadValueTextConverter.IsNumericType(PropertyType) ? CadPropertyEditorKind.Numeric :
                PropertyType == typeof(string) || Converter.CanConvertFrom(typeof(string))
                    ? CadPropertyEditorKind.Text : CadPropertyEditorKind.ReadOnly
        };

    public IReadOnlyList<string> GetChoices(CadWorkspace workspace) =>
        Value.Semantic == CadValueSemantic.Layer
            ? workspace.Layers.Layers.Select(layer => layer.Name).ToArray()
            : Value.Choices;

    public object? GetValue(object target) =>
        Property.GetValue(target);

    public void SetValue(
        object target,
        object? value) =>
        Property.SetValue(target, value);

    public static string NormalizeCategory(string rawCategory, string propertyName)
    {
        if (string.Equals(propertyName, "EntityType", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Name", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Layer", StringComparison.OrdinalIgnoreCase))
        {
            return "General";
        }

        if (string.Equals(propertyName, "X", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Y", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Z", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Origin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Position", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Placement", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Center", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "CenterX", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "CenterY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "CenterZ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "StartX", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "StartY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "StartZ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "EndX", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "EndY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "EndZ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "StartPoint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "EndPoint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "CenterPoint", StringComparison.OrdinalIgnoreCase) ||
            rawCategory is "Position" or "Placement" or "Location" or "Transform" or "Orientation")
        {
            return "Position";
        }

        if (string.Equals(propertyName, "Color", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "LineWidth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "LineStyle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Transparency", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Visible", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "DisplayMode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Material", StringComparison.OrdinalIgnoreCase) ||
            rawCategory is "Display" or "Appearance")
        {
            return "Display";
        }

        if (string.Equals(propertyName, "Length", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Width", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Height", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Radius", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "MajorRadius", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "MinorRadius", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Diameter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Angle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "AngleDegrees", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "StartAngle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "EndAngle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Area", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "SurfaceArea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Volume", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "Circumference", StringComparison.OrdinalIgnoreCase) ||
            rawCategory is "Geometry" or "Measurement" or "Dimension")
        {
            return "Geometry";
        }

        if (rawCategory is "Data" or "Parameters" or "Custom")
        {
            return "Data";
        }

        return string.IsNullOrWhiteSpace(rawCategory) ? "General" : rawCategory;
    }
}

public static class CadPropertyCatalog
{
    private static readonly HashSet<string> InternalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ViewerObject",
        "ViewerShape",
        "ViewerObjectId",
        "NativeHandle",
        "InternalId",
        "DebugInfo",
        "AisObject"
    };

    public static IReadOnlyList<CadPropertyDescriptor>
        Describe(object target, bool includeInternal = false)
    {
        ArgumentNullException.ThrowIfNull(target);

        return TypeDescriptor
            .GetProperties(target, true)
            .Cast<PropertyDescriptor>()
            .Where(property =>
                property.IsBrowsable &&
                (includeInternal || !InternalNames.Contains(property.Name)))
            .Select(Create)
            .OrderBy(static property =>
                CategoryOrder(property.Category))
            .ThenBy(static property =>
                property.Category,
                StringComparer.Ordinal)
            .ThenBy(static property =>
                property.Order)
            .ThenBy(static property =>
                property.DisplayName,
                StringComparer.Ordinal)
            .ToArray();
    }

    public static int CategoryOrder(string category) =>
        category switch
        {
            "General" => 0,
            "Geometry" => 10,
            "Position" => 20,
            "Display" => 30,
            "Data" => 40,
            _ => 100
        };

    private static CadPropertyDescriptor Create(
        PropertyDescriptor property)
    {
        var attribute =
            property.Attributes[
                typeof(CadPropertyAttribute)]
            as CadPropertyAttribute;

        var semantic =
            attribute?.Semantic ??
            CadValueDescriptor.InferSemantic(
                property.Name,
                property.PropertyType);

        double? minimum =
            attribute is not null &&
            double.IsFinite(attribute.Minimum)
                ? attribute.Minimum
                : semantic ==
                    CadValueSemantic.Transparency
                    ? 0.0
                    : null;

        double? maximum =
            attribute is not null &&
            double.IsFinite(attribute.Maximum)
                ? attribute.Maximum
                : semantic ==
                    CadValueSemantic.Transparency
                    ? 1.0
                    : null;

        IReadOnlyList<string>? choices =
            property.PropertyType.IsEnum
                ? Enum.GetNames(property.PropertyType)
                : null;

        var value = new CadValueDescriptor(
            property.Name,
            $"Cad.Property.{property.Name}",
            property.PropertyType,
            semantic,
            minimum,
            maximum,
            choices);

        return new CadPropertyDescriptor(
            property,
            value,
            attribute?.Order ??
            int.MaxValue);
    }
}
