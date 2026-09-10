using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DrawingColor = System.Drawing.Color;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

internal sealed class CadColorTable : UserControl
{
    private readonly record struct AciColor(
        int Index,
        DrawingColor Color);

    private static readonly IReadOnlyList<AciColor> StandardColors =
    [
        new(1, DrawingColor.FromArgb(255, 0, 0)),
        new(2, DrawingColor.FromArgb(255, 255, 0)),
        new(3, DrawingColor.FromArgb(0, 255, 0)),
        new(4, DrawingColor.FromArgb(0, 255, 255)),
        new(5, DrawingColor.FromArgb(0, 0, 255)),
        new(6, DrawingColor.FromArgb(255, 0, 255)),
        new(7, DrawingColor.White),
        new(8, DrawingColor.FromArgb(128, 128, 128)),
        new(9, DrawingColor.FromArgb(192, 192, 192))
    ];

    private static readonly IReadOnlyList<AciColor> IndexedColors =
        BuildIndexedColors();

    private static readonly IReadOnlyList<AciColor> GrayColors =
    [
        new(250, DrawingColor.FromArgb(51, 51, 51)),
        new(251, DrawingColor.FromArgb(80, 80, 80)),
        new(252, DrawingColor.FromArgb(105, 105, 105)),
        new(253, DrawingColor.FromArgb(130, 130, 130)),
        new(254, DrawingColor.FromArgb(190, 190, 190)),
        new(255, DrawingColor.FromArgb(255, 255, 255))
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
        var root = new StackPanel
        {
            Spacing = 6
        };

        root.Children.Add(
            SectionLabel(
                CadLanguageManager.Text(
                    "Cad.Text.StandardColors",
                    "Standard Colors")));
        root.Children.Add(
            BuildStandardRow());

        root.Children.Add(
            SectionLabel(
                CadLanguageManager.Text(
                    "Cad.Text.IndexColors",
                    "Index Colors")));
        root.Children.Add(
            BuildIndexedGrid());

        root.Children.Add(
            SectionLabel(
                CadLanguageManager.Text(
                    "Cad.Text.GrayColors",
                    "Gray Scale")));
        root.Children.Add(
            BuildGrayRow());

        return root;
    }

    private Control BuildStandardRow()
    {
        var grid = CreateGrid(
            StandardColors.Count,
            1,
            28,
            4);

        for (var index = 0;
             index < StandardColors.Count;
             index++)
        {
            var button =
                CreateColorButton(
                    StandardColors[index],
                    28);
            Grid.SetColumn(button, index);
            grid.Children.Add(button);
        }

        return grid;
    }

    private Control BuildIndexedGrid()
    {
        const int columns = 24;
        const int rows = 10;
        var grid = CreateGrid(
            columns,
            rows,
            20,
            1);

        foreach (var entry in IndexedColors)
        {
            var offset = entry.Index - 10;
            var column = offset / 10;
            var row = offset % 10;

            var button =
                CreateColorButton(
                    entry,
                    20);
            Grid.SetColumn(button, column);
            Grid.SetRow(button, row);
            grid.Children.Add(button);
        }

        return new Border
        {
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Child = grid
        };
    }

    private Control BuildGrayRow()
    {
        var grid = CreateGrid(
            GrayColors.Count,
            1,
            28,
            4);

        for (var index = 0;
             index < GrayColors.Count;
             index++)
        {
            var button =
                CreateColorButton(
                    GrayColors[index],
                    28);
            Grid.SetColumn(button, index);
            grid.Children.Add(button);
        }

        return grid;
    }

    private Grid CreateGrid(
        int columns,
        int rows,
        double cell,
        double spacing)
    {
        var grid = new Grid
        {
            ColumnSpacing = spacing,
            RowSpacing = spacing
        };

        for (var column = 0;
             column < columns;
             column++)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition(
                    new GridLength(cell)));
        }

        for (var row = 0;
             row < rows;
             row++)
        {
            grid.RowDefinitions.Add(
                new RowDefinition(
                    new GridLength(cell)));
        }

        return grid;
    }

    private Button CreateColorButton(
        AciColor entry,
        double size)
    {
        var button = new Button
        {
            Tag = entry,
            Width = size,
            Height = size,
            MinWidth = size,
            MinHeight = size,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background =
                new SolidColorBrush(
                    ToMediaColor(entry.Color)),
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(1),
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        ToolTip.SetTip(
            button,
            $"ACI {entry.Index}   RGB {entry.Color.R}, {entry.Color.G}, {entry.Color.B}");
        button.Click += ColorButtonClicked;
        _buttons.Add(button);
        return button;
    }

    private void ColorButtonClicked(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button
            {
                Tag: AciColor entry
            })
            return;

        SelectedColor = entry.Color;
        ColorChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void RefreshSelection()
    {
        foreach (var button in _buttons)
        {
            var selected =
                button.Tag is AciColor entry &&
                entry.Color.ToArgb() ==
                _selectedColor.ToArgb();

            button.BorderBrush =
                selected
                    ? CadTheme.Accent
                    : CadTheme.BorderStrong;
            button.BorderThickness =
                selected
                    ? new Thickness(3)
                    : new Thickness(1);
        }
    }

    private static Control SectionLabel(
        string text) =>
        new Border
        {
            MinHeight = 24,
            Background = CadTheme.Header,
            BorderBrush = CadTheme.Border,
            BorderThickness =
                new Thickness(0, 0, 0, 1),
            Padding = new Thickness(6, 0),
            Child = new TextBlock
            {
                Text = text,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment =
                    VerticalAlignment.Center,
                Foreground = CadTheme.Text
            }
        };

    private static IReadOnlyList<AciColor>
        BuildIndexedColors()
    {
        var values = new List<AciColor>(240);
        var valueLevels =
            new[] { 1.0, 1.0, 0.65, 0.65, 0.5, 0.5, 0.30, 0.30, 0.15, 0.15 };
        var saturationLevels =
            new[] { 1.0, 0.5, 1.0, 0.5, 1.0, 0.5, 1.0, 0.5, 1.0, 0.5 };

        for (var hueIndex = 0;
             hueIndex < 24;
             hueIndex++)
        {
            var hue = hueIndex * 15.0;
            for (var shade = 0;
                 shade < 10;
                 shade++)
            {
                var index =
                    10 + hueIndex * 10 + shade;
                values.Add(
                    new AciColor(
                        index,
                        FromHsv(
                            hue,
                            saturationLevels[shade],
                            valueLevels[shade])));
            }
        }

        return values;
    }

    private static DrawingColor FromHsv(
        double hue,
        double saturation,
        double value)
    {
        var chroma = value * saturation;
        var sector = hue / 60.0;
        var x =
            chroma *
            (1.0 -
             Math.Abs(
                 sector % 2.0 -
                 1.0));

        var (r1, g1, b1) =
            sector switch
            {
                >= 0 and < 1 =>
                    (chroma, x, 0.0),
                >= 1 and < 2 =>
                    (x, chroma, 0.0),
                >= 2 and < 3 =>
                    (0.0, chroma, x),
                >= 3 and < 4 =>
                    (0.0, x, chroma),
                >= 4 and < 5 =>
                    (x, 0.0, chroma),
                _ =>
                    (chroma, 0.0, x)
            };

        var m = value - chroma;
        return DrawingColor.FromArgb(
            255,
            Channel(r1 + m),
            Channel(g1 + m),
            Channel(b1 + m));
    }

    private static int Channel(double value) =>
        Math.Clamp(
            (int)Math.Round(value * 255.0),
            0,
            255);

    private static MediaColor ToMediaColor(
        DrawingColor value) =>
        MediaColor.FromArgb(
            value.A,
            value.R,
            value.G,
            value.B);
}
