using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace OCCAD.Avalonia;

internal static class CadTheme
{
    public const double FontSize = 11.5;
    public const double SmallFontSize = 10.5;
    public const double CaptionFontSize = 10.5;
    public const double ControlHeight = 22.0;
    public const double MenuHeight = 25.0;
    public const double PanelHeaderHeight = 24.0;
    public const double StatusHeight = 20.0;
    public const double SplitterThickness = 3.0;
    public const double ModelPanelWidth = 210.0;
    public const double PropertyPanelWidth = 320.0;
    public const double ToolPanelWidth = 276.0;
    public const double PropertyLabelWidth = 116.0;
    public const double PropertyTitleHeight = 24.0;
    public const double PropertyCategoryHeaderHeight = 22.0;
    public const double PropertyRowHeight = 23.0;
    public const double PropertyRowIndent = 4.0;
    public const double PropertyChevronWidth = 14.0;
    public const double LayerHeaderHeight = 24.0;
    public const double LayerRowHeight = 24.0;
    public const double LayerActionButtonMinWidth = 44.0;
    public const double ToolLabelWidth = 88.0;
    public const double DialogButtonWidth = 76.0;
    public const double DialogPadding = 9.0;
    public const double DynamicHudMaxWidth = 270.0;
    public const double DynamicHudEstimatedHeight = 30.0;
    public const double DynamicHudOffset = 13.0;
    public const double OverlayMargin = 6.0;

    public static readonly IBrush WindowBrush = Brush("#D9DDE1");
    public static readonly IBrush Surface = Brush("#FAFAFA");
    public static readonly IBrush Panel = Brush("#F1F3F5");
    public static readonly IBrush PanelAlt = Brush("#E7EAED");
    public static readonly IBrush Toolbar = Brush("#ECEFF1");
    public static readonly IBrush Header = Brush("#E2E6E9");
    public static readonly IBrush HeaderHover = Brush("#D5E1EB");
    public static readonly IBrush Accent = Brush("#2D6F9F");
    public static readonly IBrush AccentSoft = Brush("#D9E8F2");
    public static readonly IBrush Border = Brush("#BFC5CA");
    public static readonly IBrush BorderStrong = Brush("#929AA1");
    public static readonly IBrush Splitter = Brush("#AEB5BB");
    public static readonly IBrush Text = Brush("#1F2326");
    public static readonly IBrush Muted = Brush("#5E666C");
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
                new Setter(Window.FontFamilyProperty,
                    FontFamily.Parse("Segoe UI, Microsoft YaHei UI, Noto Sans CJK SC, sans-serif"))
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
                new Setter(Button.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                new Setter(Button.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<ToggleButton>())
        {
            Setters =
            {
                new Setter(ToggleButton.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(ToggleButton.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                new Setter(ToggleButton.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<Button>().Class("cad-compact"))
        {
            Setters =
            {
                new Setter(Button.MinHeightProperty, ControlHeight),
                new Setter(Button.FontSizeProperty, FontSize),
                new Setter(Button.PaddingProperty, new Thickness(6, 0)),
                new Setter(Button.MarginProperty, new Thickness(1, 0)),
                new Setter(Button.BackgroundProperty, Brushes.Transparent),
                new Setter(Button.BorderBrushProperty, Brushes.Transparent),
                new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                new Setter(Button.ForegroundProperty, Text),
                new Setter(Button.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<Button>().Class("cad-primary"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, Accent),
                new Setter(Button.BorderBrushProperty, Accent),
                new Setter(Button.ForegroundProperty, Brushes.White),
                new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
                new Setter(Button.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<ToggleButton>().Class("cad-toggle"))
        {
            Setters =
            {
                new Setter(ToggleButton.MinHeightProperty, ControlHeight),
                new Setter(ToggleButton.FontSizeProperty, FontSize),
                new Setter(ToggleButton.PaddingProperty, new Thickness(5, 0)),
                new Setter(ToggleButton.MarginProperty, new Thickness(1, 0)),
                new Setter(ToggleButton.BackgroundProperty, Brushes.Transparent),
                new Setter(ToggleButton.BorderBrushProperty, Border),
                new Setter(ToggleButton.BorderThicknessProperty, new Thickness(1)),
                new Setter(ToggleButton.ForegroundProperty, Text),
                new Setter(ToggleButton.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TextBox>())
        {
            Setters =
            {
                new Setter(TextBox.FontSizeProperty, FontSize),
                new Setter(TextBox.ForegroundProperty, Text),
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(TextBox.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TextBox>().PropertyEquals(TextBox.AcceptsReturnProperty, true))
        {
            Setters =
            {
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top)
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TextBox>().Class("cad-input"))
        {
            Setters =
            {
                new Setter(TextBox.MinHeightProperty, ControlHeight),
                new Setter(TextBox.FontSizeProperty, FontSize),
                new Setter(TextBox.PaddingProperty, new Thickness(4, 0)),
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(TextBox.BackgroundProperty, Surface),
                new Setter(TextBox.BorderBrushProperty, Border),
                new Setter(TextBox.BorderThicknessProperty, new Thickness(1)),
                new Setter(TextBox.ForegroundProperty, Text),
                new Setter(TextBox.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<TextBox>().Class("cad-input").PropertyEquals(TextBox.AcceptsReturnProperty, true))
        {
            Setters =
            {
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top),
                new Setter(TextBox.PaddingProperty, new Thickness(4, 3))
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
                new Setter(ComboBox.ForegroundProperty, Text),
                new Setter(ComboBox.CornerRadiusProperty, new CornerRadius(0))
            }
        });

        app.Styles.Add(new Style(x => x.OfType<MenuItem>())
        {
            Setters =
            {
                new Setter(MenuItem.MinHeightProperty, MenuHeight),
                new Setter(MenuItem.FontSizeProperty, FontSize),
                new Setter(MenuItem.PaddingProperty, new Thickness(7, 1)),
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
                new Setter(TreeViewItem.MinHeightProperty, 21d),
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
                new Setter(TabItem.PaddingProperty, new Thickness(7, 1)),
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

    private static SolidColorBrush Brush(string value) => new(Color.Parse(value));
}
