using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OCCAD;
using OcctNet;
using Binding = System.Windows.Data.Binding;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Control = System.Windows.Controls.Control;
using DataGridCell = System.Windows.Controls.DataGridCell;
using WinForms = System.Windows.Forms;

namespace OCCAD.Wpf;

public partial class MainWindow
{
    private static readonly double[] LayerLineWidths =
    [
        0.25,
        0.35,
        0.50,
        0.70,
        1.00,
        1.40,
        2.00,
        3.00
    ];

    private bool _layerGridConfigured;
    private bool _updatingLayerCell;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Viewport.EngineRecreated += (_, args) => args.Engine.SetViewCubeVisible(false);
        Dispatcher.InvokeAsync(ConfigureLayerGrid, DispatcherPriority.ContextIdle);
        CadLanguageManager.Changed += (_, _) =>
            Dispatcher.InvokeAsync(RefreshLayerGridLocalization);
    }

    private void ConfigureLayerGrid()
    {
        if (_layerGridConfigured)
        {
            RefreshLayerGridLocalization();
            return;
        }

        _layerGridConfigured = true;
        LayerList.Columns.Clear();
        LayerList.SelectionUnit = DataGridSelectionUnit.FullRow;
        LayerList.SelectionMode = DataGridSelectionMode.Single;
        LayerList.HeadersVisibility = DataGridHeadersVisibility.Column;
        LayerList.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        LayerList.CanUserAddRows = false;
        LayerList.CanUserDeleteRows = false;
        LayerList.CanUserReorderColumns = false;
        LayerList.CanUserResizeRows = false;
        LayerList.RowHeight = 26;
        LayerList.ColumnHeaderHeight = 26;
        LayerList.PreviewMouseLeftButtonDown += LayerGridPreviewMouseLeftButtonDown;

        LayerList.Columns.Add(new DataGridTextColumn
        {
            SortMemberPath = nameof(CadLayer.Name),
            Binding = new Binding(nameof(CadLayer.Name)),
            IsReadOnly = true,
            MinWidth = 76,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        LayerList.Columns.Add(CreateColorColumn());
        LayerList.Columns.Add(CreateToggleColumn(nameof(CadLayer.Visible), LayerVisibleClick));
        LayerList.Columns.Add(CreateToggleColumn(nameof(CadLayer.Locked), LayerLockedClick));
        LayerList.Columns.Add(CreateLineWidthColumn());
        LayerList.Columns.Add(CreateLineStyleColumn());

        if (ActualWidth >= 1000.0 && RightDockColumn.ActualWidth < 350.0)
            RightDockColumn.Width = new GridLength(360.0);

        RefreshLayerGridLocalization();
        RefreshLayerUi();
    }

    private DataGridColumn CreateColorColumn()
    {
        var template = new DataTemplate();
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetValue(Control.MarginProperty, new Thickness(3, 2, 3, 2));
        button.SetValue(Control.PaddingProperty, new Thickness(0));
        button.SetBinding(FrameworkElement.TagProperty, new Binding());
        button.SetBinding(
            Control.BackgroundProperty,
            new Binding(nameof(CadLayer.Color))
            {
                Converter = DrawingColorBrushConverter.Instance
            });
        button.AddHandler(
            ButtonBase.ClickEvent,
            new RoutedEventHandler(LayerColorButtonClick));
        template.VisualTree = button;

        return new DataGridTemplateColumn
        {
            SortMemberPath = nameof(CadLayer.Color),
            Width = new DataGridLength(46),
            CellTemplate = template,
            IsReadOnly = true
        };
    }

    private static DataGridColumn CreateToggleColumn(
        string propertyName,
        RoutedEventHandler handler)
    {
        var template = new DataTemplate();
        var checkBox = new FrameworkElementFactory(typeof(CheckBox));
        checkBox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        checkBox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        checkBox.SetBinding(FrameworkElement.TagProperty, new Binding());
        checkBox.SetBinding(
            ToggleButton.IsCheckedProperty,
            new Binding(propertyName) { Mode = BindingMode.OneWay });
        checkBox.AddHandler(ButtonBase.ClickEvent, handler);
        template.VisualTree = checkBox;

        return new DataGridTemplateColumn
        {
            SortMemberPath = propertyName,
            Width = new DataGridLength(48),
            CellTemplate = template,
            IsReadOnly = true
        };
    }

    private DataGridColumn CreateLineWidthColumn()
    {
        var template = new DataTemplate();
        var combo = new FrameworkElementFactory(typeof(ComboBox));
        combo.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
        combo.SetValue(ItemsControl.ItemsSourceProperty, LayerLineWidths);
        combo.SetBinding(FrameworkElement.TagProperty, new Binding());
        combo.SetBinding(
            Selector.SelectedItemProperty,
            new Binding(nameof(CadLayer.LineWidth)) { Mode = BindingMode.OneWay });
        combo.AddHandler(
            Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler(LayerLineWidthChanged));
        template.VisualTree = combo;

        return new DataGridTemplateColumn
        {
            SortMemberPath = nameof(CadLayer.LineWidth),
            Width = new DataGridLength(68),
            CellTemplate = template,
            IsReadOnly = true
        };
    }

    private DataGridColumn CreateLineStyleColumn()
    {
        var itemTemplate = new DataTemplate();
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(
            TextBlock.TextProperty,
            new Binding { Converter = CadLineStyleDisplayConverter.Instance });
        itemTemplate.VisualTree = label;

        var template = new DataTemplate();
        var combo = new FrameworkElementFactory(typeof(ComboBox));
        combo.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
        combo.SetValue(ItemsControl.ItemsSourceProperty, Enum.GetValues<OcctLineStyle>());
        combo.SetValue(ItemsControl.ItemTemplateProperty, itemTemplate);
        combo.SetBinding(FrameworkElement.TagProperty, new Binding());
        combo.SetBinding(
            Selector.SelectedItemProperty,
            new Binding(nameof(CadLayer.LineStyle)) { Mode = BindingMode.OneWay });
        combo.AddHandler(
            Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler(LayerLineStyleChanged));
        template.VisualTree = combo;

        return new DataGridTemplateColumn
        {
            SortMemberPath = nameof(CadLayer.LineStyle),
            Width = new DataGridLength(84),
            CellTemplate = template,
            IsReadOnly = true
        };
    }

    private void LayerGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualAncestor<ComboBox>(e.OriginalSource as DependencyObject) is { } combo &&
            combo.Tag is CadLayer)
        {
            combo.Focus();
            combo.IsDropDownOpen = true;
            e.Handled = true;
            return;
        }

        if (FindVisualAncestor<CheckBox>(e.OriginalSource as DependencyObject) is
            { Tag: CadLayer layer } checkBox)
        {
            if (checkBox.IsEnabled)
            {
                if (GetColumnProperty(e.OriginalSource as DependencyObject) == nameof(CadLayer.Visible))
                    _workspace.SetLayerVisible(layer, !layer.Visible);
                else if (GetColumnProperty(e.OriginalSource as DependencyObject) == nameof(CadLayer.Locked))
                    _workspace.SetLayerLocked(layer, !layer.Locked);
            }
            e.Handled = true;
            return;
        }

        if (FindVisualAncestor<Button>(e.OriginalSource as DependencyObject) is
            { Tag: CadLayer } colorButton)
        {
            LayerColorButtonClick(colorButton, new RoutedEventArgs(ButtonBase.ClickEvent, colorButton));
            e.Handled = true;
        }
    }

    private void LayerColorButtonClick(object sender, RoutedEventArgs e)
    {
        if (_updatingLayerCell || sender is not Button { Tag: CadLayer layer })
            return;

        using var dialog = new WinForms.ColorDialog
        {
            Color = layer.Color,
            FullOpen = true,
            AnyColor = true
        };
        if (dialog.ShowDialog() != WinForms.DialogResult.OK || dialog.Color.ToArgb() == layer.Color.ToArgb())
            return;

        var selected = dialog.Color;
        ApplyLayerCellChange(layer, () => layer.Color = selected);
    }

    private static string? GetColumnProperty(DependencyObject? source)
    {
        var cell = FindVisualAncestor<DataGridCell>(source);
        return cell?.Column.SortMemberPath;
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void LayerVisibleClick(object sender, RoutedEventArgs e)
    {
        if (_updatingLayerCell || sender is not CheckBox { Tag: CadLayer layer } editor)
            return;
        _workspace.SetLayerVisible(layer, editor.IsChecked == true);
    }

    private void LayerLockedClick(object sender, RoutedEventArgs e)
    {
        if (_updatingLayerCell || sender is not CheckBox { Tag: CadLayer layer } editor)
            return;
        _workspace.SetLayerLocked(layer, editor.IsChecked == true);
    }

    private void LayerLineWidthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingLayerCell ||
            _refreshingLayerUi ||
            sender is not ComboBox { Tag: CadLayer layer, SelectedItem: double value } ||
            Math.Abs(layer.LineWidth - value) <= 1e-12)
            return;

        ApplyLayerCellChange(layer, () => layer.LineWidth = value);
    }

    private void LayerLineStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingLayerCell ||
            _refreshingLayerUi ||
            sender is not ComboBox { Tag: CadLayer layer, SelectedItem: OcctLineStyle value } ||
            layer.LineStyle == value)
            return;

        ApplyLayerCellChange(layer, () => layer.LineStyle = value);
    }

    private void ApplyLayerCellChange(CadLayer layer, Action change)
    {
        var before = _workspace.CaptureLayerState(layer);
        _updatingLayerCell = true;
        try
        {
            change();
            _workspace.RecordLayerStateChange(
                layer,
                before,
                "Layer Edit");
        }
        finally
        {
            _updatingLayerCell = false;
        }

        _propertyInspector.Refresh();
    }

    private void RefreshLayerGridLocalization()
    {
        if (!_layerGridConfigured || LayerList.Columns.Count < 6)
            return;

        LayerList.Columns[0].Header = CadLanguageManager.Text("Cad.Text.Name", "Name");
        LayerList.Columns[1].Header = CadLanguageManager.Text("Cad.Text.Color", "Color");
        LayerList.Columns[2].Header = CadLanguageManager.Text("Cad.Text.Visible", "Visible");
        LayerList.Columns[3].Header = CadLanguageManager.Text("Cad.Text.Locked", "Locked");
        LayerList.Columns[4].Header = CadLanguageManager.Text("Cad.Text.Width", "Width");
        LayerList.Columns[5].Header = CadLanguageManager.Text("Cad.Text.Style", "Style");
        LayerList.Items.Refresh();
    }
}
