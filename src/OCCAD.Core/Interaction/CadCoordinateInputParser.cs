using OcctNet;

namespace OCCAD;

public enum CadCoordinateInputMode
{
    AbsoluteCartesian,
    RelativeCartesian,
    RelativePolar,
    AbsolutePolar
}

public readonly record struct CadCoordinateInput(
    CadCoordinateInputMode Mode,
    OcctPoint3d Point,
    double? Distance = null,
    double? AngleDegrees = null);

/// <summary>
/// Parses AutoCAD-style point input in the active drawing-plane/UCS frame.
/// Absolute and relative input deliberately use the same effective frame so
/// typed coordinates remain correct after XY/XZ/YZ or custom plane changes.
/// Supported forms include:
/// 100,200
/// 100,200,50
/// #100,200
/// @500,0
/// @500,300,25
/// 1000&lt;30
/// #1000&lt;30
/// @1000&lt;30
///
/// A semicolon may be used as the Cartesian component separator. This keeps
/// precise input usable with cultures where comma is the decimal separator.
/// </summary>
public static class CadCoordinateInputParser
{
    public static bool TryParse(
        string? text,
        OcctPoint3d reference,
        CadWorkPlane workPlane,
        out CadCoordinateInput input)
    {
        input = default;
        if (string.IsNullOrWhiteSpace(text) || !reference.IsFinite)
            return false;
        ArgumentNullException.ThrowIfNull(workPlane);

        var value = text.Trim();
        var relative = value.StartsWith('@');
        if (relative || value.StartsWith('#'))
            value = value[1..].Trim();
        if (value.Length == 0)
            return false;

        if (value.Contains('<', StringComparison.Ordinal))
            return TryParsePolar(value, reference, workPlane, relative, out input);

        if (!TryParseCartesian(value, out var x, out var y, out var z, out var hasZ))
            return false;

        var frame = workPlane.EffectivePlane;
        if (!frame.XAxis.Cross(frame.YAxis).TryNormalize(out var normal))
            return false;

        OcctPoint3d point;
        if (relative)
        {
            point = reference +
                    frame.XAxis * x +
                    frame.YAxis * y +
                    normal * (hasZ ? z : 0.0);
            input = new CadCoordinateInput(
                CadCoordinateInputMode.RelativeCartesian,
                point);
        }
        else
        {
            point = frame.Origin +
                    frame.XAxis * x +
                    frame.YAxis * y +
                    normal * (hasZ ? z : 0.0);
            input = new CadCoordinateInput(
                CadCoordinateInputMode.AbsoluteCartesian,
                point);
        }

        return point.IsFinite;
    }

    private static bool TryParsePolar(
        string value,
        OcctPoint3d reference,
        CadWorkPlane workPlane,
        bool relative,
        out CadCoordinateInput input)
    {
        input = default;
        var separator = value.IndexOf('<');
        if (separator <= 0 || separator >= value.Length - 1 ||
            value.LastIndexOf('<') != separator)
            return false;

        if (!TryDouble(value[..separator], out var distance) || distance <= 0.0)
            return false;
        if (!TryDouble(value[(separator + 1)..], out var angleDegrees))
            return false;

        var frame = workPlane.EffectivePlane;
        if (!frame.XAxis.TryNormalize(out var xAxis) ||
            !frame.YAxis.TryNormalize(out var yAxis) ||
            !xAxis.Cross(yAxis).TryNormalize(out _))
            return false;

        var radians = angleDegrees * Math.PI / 180.0;
        var direction =
            xAxis * Math.Cos(radians) +
            yAxis * Math.Sin(radians);
        var point = (relative ? reference : frame.Origin) + direction * distance;
        if (!point.IsFinite)
            return false;

        input = new CadCoordinateInput(
            relative ? CadCoordinateInputMode.RelativePolar : CadCoordinateInputMode.AbsolutePolar,
            point,
            distance,
            angleDegrees);
        return true;
    }

    private static bool TryParseCartesian(
        string value,
        out double x,
        out double y,
        out double z,
        out bool hasZ)
    {
        x = y = z = 0.0;
        hasZ = false;

        var separator = value.Contains(';', StringComparison.Ordinal)
            ? ';'
            : ',';
        var parts = value.Split(separator, StringSplitOptions.TrimEntries);
        if (parts.Length is not (2 or 3) ||
            parts.Any(string.IsNullOrWhiteSpace))
            return false;

        if (!TryDouble(parts[0], out x) || !TryDouble(parts[1], out y))
            return false;

        if (parts.Length == 3)
        {
            if (!TryDouble(parts[2], out z))
                return false;
            hasZ = true;
        }

        return true;
    }

    private static bool TryDouble(string text, out double value) =>
        CadValueTextConverter.TryParseFiniteDouble(text, out value);
}
