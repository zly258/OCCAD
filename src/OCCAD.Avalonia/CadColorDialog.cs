using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DrawingColor = System.Drawing.Color;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

internal sealed class CadColorDialog : Window
{
    private readonly CadColorTable _table;
    private readonly ColorPicker _picker;
    private readonly Border _customHost;
    private readonly Border _preview;
    private DrawingColor _selected;

    private CadColorDialog(DrawingColor initial)
    {
        _selected = initial;

        Title = CadLanguageManager.Text(
            "Cad.Text.ColorTable",
            "Color table");
        Width = 360;
        MinWidth = 340;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadTheme.Surface;

        _picker = new ColorPicker
        {
            Color = ToMediaColor(initial),
            IsAlphaEnabled = false,
            IsAlphaVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _table = new CadColorTable(initial);
        _table.ColorChanged += (_, _) =>
        {
            _selected = _table.SelectedColor;
            _picker.Color = ToMediaColor(_selected);
            RefreshPreview();
        };
        _picker.ColorChanged += (_, args) =>
        {
            _selected = ToDrawingColor(args.NewColor);
            _table.SelectedColor = _selected;
            RefreshPreview();
        };

        _customHost = new Border
        {
            IsVisible = false,
            Background = CadTheme.Panel,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = _picker
        };

        _preview = new Border
        {
            Width = 44,
            Height = 24,
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(
                ToMediaColor(initial))
        };

        var custom = CompactButton(
            CadLanguageManager.Text(
                "Cad.Text.MoreColors",
                "More colors"));
        custom.Click += (_, _) =>
        {
            _customHost.IsVisible =
                !_customHost.IsVisible;
        };

        var top = new Grid
        {
            ColumnSpacing = 8
        };
        top.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(1, GridUnitType.Star)));
        top.ColumnDefinitions.Add(
            new ColumnDefinition(GridLength.Auto));
        top.Children.Add(custom);
        Grid.SetColumn(_preview, 1);
        top.Children.Add(_preview);

        var ok = CompactButton(
            CadLanguageManager.Text(
                "Cad.Text.Ok",
                "OK"));
        ok.Classes.Add("cad-primary");
        ok.Click += (_, _) => Close(_selected);

        var cancel = CompactButton(
            CadLanguageManager.Text(
                "Cad.Text.Cancel",
                "Cancel"));
        cancel.Click += (_, _) => Close(null);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var content = new StackPanel
        {
            Margin = new Thickness(12),
            Spacing = 10
        };
        content.Children.Add(_table);
        content.Children.Add(top);
        content.Children.Add(_customHost);
        content.Children.Add(buttons);
        Content = content;
    }

    public static Task<DrawingColor?> ShowAsync(
        Window owner,
        DrawingColor initial) =>
        new CadColorDialog(initial)
            .ShowDialog<DrawingColor?>(owner);

    private void RefreshPreview()
    {
        _preview.Background =
            new SolidColorBrush(
                ToMediaColor(_selected));
    }

    private static Button CompactButton(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 84,
            HorizontalContentAlignment =
                HorizontalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        return button;
    }

    private static MediaColor ToMediaColor(
        DrawingColor value) =>
        MediaColor.FromArgb(
            value.A,
            value.R,
            value.G,
            value.B);

    private static DrawingColor ToDrawingColor(
        MediaColor value) =>
        DrawingColor.FromArgb(
            value.A,
            value.R,
            value.G,
            value.B);
}
