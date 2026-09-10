using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OCCAD.Avalonia;

/// <summary>
/// Small visual token set for the desktop shell. The CAD domain owns behavior;
/// this class only keeps Avalonia spacing, typography and surfaces consistent.
/// </summary>
internal static class CadUi
{
    public static readonly IBrush Window = Brush("#E9ECEF");
    public static readonly IBrush Surface = Brush("#F8F9FA");
    public static readonly IBrush Panel = Brush("#FCFCFD");
    public static readonly IBrush Header = Brush("#F1F3F5");
    public static readonly IBrush Border = Brush("#CED4DA");
    public static readonly IBrush Text = Brush("#252A2E");
    public static readonly IBrush Muted = Brush("#687078");
    public static readonly IBrush Accent = Brush("#1976D2");

    public const double UiFontSize = 11;
    public const double HeaderFontSize = 11;
    public const double CompactControlHeight = 24;
    public const double RibbonHeight = 68;
    public const double ModelPanelWidth = 220;
    public const double InspectorPanelWidth = 280;
    public const double SplitterWidth = 3;

    public static void ConfigureCompactButton(Button button)
    {
        button.MinHeight = CompactControlHeight;
        button.Padding = new Thickness(7, 2);
        button.Margin = new Thickness(1, 0);
        button.FontSize = UiFontSize;
    }

    public static void ConfigureStatusButton(Button button)
    {
        button.MinHeight = 20;
        button.Padding = new Thickness(5, 0);
        button.Margin = new Thickness(1, 0);
        button.FontSize = UiFontSize;
    }

    public static Border CreatePanelHeader(string text) =>
        new()
        {
            Background = Header,
            BorderBrush = Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(7, 4),
            Child = new TextBlock
            {
                Text = text,
                FontSize = HeaderFontSize,
                FontWeight = FontWeight.SemiBold,
                Foreground = Text
            }
        };

    private static SolidColorBrush Brush(string value) =>
        new(Color.Parse(value));
}
