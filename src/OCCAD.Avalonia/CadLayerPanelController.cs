using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;
using OcctNet;
using DrawingColor = System.Drawing.Color;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

internal sealed class CadLayerPanelController : IDisposable
{
    private static readonly double[] LineWidths =
        [0.25, 0.35, 0.50, 0.70, 1.00, 1.40, 2.00, 3.00];

    private readonly Window _owner;
    private readonly CadWorkspace _workspace;
    private readonly StackPanel _host;
    private readonly Action<CadLayer> _inspectLayer;
    private readonly TextBox _search;
    private bool _refreshing;
    private bool _disposed;

    public CadLayerPanelController(
        Window owner,
        CadWorkspace workspace,
        StackPanel host,
        Action<CadLayer> inspectLayer)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _inspectLayer = inspectLayer ?? throw new ArgumentNullException(nameof(inspectLayer));

        _search = new TextBox
        {
            PlaceholderText = CadLanguageManager.Text(
                "Cad.Text.FilterLayers",
                "Filter layers")
        };
        _search.Classes.Add("cad-input");
        _search.TextChanged += (_, _) => Rebuild();

        _workspace.Layers.Changed += LayersChanged;
        Rebuild();
    }

    public void Refresh() => Rebuild();

    public void RefreshLanguage()
    {
        _search.PlaceholderText = CadLanguageManager.Text(
            "Cad.Text.FilterLayers",
            "Filter layers");
        Rebuild();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workspace.Layers.Changed -= LayersChanged;
        _host.Children.Clear();
    }

    private void LayersChanged(
        object? sender,
        CadLayerManagerChangedEventArgs e) =>
        Rebuild();

    private void Rebuild()
    {
        if (_disposed || _refreshing)
            return;

        _refreshing = true;
        try
        {
            if (_search.Parent is Panel searchParent)
                searchParent.Children.Remove(_search);
            _host.Children.Clear();

            var toolbar = new Grid
            {
                ColumnSpacing = 4,
                Margin = new Thickness(7, 6, 7, 5)
            };
            toolbar.ColumnDefinitions.Add(
                new ColumnDefinition(
                    new GridLength(1, GridUnitType.Star)));
            toolbar.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Auto));
            toolbar.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Auto));
            toolbar.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Auto));

            toolbar.Children.Add(_search);

            var add = CompactButton(
                CadLanguageManager.Text("Cad.Text.New", "New"));
            add.Click += async (_, _) => await AddLayerAsync();
            Grid.SetColumn(add, 1);
            toolbar.Children.Add(add);

            var rename = CompactButton(
                CadLanguageManager.Text("Cad.Text.Rename", "Rename"));
            rename.Click += async (_, _) => await RenameLayerAsync();
            Grid.SetColumn(rename, 2);
            toolbar.Children.Add(rename);

            var remove = CompactButton(
                CadLanguageManager.Text("Cad.Text.Remove", "Remove"));
            remove.Click += (_, _) => RemoveLayer();
            Grid.SetColumn(remove, 3);
            toolbar.Children.Add(remove);

            _host.Children.Add(toolbar);
            _host.Children.Add(CreateColumnHeader());

            var filter = _search.Text?.Trim() ?? string.Empty;
            var layers = _workspace.Layers.Layers
                .Where(layer =>
                    filter.Length == 0 ||
                    layer.Name.Contains(
                        filter,
                        StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(
                    static layer => layer.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            for (var index = 0; index < layers.Length; index++)
                _host.Children.Add(CreateLayerRow(layers[index], index));

            _host.Children.Add(new Border
            {
                MinHeight = CadTheme.LayerHeaderHeight,
                Background = CadTheme.PanelAlt,
                BorderBrush = CadTheme.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 0),
                Child = new TextBlock
                {
                    Text = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        CadLanguageManager.Text(
                            "Cad.Text.LayerCurrent",
                            "Layer: {0}"),
                        _workspace.Layers.Current.Name),
                    Foreground = CadTheme.Muted,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    TextTrimming =
                        TextTrimming.CharacterEllipsis
                }
            });
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Control CreateColumnHeader()
    {
        var grid = new Grid
        {
            MinHeight = CadTheme.LayerHeaderHeight,
            ColumnSpacing = 3,
            Margin = new Thickness(6, 0),
            Background = CadTheme.Header
        };
        ConfigureLayerColumns(grid);

        AddHeader(
            grid,
            0,
            CadLanguageManager.Text(
                "Cad.Text.Name",
                "Name"),
            HorizontalAlignment.Left);
        AddHeader(
            grid,
            1,
            CadLanguageManager.Text(
                "Cad.Text.Visible",
                "Visible"));
        AddHeader(
            grid,
            2,
            CadLanguageManager.Text(
                "Cad.Text.Color",
                "Color"));
        AddHeader(
            grid,
            3,
            CadLanguageManager.Text(
                "Cad.Text.LineStyle",
                "Line style"));
        AddHeader(
            grid,
            4,
            CadLanguageManager.Text(
                "Cad.Text.LineWidth",
                "Line width"));
        AddHeader(
            grid,
            5,
            CadLanguageManager.Text(
                "Cad.Text.Locked",
                "Locked"));

        return new Border
        {
            Background = CadTheme.Header,
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(0, 1, 0, 1),
            Child = grid
        };
    }

    private static void ConfigureLayerColumns(Grid grid)
    {
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(40)));
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(38)));
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(68)));
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(58)));
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(40)));
    }

    private static void AddHeader(
        Grid grid,
        int column,
        string text,
        HorizontalAlignment alignment =
            HorizontalAlignment.Center)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = CadTheme.SmallFontSize,
            FontWeight = FontWeight.SemiBold,
            Foreground = CadTheme.Muted,
            Margin = new Thickness(4, 0),
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }

    private Control CreateLayerRow(
        CadLayer layer,
        int rowIndex)
    {
        var current = ReferenceEquals(
            layer,
            _workspace.Layers.Current);

        var grid = new Grid
        {
            MinHeight = CadTheme.LayerRowHeight,
            ColumnSpacing = 3,
            VerticalAlignment =
                VerticalAlignment.Center
        };
        ConfigureLayerColumns(grid);

        var name = new Button
        {
            Content = layer.Name,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = current
                ? CadTheme.AccentSoft
                : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 1),
            VerticalContentAlignment =
                VerticalAlignment.Center,
            Foreground = CadTheme.Text,
            FontWeight = current
                ? FontWeight.SemiBold
                : FontWeight.Normal
        };
        name.Click += (_, _) =>
        {
            _workspace.SetCurrentLayer(layer);
            _inspectLayer(layer);
        };
        grid.Children.Add(name);

        var width = new ComboBox
        {
            ItemsSource = LineWidths,
            SelectedItem = LineWidths.FirstOrDefault(
                value => Math.Abs(value - layer.LineWidth) < 1e-12)
        };
        width.Classes.Add("cad-input");
        width.SelectionChanged += (_, _) =>
        {
            if (_refreshing ||
                width.SelectedItem is not double value ||
                Math.Abs(value - layer.LineWidth) < 1e-12)
                return;

            TryLayerChange(
                () => _workspace.SetLayerLineWidth(
                    layer,
                    value));
        };
        Grid.SetColumn(width, 4);
        grid.Children.Add(width);

        var styles = Enum.GetValues<OcctLineStyle>()
            .Select(value => new StyleChoice(
                value,
                CadLanguageManager.Text(
                    $"Cad.Value.OcctLineStyle.{value}",
                    value.ToString())))
            .ToArray();
        var style = new ComboBox
        {
            ItemsSource = styles,
            SelectedItem = styles.FirstOrDefault(
                item => item.Value == layer.LineStyle)
        };
        style.Classes.Add("cad-input");
        style.SelectionChanged += (_, _) =>
        {
            if (_refreshing ||
                style.SelectedItem is not StyleChoice item ||
                item.Value == layer.LineStyle)
                return;

            TryLayerChange(
                () => _workspace.SetLayerLineStyle(
                    layer,
                    item.Value));
        };
        Grid.SetColumn(style, 3);
        grid.Children.Add(style);

        var visible = new CheckBox
        {
            IsChecked = layer.Visible,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(
            visible,
            CadLanguageManager.Text("Cad.Text.Visible", "Visible"));
        visible.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing) return;
            TryLayerChange(
                () => _workspace.SetLayerVisible(
                    layer,
                    visible.IsChecked == true));
        };
        Grid.SetColumn(visible, 1);
        grid.Children.Add(visible);

        var locked = new CheckBox
        {
            IsChecked = layer.Locked,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(
            locked,
            CadLanguageManager.Text("Cad.Text.Locked", "Locked"));
        locked.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing) return;
            TryLayerChange(
                () => _workspace.SetLayerLocked(
                    layer,
                    locked.IsChecked == true));
        };
        Grid.SetColumn(locked, 5);
        grid.Children.Add(locked);

        var color = new Button
        {
            Width = 30,
            MinHeight = CadTheme.ControlHeight,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(
                ToMediaColor(layer.Color)),
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(1)
        };
        ToolTip.SetTip(
            color,
            CadLanguageManager.Text(
                "Cad.Text.Color",
                "Color"));
        color.Click += async (_, _) =>
        {
            if (_refreshing)
                return;

            var selected = await CadColorDialog.ShowAsync(
                _owner,
                layer.Color);
            if (selected is not { } next ||
                next.ToArgb() == layer.Color.ToArgb())
                return;

            TryLayerChange(
                () => _workspace.SetLayerColor(
                    layer,
                    next));
        };
        Grid.SetColumn(color, 2);
        grid.Children.Add(color);

        return new Border
        {
            Background = current
                ? CadTheme.AccentSoft
                : CadTheme.Surface,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            MinHeight = CadTheme.LayerRowHeight,
            Padding = new Thickness(6, 1),
            Margin = new Thickness(0),
            Child = grid
        };
    }

    private async Task AddLayerAsync()
    {
        var suggested =
            _workspace.Layers.GenerateUniqueName("Layer");
        var dialog = new LayerNameDialog(
            suggested,
            creating: true);
        var name = await dialog.ShowDialog<string?>(_owner);
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            _workspace.AddLayer(name.Trim());
        }
        catch (Exception exception)
        {
            ShowLayerError(exception);
            Rebuild();
        }
    }

    private async Task RenameLayerAsync()
    {
        var layer = _workspace.Layers.Current;
        if (layer.IsDefault)
            return;

        var dialog = new LayerNameDialog(
            layer.Name,
            creating: false);
        var name = await dialog.ShowDialog<string?>(_owner);
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            _workspace.RenameLayer(
                layer,
                name.Trim());
        }
        catch (Exception exception)
        {
            ShowLayerError(exception);
            Rebuild();
        }
    }

    private void RemoveLayer()
    {
        var layer = _workspace.Layers.Current;
        if (layer.IsDefault)
            return;

        try
        {
            _workspace.RemoveLayer(layer);
        }
        catch (Exception exception)
        {
            ShowLayerError(exception);
            Rebuild();
        }
    }

    private void TryLayerChange(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);

        try
        {
            change();
        }
        catch (Exception exception)
        {
            ShowLayerError(exception);
        }

        Rebuild();
    }

    private void ShowLayerError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _ = CadMessageDialog.ShowAsync(
            _owner,
            CadLanguageManager.Text(
                "Cad.Text.ErrorTitle",
                "OCCAD Error"),
            exception.GetBaseException().Message,
            kind: CadMessageDialogKind.Error);
    }

    private static Button CompactButton(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = CadTheme.LayerActionButtonMinWidth,
            HorizontalContentAlignment =
                HorizontalAlignment.Center,
            VerticalContentAlignment =
                VerticalAlignment.Center,
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(1)
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

    private sealed record StyleChoice(
        OcctLineStyle Value,
        string Label)
    {
        public override string ToString() => Label;
    }
}
