using System.Globalization;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Culture-aware text conversion for CAD values shared by property surfaces,
/// tool panels and command input. CAD semantics stay in Core so UI layers do
/// not maintain competing parsing rules.
/// </summary>
public static class CadValueTextConverter
{
    public static string Format(
        CadValueDescriptor descriptor,
        Type valueType,
        System.ComponentModel.TypeConverter converter,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(converter);

        if (value is null)
            return string.Empty;

        if (value is OcctPoint3d point)
            return FormatTriple(point.X, point.Y, point.Z);
        if (value is OcctVector3d vector)
            return FormatTriple(vector.X, vector.Y, vector.Z);
        if (value is double number)
            return number.ToString("0.######", CultureInfo.CurrentCulture);
        if (value is float single)
            return single.ToString("0.######", CultureInfo.CurrentCulture);
        if (value is decimal decimalNumber)
            return decimalNumber.ToString("0.######", CultureInfo.CurrentCulture);

        try
        {
            return converter.ConvertToString(
                       null,
                       CultureInfo.CurrentCulture,
                       value) ??
                   value.ToString() ??
                   string.Empty;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return value.ToString() ?? string.Empty;
        }
    }

    public static bool TryParse(
        CadValueDescriptor descriptor,
        Type valueType,
        System.ComponentModel.TypeConverter converter,
        string text,
        out object? value)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            if (descriptor.Semantic == CadValueSemantic.Point ||
                valueType == typeof(OcctPoint3d))
            {
                if (TryParseTriple(text, out var x, out var y, out var z))
                {
                    value = new OcctPoint3d(x, y, z);
                    return true;
                }

                value = null;
                return false;
            }

            if (descriptor.Semantic == CadValueSemantic.Vector ||
                valueType == typeof(OcctVector3d))
            {
                if (TryParseTriple(text, out var x, out var y, out var z))
                {
                    value = new OcctVector3d(x, y, z);
                    return true;
                }

                value = null;
                return false;
            }

            if (IsNumericType(valueType))
            {
                if (!TryParseNumeric(valueType, text, out value) ||
                    !ValidateNumericRange(descriptor, value))
                {
                    value = null;
                    return false;
                }

                return true;
            }

            value = converter.ConvertFromString(
                null,
                CultureInfo.CurrentCulture,
                text);
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            value = null;
            return false;
        }
    }

    public static bool TryParseFiniteDouble(
        string text,
        out double value)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParseFinite(text.Trim(), out value);
    }

    public static bool IsNumericType(Type type) =>
        type == typeof(double) ||
        type == typeof(float) ||
        type == typeof(decimal) ||
        type == typeof(int) ||
        type == typeof(long);

    private static string FormatTriple(
        double x,
        double y,
        double z) =>
        string.Format(
            CultureInfo.CurrentCulture,
            "{0:0.######}, {1:0.######}, {2:0.######}",
            x,
            y,
            z);

    private static bool TryParseTriple(
        string text,
        out double x,
        out double y,
        out double z)
    {
        x = y = z = 0.0;
        var normalized = text.Trim();

        if (normalized.Length >= 2 &&
            ((normalized[0] == '(' && normalized[^1] == ')') ||
             (normalized[0] == '[' && normalized[^1] == ']')))
        {
            normalized = normalized[1..^1].Trim();
        }

        var separators = normalized.Contains(',') || normalized.Contains(';')
            ? new[] { ',', ';' }
            : new[] { ' ', '\t' };
        var parts = normalized.Split(
            separators,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        return parts.Length == 3 &&
               TryParseFinite(parts[0], out x) &&
               TryParseFinite(parts[1], out y) &&
               TryParseFinite(parts[2], out z);
    }

    private static bool TryParseFinite(
        string text,
        out double value)
    {
        if ((double.TryParse(
                 text,
                 NumberStyles.Float,
                 CultureInfo.CurrentCulture,
                 out value) ||
             double.TryParse(
                 text,
                 NumberStyles.Float,
                 CultureInfo.InvariantCulture,
                 out value)) &&
            double.IsFinite(value))
        {
            return true;
        }

        value = 0.0;
        return false;
    }

    private static bool TryParseNumeric(
        Type type,
        string text,
        out object? value)
    {
        var culture = CultureInfo.CurrentCulture;
        var invariant = CultureInfo.InvariantCulture;

        if (type == typeof(double))
        {
            if (TryParseFinite(text, out var number))
            {
                value = number;
                return true;
            }
        }
        else if (type == typeof(float))
        {
            if ((float.TryParse(text, NumberStyles.Float, culture, out var number) ||
                 float.TryParse(text, NumberStyles.Float, invariant, out number)) &&
                float.IsFinite(number))
            {
                value = number;
                return true;
            }
        }
        else if (type == typeof(decimal))
        {
            if (decimal.TryParse(text, NumberStyles.Float, culture, out var number) ||
                decimal.TryParse(text, NumberStyles.Float, invariant, out number))
            {
                value = number;
                return true;
            }
        }
        else if (type == typeof(int))
        {
            if (int.TryParse(text, NumberStyles.Integer, culture, out var number) ||
                int.TryParse(text, NumberStyles.Integer, invariant, out number))
            {
                value = number;
                return true;
            }
        }
        else if (type == typeof(long))
        {
            if (long.TryParse(text, NumberStyles.Integer, culture, out var number) ||
                long.TryParse(text, NumberStyles.Integer, invariant, out number))
            {
                value = number;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool ValidateNumericRange(
        CadValueDescriptor descriptor,
        object? value)
    {
        if (value is null)
            return false;

        double number;
        try
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return false;
        }

        return double.IsFinite(number) &&
               (descriptor.Minimum is not { } minimum || number >= minimum) &&
               (descriptor.Maximum is not { } maximum || number <= maximum);
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
