using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace OCCAD.Avalonia;

internal static class CadTheme
{
    public const double FontSize = 12.0;
    public const double SmallFontSize = 11.0;
    public const double CaptionFontSize = 11.0;
    public const double ControlHeight = 26.0;
    public const double MenuHeight = 28.0;
    public const double PanelHeaderHeight = 28.0;
    public const double StatusHeight = 24.0;
    public const double SplitterThickness = 4.0;
    public const double ModelPanelWidth = 210.0;
    public const double PropertyPanelWidth = 360.0;
    public const double ToolPanelWidth = 304.0;
    public const double PropertyLabelWidth = 112.0;
    public const double PropertyTitleHeight = 30.0;
    public const double PropertyCategoryHeaderHeight = 27.0;
    public const double PropertyRowHeight = 28.0;
    public const double PropertyRowIndent = 14.0;
    public const double PropertyChevronWidth = 16.0;
    public const double LayerHeaderHeight = 27.0;
    public const double LayerRowHeight = 30.0;
    public const double LayerActionButtonMinWidth = 52.0;
    public const double ToolLabelWidth = 100.0;
    public const double DialogButtonWidth = 80.0;
    public const double DialogPadding = 12.0;
    public const double DynamicHudMaxWidth = 300.0;
    public const double DynamicHudEstimatedHeight = 34.0;
    public const double DynamicHudOffset = 16.0;
    public const double OverlayMargin = 8.0;

    public static readonly IBrush WindowBrush = Brush("#D6DADF");
    public static readonly IBrush Surface = Brush("#F7F8F9");
    public static readonly IBrush Panel = Brush("#F0F2F4");
    public static readonly IBrush PanelAlt = Brush("#E8EBEE");
    public static readonly IBrush Toolbar = Brush("#E3E6E9");
    public static readonly IBrush Header = Brush("#DCE1E5");
    public static readonly IBrush HeaderHover = Brush("#D3DAE0");
    public static readonly IBrush Accent = Brush("#2F6FA5");
    public static readonly IBrush AccentSoft = Brush("#D8E7F2");
    public static readonly IBrush Border = Brush("#B5BDC4");
    public static readonly IBrush BorderStrong = Brush("#9FA9B1");
    public static readonly IBrush Splitter = Brush("#BEC5CB");
    public static readonly IBrush Text = Brush("#1F252A");
    public static readonly IBrush Muted = Brush("#5B6670");
    public static readonly IBrush Viewport = Brush("#202326");

    public static void Apply(Application app)
    {
        app.Resources["Cad.Window"] = WindowBrush;
        app.Resources["Cad.Surface"] = Surface;
        app.Resources["Cad.Panel"] = Panel;
        app.Resources["Cad.PanelAlt"] = PanelAlt;
        app.Resources["Cad.Toolbar"] = Toolbar;
        app.Resources["Cad.Header"] = Header;
        app.Resources["Cad.Accent"] = Accent;
        app.Resources["Cad.AccentSoft"] = AccentSoft;
        app.Resources["Cad.Border"] = Border;
        app.Resources["Cad.BorderStrong"] = BorderStrong;
        app.Resources["Cad.Text"] = Text;
        app.Resources["Cad.Muted"] = Muted;

        app.Styles.Add(new Style(x => x.OfType<Window>())
        {
            Setters =
            {
                new Setter(Window.BackgroundProperty, WindowBrush),
                new Setter(Window.FontSizeProperty, FontSize),
                new Setter(
                    Window.FontFamilyProperty,
                    FontFamily.Parse(
                        "Segoe UI, Microsoft YaHei UI, Noto Sans CJK SC, sans-serif"))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TextBlock>())
        {
            Setters =
            {
                new Setter(TextBlock.FontSizeProperty, FontSize),
                new Setter(TextBlock.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(
                    Button.VerticalContentAlignmentProperty,
                    VerticalAlignment.Center),
                new Setter(
                    Button.HorizontalContentAlignmentProperty,
                    HorizontalAlignment.Center)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<ToggleButton>())
        {
            Setters =
            {
                new Setter(
                    ToggleButton.VerticalContentAlignmentProperty,
                    VerticalAlignment.Center),
                new Setter(
                    ToggleButton.HorizontalContentAlignmentProperty,
                    HorizontalAlignment.Center)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<Button>().Class("cad-compact"))
        {
            Setters =
            {
                new Setter(Button.MinHeightProperty, ControlHeight),
                new Setter(Button.FontSizeProperty, FontSize),
                new Setter(Button.PaddingProperty, new Thickness(8, 1)),
                new Setter(Button.MarginProperty, new Thickness(1)),
                new Setter(Button.BackgroundProperty, Brushes.Transparent),
                new Setter(Button.BorderBrushProperty, Brushes.Transparent),
                new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                new Setter(Button.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<Button>().Class("cad-primary"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, Accent),
                new Setter(Button.BorderBrushProperty, Accent),
                new Setter(Button.ForegroundProperty, Brushes.White),
                new Setter(Button.FontWeightProperty, FontWeight.SemiBold)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<ToggleButton>().Class("cad-toggle"))
        {
            Setters =
            {
                new Setter(ToggleButton.MinHeightProperty, ControlHeight),
                new Setter(ToggleButton.FontSizeProperty, FontSize),
                new Setter(ToggleButton.PaddingProperty, new Thickness(6, 0)),
                new Setter(ToggleButton.MarginProperty, new Thickness(1)),
                new Setter(ToggleButton.BackgroundProperty, Brushes.Transparent),
                new Setter(ToggleButton.BorderBrushProperty, Border),
                new Setter(ToggleButton.BorderThicknessProperty, new Thickness(1)),
                new Setter(ToggleButton.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TextBox>().Class("cad-input"))
        {
            Setters =
            {
                new Setter(TextBox.MinHeightProperty, ControlHeight),
                new Setter(TextBox.FontSizeProperty, FontSize),
                new Setter(TextBox.PaddingProperty, new Thickness(7, 1)),
                new Setter(TextBox.BackgroundProperty, Surface),
                new Setter(TextBox.BorderBrushProperty, Border),
                new Setter(TextBox.BorderThicknessProperty, new Thickness(1)),
                new Setter(TextBox.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<ComboBox>().Class("cad-input"))
        {
            Setters =
            {
                new Setter(ComboBox.MinHeightProperty, ControlHeight),
                new Setter(ComboBox.FontSizeProperty, FontSize),
                new Setter(ComboBox.BackgroundProperty, Surface),
                new Setter(ComboBox.BorderBrushProperty, Border),
                new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)),
                new Setter(ComboBox.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<MenuItem>())
        {
            Setters =
            {
                new Setter(MenuItem.MinHeightProperty, MenuHeight),
                new Setter(MenuItem.FontSizeProperty, FontSize),
                new Setter(MenuItem.PaddingProperty, new Thickness(9, 3)),
                new Setter(MenuItem.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<CheckBox>())
        {
            Setters =
            {
                new Setter(CheckBox.FontSizeProperty, FontSize),
                new Setter(CheckBox.MinHeightProperty, ControlHeight),
                new Setter(CheckBox.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
        {
            Setters =
            {
                new Setter(TreeViewItem.MinHeightProperty, 24d),
                new Setter(TreeViewItem.PaddingProperty, new Thickness(5, 1))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TreeView>().Class("cad-tree"))
        {
            Setters =
            {
                new Setter(TreeView.BackgroundProperty, Surface),
                new Setter(TreeView.BorderThicknessProperty, new Thickness(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TabControl>().Class("cad-tabs"))
        {
            Setters =
            {
                new Setter(TabControl.BackgroundProperty, Surface)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class("cad-tab"))
        {
            Setters =
            {
                new Setter(TabItem.MinHeightProperty, MenuHeight),
                new Setter(TabItem.PaddingProperty, new Thickness(10, 3)),
                new Setter(TabItem.FontWeightProperty, FontWeight.SemiBold)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<Menu>().Class("cad-menu"))
        {
            Setters =
            {
                new Setter(Menu.MinHeightProperty, MenuHeight),
                new Setter(Menu.BackgroundProperty, Toolbar),
                new Setter(Menu.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<GridSplitter>().Class("cad-splitter"))
        {
            Setters =
            {
                new Setter(GridSplitter.BackgroundProperty, Splitter)
            }
        });
    }

    public static Border Card(Control child, Thickness? padding = null) =>
        new()
        {
            Background = Surface,
            BorderBrush = Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0),
            Padding = padding ?? new Thickness(6),
            Child = child
        };

    public static TextBlock SectionTitle(string text) =>
        new()
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Foreground = Text,
            Margin = new Thickness(0, 0, 0, 5)
        };

    private static SolidColorBrush Brush(string value) =>
        new(Color.Parse(value));
}
