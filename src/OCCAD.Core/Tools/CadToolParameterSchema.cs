namespace OCCAD;

/// <summary>
/// UI-neutral parameter metadata exposed by CAD tools. The schema belongs to
/// Core; a desktop, script, MCP, or other frontend decides how to present it.
/// </summary>
public abstract record CadToolParameterDescriptor
{
    protected CadToolParameterDescriptor(
        string id,
        string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Id = id.Trim();
        Label = label.Trim();
        ValueDescriptor = CadValueDescriptor.Create(
            Id,
            typeof(object),
            semantic: CadValueSemantic.General);
    }

    public string Id { get; }
    public string Label { get; }
    public CadValueDescriptor ValueDescriptor { get; protected set; }
}

public sealed record CadStringToolParameterDescriptor : CadToolParameterDescriptor
{
    public CadStringToolParameterDescriptor(
        string id,
        string label,
        string value,
        bool allowEmpty = false)
        : base(id, label)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!allowEmpty && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty.", nameof(value));

        Value = value;
        AllowEmpty = allowEmpty;
        ValueDescriptor = CadValueDescriptor.Create(
            Id,
            typeof(string),
            semantic: CadValueSemantic.Text);
    }

    public string Value { get; }
    public bool AllowEmpty { get; }
}

public sealed record CadIntegerToolParameterDescriptor : CadToolParameterDescriptor
{
    public CadIntegerToolParameterDescriptor(
        string id,
        string label,
        int value,
        int minimum,
        int maximum)
        : base(id, label)
    {
        if (minimum > maximum)
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                "Minimum cannot exceed maximum.");
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Value must be inside the declared range.");

        Value = value;
        Minimum = minimum;
        Maximum = maximum;
        ValueDescriptor = CadValueDescriptor.Create(
            Id,
            typeof(int),
            minimum,
            maximum,
            semantic: CadValueSemantic.Integer);
    }

    public int Value { get; }
    public int Minimum { get; }
    public int Maximum { get; }
}

public sealed record CadDoubleToolParameterDescriptor : CadToolParameterDescriptor
{
    public CadDoubleToolParameterDescriptor(
        string id,
        string label,
        double value,
        double minimum,
        double maximum)
        : base(id, label)
    {
        ValidateBounds(minimum, maximum);
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Value must be finite and inside the declared range.");

        Value = value;
        Minimum = minimum;
        Maximum = maximum;
        ValueDescriptor = CadValueDescriptor.Create(
            Id,
            typeof(double),
            minimum,
            maximum);
    }

    public double Value { get; }
    public double Minimum { get; }
    public double Maximum { get; }

    private static void ValidateBounds(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) ||
            !double.IsFinite(maximum) ||
            minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                "Numeric parameter bounds must be finite and ordered.");
        }
    }
}

public sealed record CadOptionalDoubleToolParameterDescriptor : CadToolParameterDescriptor
{
    public CadOptionalDoubleToolParameterDescriptor(
        string id,
        string label,
        double? value,
        double minimum,
        double maximum)
        : base(id, label)
    {
        if (!double.IsFinite(minimum) ||
            !double.IsFinite(maximum) ||
            minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum),
                "Numeric parameter bounds must be finite and ordered.");
        }

        if (value is { } actual &&
            (!double.IsFinite(actual) || actual < minimum || actual > maximum))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Value must be finite and inside the declared range.");
        }

        Value = value;
        Minimum = minimum;
        Maximum = maximum;
        ValueDescriptor = CadValueDescriptor.Create(
            Id,
            typeof(double),
            minimum,
            maximum);
    }

    public double? Value { get; }
    public double Minimum { get; }
    public double Maximum { get; }
}

public sealed record CadBooleanToolParameterDescriptor : CadToolParameterDescriptor
{
    public CadBooleanToolParameterDescriptor(
        string id,
        string label,
        bool value)
        : base(id, label)
    {
        Value = value;
        ValueDescriptor = CadValueDescriptor.Create(
            Id,
            typeof(bool),
            semantic: CadValueSemantic.Boolean);
    }

    public bool Value { get; }
}

public sealed record CadChoiceToolParameterDescriptor : CadToolParameterDescriptor
{
    public CadChoiceToolParameterDescriptor(
        string id,
        string label,
        string value,
        IReadOnlyList<string> choices)
        : base(id, label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentNullException.ThrowIfNull(choices);

        var normalizedChoices = choices
            .Select(static choice =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(choice);
                return choice.Trim();
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedChoices.Length == 0)
            throw new ArgumentException(
                "Choice parameter must define at least one value.",
                nameof(choices));

        var selected = normalizedChoices.FirstOrDefault(choice =>
            string.Equals(
                choice,
                value.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (selected is null)
            throw new ArgumentException(
                $"Value '{value}' is not a valid choice.",
                nameof(value));

        Value = selected;
        Choices = normalizedChoices;
        ValueDescriptor = CadValueDescriptor.Create(
            Id,
            typeof(string),
            choices: normalizedChoices,
            semantic: CadValueSemantic.Choice);
    }

    public string Value { get; }
    public IReadOnlyList<string> Choices { get; }
}

public class CadToolParameterSchema
{
    public CadToolParameterSchema(
        string title,
        IReadOnlyList<CadToolParameterDescriptor> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(parameters);

        var values = parameters.ToArray();
        var duplicateId = values
            .GroupBy(
                static parameter => parameter.Id,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1)
            ?.Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException(
                $"Tool parameter id '{duplicateId}' is duplicated.",
                nameof(parameters));
        }

        Title = title.Trim();
        Parameters = values;
    }

    public string Title { get; }
    public IReadOnlyList<CadToolParameterDescriptor> Parameters { get; }
}

/// <summary>
/// Transitional source-compatibility name. Existing tools may keep returning
/// this type while consumers depend only on CadToolParameterSchema.
/// </summary>
public sealed class CadToolPanelDescriptor : CadToolParameterSchema
{
    public CadToolPanelDescriptor(
        string title,
        IReadOnlyList<CadToolParameterDescriptor> parameters)
        : base(title, parameters)
    {
    }
}
