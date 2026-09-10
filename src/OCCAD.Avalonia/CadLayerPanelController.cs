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
                ColumnSpacing = 3,
                Margin = new Thickness(5, 4, 5, 4)
            };
            toolbar.ColumnDefinitions.Add(
                new ColumnDefinition(
                    new GridLength(1, GridUnitType.Star)));
            toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

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
            ColumnSpacing = 1,
            Margin = new Thickness(3, 0),
            Background = CadTheme.Header
        };
        ConfigureLayerColumns(grid);

        AddHeader(
            grid,
            0,
            CadLanguageManager.Text("Cad.Text.Current", "Current"),
            "●");
        AddHeader(
            grid,
            1,
            CadLanguageManager.Text("Cad.Text.Name", "Name"),
            CadLanguageManager.Text("Cad.Text.Name", "Name"),
            HorizontalAlignment.Left);
        AddHeader(
            grid,
            2,
            CadLanguageManager.Text("Cad.Text.Visible", "Visible"),
            ShortHeader("显", "V"));
        AddHeader(
            grid,
            3,
            CadLanguageManager.Text("Cad.Text.Color", "Color"),
            ShortHeader("色", "C"));
        AddHeader(
            grid,
            4,
            CadLanguageManager.Text("Cad.Text.LineStyle", "Line style"),
            ShortHeader("线型", "Style"));
        AddHeader(
            grid,
            5,
            CadLanguageManager.Text("Cad.Text.LineWidth", "Line width"),
            ShortHeader("线宽", "Width"));
        AddHeader(
            grid,
            6,
            CadLanguageManager.Text("Cad.Text.Locked", "Locked"),
            ShortHeader("锁", "L"));

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
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(26)));
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(28)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(32)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(56)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(48)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(28)));
    }

    private static void AddHeader(
        Grid grid,
        int column,
        string fullText,
        string displayText,
        HorizontalAlignment alignment = HorizontalAlignment.Center)
    {
        var label = new TextBlock
        {
            Text = displayText,
            FontSize = CadTheme.SmallFontSize,
            FontWeight = FontWeight.SemiBold,
            Foreground = CadTheme.Muted,
            Margin = new Thickness(2, 0),
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTip.SetTip(label, fullText);
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }

    private Control CreateLayerRow(CadLayer layer, int rowIndex)
    {
        var current = ReferenceEquals(layer, _workspace.Layers.Current);
        var rowBackground = current
            ? CadTheme.AccentSoft
            : rowIndex % 2 == 0
                ? CadTheme.Surface
                : CadTheme.Panel;

        var grid = new Grid
        {
            MinHeight = CadTheme.LayerRowHeight,
            ColumnSpacing = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        ConfigureLayerColumns(grid);

        var currentLayer = new Button
        {
            Content = current ? "●" : "○",
            IsEnabled = !current,
            Width = 24,
            Height = Math.Max(18, CadTheme.ControlHeight - 4),
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = current ? CadTheme.Accent : CadTheme.Muted
        };
        ToolTip.SetTip(
            currentLayer,
            current
                ? CadLanguageManager.Text("Cad.Text.CurrentLayer", "Current layer")
                : CadLanguageManager.Text("Cad.Text.SetCurrentLayer", "Set current layer"));
        currentLayer.Click += (_, _) =>
        {
            if (_refreshing || current)
                return;

            _workspace.SetCurrentLayer(layer);
            _inspectLayer(layer);
        };
        grid.Children.Add(currentLayer);

        var name = new Button
        {
            Content = layer.Name,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = CadTheme.Text,
            FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal
        };
        name.Click += (_, _) => _inspectLayer(layer);
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var visible = new CheckBox
        {
            IsChecked = layer.Visible,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(visible, CadLanguageManager.Text("Cad.Text.Visible", "Visible"));
        visible.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing) return;
            TryLayerChange(
                () => _workspace.SetLayerVisible(layer, visible.IsChecked == true));
        };
        Grid.SetColumn(visible, 2);
        grid.Children.Add(visible);

        var color = new Button
        {
            Width = 24,
            Height = Math.Max(18, CadTheme.ControlHeight - 4),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(ToMediaColor(layer.Color)),
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(1)
        };
        ToolTip.SetTip(color, CadLanguageManager.Text("Cad.Text.Color", "Color"));
        color.Click += async (_, _) =>
        {
            if (_refreshing)
                return;

            var selected = await CadColorDialog.ShowAsync(_owner, layer.Color);
            if (selected is not { } next || next.ToArgb() == layer.Color.ToArgb())
                return;

            TryLayerChange(() => _workspace.SetLayerColor(layer, next));
        };
        Grid.SetColumn(color, 3);
        grid.Children.Add(color);

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
            SelectedItem = styles.FirstOrDefault(item => item.Value == layer.LineStyle),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        style.Classes.Add("cad-input");
        style.SelectionChanged += (_, _) =>
        {
            if (_refreshing ||
                style.SelectedItem is not StyleChoice item ||
                item.Value == layer.LineStyle)
                return;

            TryLayerChange(() => _workspace.SetLayerLineStyle(layer, item.Value));
        };
        Grid.SetColumn(style, 4);
        grid.Children.Add(style);

        var width = new ComboBox
        {
            ItemsSource = LineWidths,
            SelectedItem = LineWidths.FirstOrDefault(
                value => Math.Abs(value - layer.LineWidth) < 1e-12),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        width.Classes.Add("cad-input");
        width.SelectionChanged += (_, _) =>
        {
            if (_refreshing ||
                width.SelectedItem is not double value ||
                Math.Abs(value - layer.LineWidth) < 1e-12)
                return;

            TryLayerChange(() => _workspace.SetLayerLineWidth(layer, value));
        };
        Grid.SetColumn(width, 5);
        grid.Children.Add(width);

        var locked = new CheckBox
        {
            IsChecked = layer.Locked,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(locked, CadLanguageManager.Text("Cad.Text.Locked", "Locked"));
        locked.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing) return;
            TryLayerChange(() => _workspace.SetLayerLocked(layer, locked.IsChecked == true));
        };
        Grid.SetColumn(locked, 6);
        grid.Children.Add(locked);

        return new Border
        {
            Background = rowBackground,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            MinHeight = CadTheme.LayerRowHeight,
            Padding = new Thickness(3, 0),
            Margin = new Thickness(0),
            Child = grid
        };
    }

    private async Task AddLayerAsync()
    {
        var suggested = _workspace.Layers.GenerateUniqueName("Layer");
        var dialog = new LayerNameDialog(suggested, creating: true);
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

        var dialog = new LayerNameDialog(layer.Name, creating: false);
        var name = await dialog.ShowDialog<string?>(_owner);
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            _workspace.RenameLayer(layer, name.Trim());
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
            CadLanguageManager.Text("Cad.Text.ErrorTitle", "OCCAD Error"),
            exception.GetBaseException().Message,
            kind: CadMessageDialogKind.Error);
    }

    private static Button CompactButton(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = CadTheme.LayerActionButtonMinWidth,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(1)
        };
        button.Classes.Add("cad-compact");
        return button;
    }

    private static string ShortHeader(string chinese, string english) =>
        string.Equals(
            CadLanguageManager.CurrentLanguage,
            "zh-CN",
            StringComparison.OrdinalIgnoreCase)
            ? chinese
            : english;

    private static MediaColor ToMediaColor(DrawingColor value) =>
        MediaColor.FromArgb(value.A, value.R, value.G, value.B);

    private sealed record StyleChoice(OcctLineStyle Value, string Label)
    {
        public override string ToString() => Label;
    }
}
