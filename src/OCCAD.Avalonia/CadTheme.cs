using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace OCCAD.Avalonia;

internal static class CadTheme
{
    public static readonly IBrush WindowBrush = Brush("#E9EDF1");
    public static readonly IBrush Panel = Brush("#F8FAFC");
    public static readonly IBrush PanelAlt = Brush("#EEF2F5");
    public static readonly IBrush Header = Brush("#303942");
    public static readonly IBrush HeaderHover = Brush("#3B4652");
    public static readonly IBrush Accent = Brush("#3278B8");
    public static readonly IBrush Border = Brush("#C7CED6");
    public static readonly IBrush Text = Brush("#20272D");
    public static readonly IBrush Muted = Brush("#66727D");
    public static readonly IBrush Viewport = Brushes.Black;

    public static void Apply(Application app)
    {
        app.Resources["Cad.Window"] = WindowBrush;
        app.Resources["Cad.Panel"] = Panel;
        app.Resources["Cad.PanelAlt"] = PanelAlt;
        app.Resources["Cad.Header"] = Header;
        app.Resources["Cad.Accent"] = Accent;
        app.Resources["Cad.Border"] = Border;
        app.Resources["Cad.Text"] = Text;
        app.Resources["Cad.Muted"] = Muted;

        app.Styles.Add(new Style(x => x.OfType<Window>())
        {
            Setters =
            {
                new Setter(Window.BackgroundProperty, WindowBrush),
                new Setter(Window.FontFamilyProperty, FontFamily.Parse("Segoe UI, Noto Sans CJK SC, sans-serif"))
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("cad-compact"))
        {
            Setters =
            {
                new Setter(Button.MinHeightProperty, 25d),
                new Setter(Button.PaddingProperty, new Thickness(8, 2)),
                new Setter(Button.MarginProperty, new Thickness(2))
            }
        });
        app.Styles.Add(new Style(x => x.OfType<TextBox>().Class("cad-input"))
        {
            Setters =
            {
                new Setter(TextBox.MinHeightProperty, 25d),
                new Setter(TextBox.PaddingProperty, new Thickness(5, 2))
            }
        });
        app.Styles.Add(new Style(x => x.OfType<ComboBox>().Class("cad-input"))
        {
            Setters =
            {
                new Setter(ComboBox.MinHeightProperty, 25d)
            }
        });
    }

    public static Border Card(Control child, Thickness? padding = null) =>
        new()
        {
            Background = Panel,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = padding ?? new Thickness(8),
            Child = child
        };

    public static TextBlock SectionTitle(string text) =>
        new()
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Foreground = Text,
            Margin = new Thickness(0, 0, 0, 6)
        };

    private static SolidColorBrush Brush(string value) =>
        new(Color.Parse(value));
}
