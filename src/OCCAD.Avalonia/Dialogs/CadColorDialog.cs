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
        Title = CadLanguageManager.Text("Cad.Text.ColorTable", "Color table");
        Width = 600;
        MinWidth = 580;
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
            Background = CadTheme.Surface,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = _picker
        };

        _preview = new Border
        {
            Width = 42,
            Height = CadTheme.ControlHeight,
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(ToMediaColor(initial))
        };

        var custom = CreateDialogButton(CadLanguageManager.Text("Cad.Text.MoreColors", "More colors"));
        custom.HorizontalAlignment = HorizontalAlignment.Left;
        custom.Click += (_, _) => _customHost.IsVisible = !_customHost.IsVisible;

        var top = new Grid { ColumnSpacing = 6 };
        top.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        top.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        top.Children.Add(custom);
        Grid.SetColumn(_preview, 1);
        top.Children.Add(_preview);

        var body = new StackPanel
        {
            Margin = new Thickness(CadTheme.DialogPadding),
            Spacing = 8
        };
        body.Children.Add(_table);
        body.Children.Add(top);
        body.Children.Add(_customHost);

        var cancel = CreateDialogButton(CadLanguageManager.Text("Cad.Text.Cancel", "Cancel"));
        cancel.Click += (_, _) => Close(null);
        var ok = CreateDialogButton(CadLanguageManager.Text("Cad.Text.OK", "OK"));
        ok.Click += (_, _) => Close(_selected);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 4
        };
        actions.Children.Add(cancel);
        actions.Children.Add(ok);

        var footer = new Border
        {
            Background = CadTheme.Panel,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(CadTheme.DialogPadding, 6),
            Child = actions
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        root.Children.Add(body);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        Content = root;
    }

    public static Task<DrawingColor?> ShowAsync(Window owner, DrawingColor initial) =>
        new CadColorDialog(initial).ShowDialog<DrawingColor?>(owner);

    private void RefreshPreview() =>
        _preview.Background = new SolidColorBrush(ToMediaColor(_selected));

    private static Button CreateDialogButton(string text) =>
        new()
        {
            Content = text,
            MinWidth = CadTheme.DialogButtonWidth,
            MinHeight = CadTheme.ControlHeight,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

    private static MediaColor ToMediaColor(DrawingColor value) =>
        MediaColor.FromArgb(value.A, value.R, value.G, value.B);

    private static DrawingColor ToDrawingColor(MediaColor value) =>
        DrawingColor.FromArgb(value.A, value.R, value.G, value.B);
}
