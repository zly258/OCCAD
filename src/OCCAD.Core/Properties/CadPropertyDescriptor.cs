using System.ComponentModel;

namespace OCCAD;

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
    public string Category => Property.Category;
    public string Description => Property.Description;
    public Type PropertyType => Property.PropertyType;
    public bool IsReadOnly => Property.IsReadOnly;
    public bool IsBrowsable => Property.IsBrowsable;
    public TypeConverter Converter => Property.Converter;
    public int Order { get; }

    public object? GetValue(object target) =>
        Property.GetValue(target);

    public void SetValue(
        object target,
        object? value) =>
        Property.SetValue(target, value);
}

public static class CadPropertyCatalog
{
    public static IReadOnlyList<CadPropertyDescriptor>
        Describe(object target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return TypeDescriptor
            .GetProperties(target, true)
            .Cast<PropertyDescriptor>()
            .Where(static property =>
                property.IsBrowsable)
            .Select(Create)
            .OrderBy(static property =>
                property.Category,
                StringComparer.Ordinal)
            .ThenBy(static property =>
                property.Order)
            .ThenBy(static property =>
                property.DisplayName,
                StringComparer.Ordinal)
            .ToArray();
    }

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
