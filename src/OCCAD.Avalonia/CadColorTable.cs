using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DrawingColor = System.Drawing.Color;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

internal sealed class CadColorTable : UserControl
{
    private static readonly DrawingColor[] Colors =
    [
        DrawingColor.Black,
        DrawingColor.FromArgb(64, 64, 64),
        DrawingColor.FromArgb(96, 96, 96),
        DrawingColor.FromArgb(128, 128, 128),
        DrawingColor.FromArgb(160, 160, 160),
        DrawingColor.FromArgb(192, 192, 192),
        DrawingColor.FromArgb(224, 224, 224),
        DrawingColor.White,

        DrawingColor.FromArgb(192, 32, 32),
        DrawingColor.FromArgb(230, 72, 60),
        DrawingColor.FromArgb(240, 138, 128),
        DrawingColor.FromArgb(138, 74, 36),
        DrawingColor.FromArgb(188, 120, 70),
        DrawingColor.FromArgb(228, 176, 116),
        DrawingColor.FromArgb(176, 126, 22),
        DrawingColor.FromArgb(232, 184, 58),

        DrawingColor.FromArgb(226, 214, 62),
        DrawingColor.FromArgb(128, 170, 42),
        DrawingColor.FromArgb(70, 148, 58),
        DrawingColor.FromArgb(34, 116, 72),
        DrawingColor.FromArgb(48, 154, 132),
        DrawingColor.FromArgb(54, 174, 174),
        DrawingColor.FromArgb(58, 146, 196),
        DrawingColor.FromArgb(54, 104, 176),

        DrawingColor.FromArgb(68, 76, 188),
        DrawingColor.FromArgb(92, 66, 180),
        DrawingColor.FromArgb(132, 72, 186),
        DrawingColor.FromArgb(174, 72, 176),
        DrawingColor.FromArgb(196, 76, 146),
        DrawingColor.FromArgb(208, 88, 114),
        DrawingColor.FromArgb(112, 84, 84),
        DrawingColor.FromArgb(132, 110, 98),

        DrawingColor.FromArgb(38, 74, 98),
        DrawingColor.FromArgb(46, 96, 122),
        DrawingColor.FromArgb(54, 118, 146),
        DrawingColor.FromArgb(64, 138, 158),
        DrawingColor.FromArgb(82, 126, 96),
        DrawingColor.FromArgb(102, 144, 104),
        DrawingColor.FromArgb(132, 154, 116),
        DrawingColor.FromArgb(164, 170, 136)
    ];

    private readonly List<Button> _buttons = [];
    private DrawingColor _selectedColor;

    public CadColorTable(DrawingColor selectedColor)
    {
        _selectedColor = selectedColor;
        Content = Build();
        RefreshSelection();
    }

    public DrawingColor SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor.ToArgb() == value.ToArgb())
                return;

            _selectedColor = value;
            RefreshSelection();
        }
    }

    public event EventHandler? ColorChanged;

    private Control Build()
    {
        const int columns = 8;
        var rows =
            (int)Math.Ceiling(
                Colors.Length / (double)columns);

        var grid = new Grid
        {
            ColumnSpacing = 3,
            RowSpacing = 3
        };

        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(
                new ColumnDefinition(
                    new GridLength(1, GridUnitType.Star)));
        for (var row = 0; row < rows; row++)
            grid.RowDefinitions.Add(
                new RowDefinition(new GridLength(27)));

        for (var index = 0; index < Colors.Length; index++)
        {
            var color = Colors[index];
            var button = new Button
            {
                Tag = color,
                MinWidth = 24,
                MinHeight = CadTheme.ControlHeight,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Background =
                    new SolidColorBrush(ToMediaColor(color)),
                BorderBrush = CadTheme.Border,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            ToolTip.SetTip(
                button,
                $"RGB {color.R}, {color.G}, {color.B}");
            button.Click += ColorButtonClicked;

            Grid.SetColumn(
                button,
                index % columns);
            Grid.SetRow(
                button,
                index / columns);
            grid.Children.Add(button);
            _buttons.Add(button);
        }

        return grid;
    }

    private void ColorButtonClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button
            {
                Tag: DrawingColor color
            })
            return;

        SelectedColor = color;
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshSelection()
    {
        foreach (var button in _buttons)
        {
            var selected =
                button.Tag is DrawingColor color &&
                color.ToArgb() ==
                    _selectedColor.ToArgb();

            button.BorderBrush =
                selected
                    ? CadTheme.Accent
                    : CadTheme.Border;
            button.BorderThickness =
                selected
                    ? new Thickness(2)
                    : new Thickness(1);
        }
    }

    private static MediaColor ToMediaColor(
        DrawingColor value) =>
        MediaColor.FromArgb(
            value.A,
            value.R,
            value.G,
            value.B);
}
