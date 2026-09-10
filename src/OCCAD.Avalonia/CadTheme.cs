using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;

namespace OCCAD.Avalonia;

internal static class CadTheme
{
    public const double FontSize = 11.0;
    public const double SmallFontSize = 10.0;
    public const double CaptionFontSize = 10.5;
    public const double ControlHeight = 22.0;
    public const double MenuHeight = 24.0;
    public const double PanelHeaderHeight = 23.0;
    public const double StatusHeight = 21.0;
    public const double SplitterThickness = 3.0;
    public const double ModelPanelWidth = 188.0;
    public const double PropertyPanelWidth = 296.0;
    public const double ToolPanelWidth = 276.0;
    public const double PropertyLabelWidth = 92.0;
    public const double ToolLabelWidth = 92.0;

    public static readonly IBrush WindowBrush = Brush("#DDE1E5");
    public static readonly IBrush Surface = Brush("#FAFAFA");
    public static readonly IBrush Panel = Brush("#F5F6F7");
    public static readonly IBrush PanelAlt = Brush("#ECEFF1");
    public static readonly IBrush Toolbar = Brush("#E9ECEF");
    public static readonly IBrush Header = Brush("#E4E7EA");
    public static readonly IBrush HeaderHover = Brush("#DCE2E7");
    public static readonly IBrush Accent = Brush("#2B78B5");
    public static readonly IBrush AccentSoft = Brush("#DCEAF5");
    public static readonly IBrush Border = Brush("#BFC5CA");
    public static readonly IBrush BorderStrong = Brush("#AEB5BB");
    public static readonly IBrush Splitter = Brush("#C8CDD2");
    public static readonly IBrush Text = Brush("#20262B");
    public static readonly IBrush Muted = Brush("#626C74");
    public static readonly IBrush Viewport = Brush("#1B1D1F");

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

        app.Styles.Add(new Style(x => x.OfType<Button>().Class("cad-compact"))
        {
            Setters =
            {
                new Setter(Button.MinHeightProperty, ControlHeight),
                new Setter(Button.PaddingProperty, new Thickness(6, 0)),
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
                new Setter(TextBox.PaddingProperty, new Thickness(5, 0)),
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
                new Setter(MenuItem.PaddingProperty, new Thickness(7, 2)),
                new Setter(MenuItem.ForegroundProperty, Text)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
        {
            Setters =
            {
                new Setter(TreeViewItem.MinHeightProperty, 20d),
                new Setter(TreeViewItem.PaddingProperty, new Thickness(3, 0))
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
                new Setter(TabItem.PaddingProperty, new Thickness(8, 2)),
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
            Padding = padding ?? new Thickness(4),
            Child = child
        };

    public static TextBlock SectionTitle(string text) =>
        new()
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Foreground = Text,
            Margin = new Thickness(0, 0, 0, 3)
        };

    private static SolidColorBrush Brush(string value) =>
        new(Color.Parse(value));
}
