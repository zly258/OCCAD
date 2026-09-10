using System.Drawing;
using System.Globalization;
using System.Text.Json;
using OCCAD;

namespace OCCAD.Avalonia;

internal sealed class CadApplicationSettings
{
    private const int CurrentVersion = 3;
    private const CadSnapType PersistedSnapModes =
        CadSnapType.Endpoint |
        CadSnapType.Midpoint |
        CadSnapType.Center |
        CadSnapType.Vertex |
        CadSnapType.Quadrant |
        CadSnapType.Nearest |
        CadSnapType.Intersection |
        CadSnapType.Perpendicular |
        CadSnapType.Tangent |
        CadSnapType.ApparentIntersection |
        CadSnapType.Extension |
        CadSnapType.Insertion |
        CadSnapType.Node;

    public int Version { get; set; } = CurrentVersion;
    public string Language { get; set; } = "en-US";
    public string SceneBackground { get; set; } = "#000000";
    public int GripSize { get; set; } = 15;
    public double GripTolerance { get; set; } = 10.0;
    public int SnapMarkerSize { get; set; } = 15;
    public string SnapMarkerColor { get; set; } = "#F5DC2D";
    public double SnapTolerance { get; set; } = 10.0;
    public double ZoomSensitivity { get; set; } = 1.0;
    public int SelectionTolerance { get; set; } = 5;
    public double DisplayDeviationCoefficient { get; set; } = 0.001;
    public double DisplayDeviationAngleDegrees { get; set; } = 20.0;

    public bool SnapEnabled { get; set; } = true;
    public int SnapModes { get; set; } = (int)CadSnapType.Default;
    public bool OrthogonalTrackingEnabled { get; set; }
    public bool PolarTrackingEnabled { get; set; }
    public double PolarIncrementDegrees { get; set; } = 45.0;
    public CadWorkPlanePreset WorkPlanePreset { get; set; } = CadWorkPlanePreset.XY;

    public Color SceneBackgroundColor => ParseColor(SceneBackground);
    public Color SnapMarkerColorValue => ParseColor(SnapMarkerColor);

    public static CadApplicationSettings CreateDefault() => new();

    public CadApplicationSettings Clone() =>
        new()
        {
            Version = CurrentVersion,
            Language = Language,
            SceneBackground = SceneBackground,
            GripSize = GripSize,
            GripTolerance = GripTolerance,
            SnapMarkerSize = SnapMarkerSize,
            SnapMarkerColor = SnapMarkerColor,
            SnapTolerance = SnapTolerance,
            ZoomSensitivity = ZoomSensitivity,
            SelectionTolerance = SelectionTolerance,
            DisplayDeviationCoefficient = DisplayDeviationCoefficient,
            DisplayDeviationAngleDegrees = DisplayDeviationAngleDegrees,
            SnapEnabled = SnapEnabled,
            SnapModes = SnapModes,
            OrthogonalTrackingEnabled = OrthogonalTrackingEnabled,
            PolarTrackingEnabled = PolarTrackingEnabled,
            PolarIncrementDegrees = PolarIncrementDegrees,
            WorkPlanePreset = WorkPlanePreset
        };

    public void Validate()
    {
        _ = SceneBackgroundColor;
        _ = SnapMarkerColorValue;

        Language = string.Equals(Language, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";

        if (GripSize is < 7 or > 31)
            throw new ArgumentOutOfRangeException(nameof(GripSize), "Grip size must be between 7 and 31 pixels.");
        if (!double.IsFinite(GripTolerance) || GripTolerance < 2.0 || GripTolerance > 50.0)
            throw new ArgumentOutOfRangeException(nameof(GripTolerance), "Grip tolerance must be between 2 and 50 pixels.");

        if (SnapMarkerSize is < 9 or > 31)
            throw new ArgumentOutOfRangeException(nameof(SnapMarkerSize), "Snap marker size must be between 9 and 31 pixels.");
        if (!double.IsFinite(SnapTolerance) || SnapTolerance < 2.0 || SnapTolerance > 50.0)
            throw new ArgumentOutOfRangeException(nameof(SnapTolerance), "Snap tolerance must be between 2 and 50 pixels.");

        var snapModes = (CadSnapType)SnapModes & PersistedSnapModes;
        if (snapModes == CadSnapType.None)
            snapModes = CadSnapType.Default;
        SnapModes = (int)snapModes;

        if (!double.IsFinite(ZoomSensitivity) || ZoomSensitivity < 0.1 || ZoomSensitivity > 5.0)
            throw new ArgumentOutOfRangeException(nameof(ZoomSensitivity), "Zoom sensitivity must be between 0.1 and 5.0.");

        if (SelectionTolerance is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(SelectionTolerance), "Selection tolerance must be between 0 and 100 pixels.");

        if (!double.IsFinite(DisplayDeviationCoefficient) || DisplayDeviationCoefficient < 0.0001 || DisplayDeviationCoefficient > 0.1)
            throw new ArgumentOutOfRangeException(nameof(DisplayDeviationCoefficient), "Display precision coefficient must be between 0.0001 and 0.1.");

        if (!double.IsFinite(DisplayDeviationAngleDegrees) || DisplayDeviationAngleDegrees < 1.0 || DisplayDeviationAngleDegrees > 45.0)
            throw new ArgumentOutOfRangeException(nameof(DisplayDeviationAngleDegrees), "Display precision angle must be between 1 and 45 degrees.");

        if (!double.IsFinite(PolarIncrementDegrees) || PolarIncrementDegrees <= 0.0 || PolarIncrementDegrees > 180.0)
            throw new ArgumentOutOfRangeException(nameof(PolarIncrementDegrees), "Polar increment must be between 0 and 180 degrees.");

        if (OrthogonalTrackingEnabled && PolarTrackingEnabled)
            PolarTrackingEnabled = false;

        if (WorkPlanePreset == CadWorkPlanePreset.Custom || !Enum.IsDefined(WorkPlanePreset))
            WorkPlanePreset = CadWorkPlanePreset.XY;
    }

    public void Save()
    {
        Validate();
        var path = SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static CadApplicationSettings Load()
    {
        var path = SettingsPath();
        if (!File.Exists(path))
            return CreateDefault();

        try
        {
            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<CadApplicationSettings>(json) ?? CreateDefault();
            value.Version = CurrentVersion;
            value.Validate();
            return value;
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            CadDiagnostics.Report(exception, "Application settings");
            return CreateDefault();
        }
    }

    public static string ColorToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color ParseColor(string value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 7 && text[0] == '#' &&
            byte.TryParse(text.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) &&
            byte.TryParse(text.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) &&
            byte.TryParse(text.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            return Color.FromArgb(255, red, green, blue);
        }

        throw new FormatException("Scene background must use #RRGGBB format.");
    }

    private static string SettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OCCAD",
            "settings.json");
}
