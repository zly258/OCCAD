using Avalonia.Media;

namespace OCCAD.Avalonia;

/// <summary>
/// Presentation-neutral shell metrics used for layout and CAD scene overlays.
/// Avalonia controls intentionally use the native Fluent theme; this type does
/// not register or override any control styles.
/// </summary>
internal static class CadTheme
{
    public const double FontSize = 11.0;
    public const double SmallFontSize = 10.0;
    public const double CaptionFontSize = 10.0;
    public const double ControlHeight = 28.0;
    public const double PanelHeaderHeight = 28.0;
    public const double StatusHeight = 26.0;
    public const double SplitterThickness = 4.0;
    public const double ModelPanelWidth = 220.0;
    public const double PropertyPanelWidth = 380.0;
    public const double PropertyLabelWidth = 120.0;
    public const double PropertyTitleHeight = 28.0;
    public const double PropertyRowHeight = 26.0;
    public const double LayerHeaderHeight = 28.0;
    public const double LayerRowHeight = 26.0;
    public const double LayerActionButtonMinWidth = 44.0;
    public const double DialogButtonWidth = 80.0;
    public const double DialogPadding = 12.0;
    public const double OverlayMargin = 6.0;

    // These brushes are limited to structural CAD surfaces and overlays. They
    // are not control themes. Standard Avalonia controls use Fluent defaults.
    public static readonly IBrush WindowBrush = Brushes.White;
    public static readonly IBrush Surface = Brushes.White;
    public static readonly IBrush Panel = Brushes.White;
    public static readonly IBrush PanelAlt = Brushes.WhiteSmoke;
    public static readonly IBrush Toolbar = Brushes.White;
    public static readonly IBrush Header = Brushes.WhiteSmoke;
    public static readonly IBrush HeaderHover = Brushes.Gainsboro;
    public static readonly IBrush Accent = Brushes.DodgerBlue;
    public static readonly IBrush AccentSoft = Brushes.AliceBlue;
    public static readonly IBrush Border = Brushes.LightGray;
    public static readonly IBrush BorderStrong = Brushes.Silver;
    public static readonly IBrush Splitter = Brushes.LightGray;
    public static readonly IBrush Text = Brushes.Black;
    public static readonly IBrush Muted = Brushes.DimGray;
    public static readonly IBrush Viewport = new SolidColorBrush(Color.Parse("#202225"));
    public static readonly IBrush OverlayBackground = new SolidColorBrush(Color.Parse("#E6202225"));
    public static readonly IBrush OverlayBorder = Brushes.Gray;
    public static readonly IBrush OverlayText = Brushes.White;
    public static readonly IBrush OverlayMuted = Brushes.LightGray;
    public static readonly IBrush OverlayAccent = Brushes.LightSkyBlue;
}
