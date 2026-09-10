using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OCCAD.Avalonia;

/// <summary>
/// Shared visual tokens for the desktop CAD shell. The palette and compact
/// spacing follow the ModelScript Avalonia industrial UI, while CAD behavior
/// remains entirely in Core.
/// </summary>
internal static class CadUi
{
    public static readonly IBrush Window = Brush("#F2F4F7");
    public static readonly IBrush Surface = Brush("#FFFFFF");
    public static readonly IBrush Panel = Brush("#FFFFFF");
    public static readonly IBrush Subtle = Brush("#F8FAFC");
    public static readonly IBrush Header = Brush("#F8FAFC");
    public static readonly IBrush Border = Brush("#CBD5DE");
    public static readonly IBrush Text = Brush("#22313F");
    public static readonly IBrush Muted = Brush("#5B6875");
    public static readonly IBrush Accent = Brush("#2F6F9F");
    public static readonly IBrush Hover = Brush("#EEF3F7");
    public static readonly IBrush Selected = Brush("#E9EEF2");
    public static readonly IBrush Pressed = Brush("#E1E7EC");
    public static readonly IBrush Disabled = Brush("#E9EDF2");

    public static readonly FontFamily UiFontFamily =
        new("Noto Sans CJK SC, Microsoft YaHei UI, PingFang SC, Segoe UI, sans-serif");

    public const double UiFontSize = 12;
    public const double HeaderFontSize = 12;
    public const double CompactControlHeight = 26;
    public const double RibbonRowHeight = 28;
    public const double RibbonHeight = 122;
    public const double ModelPanelWidth = 220;
    public const double InspectorPanelWidth = 286;
    public const double SplitterWidth = 4;

    public static void ConfigureCompactButton(Button button)
    {
        button.MinHeight = CompactControlHeight;
        button.Padding = new Thickness(6, 2);
        button.Margin = new Thickness(1, 0);
        button.FontFamily = UiFontFamily;
        button.FontSize = UiFontSize;
        button.CornerRadius = new CornerRadius(2);
    }

    public static void ConfigureStatusButton(Button button)
    {
        button.MinHeight = 20;
        button.Padding = new Thickness(5, 0);
        button.Margin = new Thickness(1, 0);
        button.FontFamily = UiFontFamily;
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
                FontFamily = UiFontFamily,
                FontSize = HeaderFontSize,
                FontWeight = FontWeight.SemiBold,
                Foreground = Text
            }
        };

    private static SolidColorBrush Brush(string value) =>
        new(Color.Parse(value));
}
