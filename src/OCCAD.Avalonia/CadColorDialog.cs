using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DrawingColor = System.Drawing.Color;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

internal sealed class CadColorDialog : Window
{
    private readonly ColorPicker _picker;

    private CadColorDialog(DrawingColor initial)
    {
        Title = CadLanguageManager.Text(
            "Cad.Text.Color",
            "Color");
        Width = 430;
        Height = 520;
        MinWidth = 360;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadTheme.WindowBrush;

        _picker = new ColorPicker
        {
            Color = ToMediaColor(initial),
            IsAlphaEnabled = false,
            IsAlphaVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6
        };

        var ok = CompactButton(
            CadLanguageManager.Text(
                "Cad.Text.Accept",
                "OK"));
        ok.Click += (_, _) =>
            Close(ToDrawingColor(_picker.Color));

        var cancel = CompactButton(
            CadLanguageManager.Text(
                "Cad.Text.Cancel",
                "Cancel"));
        cancel.Click += (_, _) =>
            Close(null);

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var grid = new Grid
        {
            Margin = new Thickness(12)
        };
        grid.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(1, GridUnitType.Star)));
        grid.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));
        grid.Children.Add(_picker);
        Grid.SetRow(buttons, 1);
        grid.Children.Add(buttons);
        Content = grid;
    }

    public static Task<DrawingColor?> ShowAsync(
        Window owner,
        DrawingColor initial) =>
        new CadColorDialog(initial)
            .ShowDialog<DrawingColor?>(owner);

    private static Button CompactButton(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 78,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        return button;
    }

    private static MediaColor ToMediaColor(DrawingColor value) =>
        MediaColor.FromArgb(
            value.A,
            value.R,
            value.G,
            value.B);

    private static DrawingColor ToDrawingColor(MediaColor value) =>
        DrawingColor.FromArgb(
            value.A,
            value.R,
            value.G,
            value.B);
}
