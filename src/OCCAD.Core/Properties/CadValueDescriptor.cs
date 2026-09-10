using System.Drawing;
using OcctNet;

namespace OCCAD;

public enum CadValueSemantic
{
    General,
    Text,
    Boolean,
    Integer,
    Number,
    Length,
    Angle,
    Choice,
    Layer,
    Scale,
    Tolerance,
    Transparency,
    Point,
    Vector,
    Color,
    Enum
}

/// <summary>
/// CAD-level value semantics shared by tools and property surfaces. This avoids
/// deriving editor behavior solely from CLR types.
/// </summary>
public sealed record CadValueDescriptor
{
    public CadValueDescriptor(
        string id,
        string displayKey,
        Type valueType,
        CadValueSemantic semantic,
        double? minimum = null,
        double? maximum = null,
        IReadOnlyList<string>? choices = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayKey);
        ArgumentNullException.ThrowIfNull(valueType);

        if (minimum is { } min && !double.IsFinite(min))
            throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum is { } max && !double.IsFinite(max))
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (minimum is { } lower &&
            maximum is { } upper &&
            lower > upper)
            throw new ArgumentException(
                "Minimum cannot exceed maximum.");

        Id = id.Trim();
        DisplayKey = displayKey.Trim();
        ValueType = valueType;
        Semantic = semantic;
        Minimum = minimum;
        Maximum = maximum;
        Choices = choices?.ToArray() ??
                  Array.Empty<string>();
    }

    public string Id { get; }
    public string DisplayKey { get; }
    public Type ValueType { get; }
    public CadValueSemantic Semantic { get; }
    public double? Minimum { get; }
    public double? Maximum { get; }
    public IReadOnlyList<string> Choices { get; }

    public static CadValueDescriptor Create(
        string id,
        Type valueType,
        double? minimum = null,
        double? maximum = null,
        IReadOnlyList<string>? choices = null,
        CadValueSemantic? semantic = null) =>
        new(
            id,
            $"Cad.Value.{id}",
            valueType,
            semantic ??
            InferSemantic(
                id,
                valueType,
                choices),
            minimum,
            maximum,
            choices);

    public static CadValueSemantic InferSemantic(
        string id,
        Type valueType,
        IReadOnlyList<string>? choices = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(valueType);

        if (choices is { Count: > 0 })
            return CadValueSemantic.Choice;
        if (valueType == typeof(bool))
            return CadValueSemantic.Boolean;
        if (valueType == typeof(Color))
            return CadValueSemantic.Color;
        if (valueType == typeof(OcctPoint3d))
            return CadValueSemantic.Point;
        if (valueType == typeof(OcctVector3d))
            return CadValueSemantic.Vector;
        if (valueType == typeof(string))
            return id.Equals(
                    "Layer",
                    StringComparison.OrdinalIgnoreCase)
                ? CadValueSemantic.Layer
                : CadValueSemantic.Text;
        if (valueType.IsEnum)
            return CadValueSemantic.Enum;
        if (valueType == typeof(int) ||
            valueType == typeof(long))
            return CadValueSemantic.Integer;

        if (valueType != typeof(double) &&
            valueType != typeof(float) &&
            valueType != typeof(decimal))
            return CadValueSemantic.General;

        if (Contains(id, "Angle") ||
            Contains(id, "Sweep"))
            return CadValueSemantic.Angle;
        if (Contains(id, "Scale") ||
            Contains(id, "Factor"))
            return CadValueSemantic.Scale;
        if (Contains(id, "Tolerance"))
            return CadValueSemantic.Tolerance;
        if (Contains(id, "Transparency"))
            return CadValueSemantic.Transparency;
        if (Contains(id, "Length") ||
            Contains(id, "Radius") ||
            Contains(id, "Diameter") ||
            Contains(id, "Width") ||
            Contains(id, "Height") ||
            Contains(id, "Distance") ||
            Contains(id, "Offset") ||
            Contains(id, "Thickness") ||
            Contains(id, "Pitch") ||
            Contains(id, "Spacing"))
            return CadValueSemantic.Length;

        return CadValueSemantic.Number;
    }

    private static bool Contains(
        string value,
        string token) =>
        value.Contains(
            token,
            StringComparison.OrdinalIgnoreCase);
}
