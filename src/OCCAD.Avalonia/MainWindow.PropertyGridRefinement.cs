using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private const string PropertyRowClass = "cad-property-row";
    private const string PropertyLabelCellClass = "cad-property-label-cell";
    private bool _propertyGridRefinementApplied;

    internal void ApplyPropertyGridRefinement()
    {
        if (_propertyGridRefinementApplied)
            return;

        _propertyGridRefinementApplied = true;
        _propertyPanelBorder.BorderBrush = CadTheme.BorderStrong;
        _propertyPanelBorder.BorderThickness = new Thickness(0, 0, 0, 0);
        _propertyHost.Spacing = 0;
        _propertyHost.Margin = new Thickness(0);

        if (_propertyHost.Parent is ScrollViewer scroll)
        {
            scroll.Background = CadTheme.Surface;
            scroll.Padding = new Thickness(0);
        }

        _propertyHost.LayoutUpdated += PropertyHostLayoutUpdated;
        Closed += PropertyGridRefinementClosed;
        Dispatcher.Post(
            RefinePropertyGridVisuals,
            DispatcherPriority.Loaded);
    }

    private void PropertyHostLayoutUpdated(object? sender, EventArgs e) =>
        RefinePropertyGridVisuals();

    private void PropertyGridRefinementClosed(object? sender, EventArgs e)
    {
        _propertyHost.LayoutUpdated -= PropertyHostLayoutUpdated;
        Closed -= PropertyGridRefinementClosed;
    }

    private void RefinePropertyGridVisuals()
    {
        if (!_propertyGridRefinementApplied)
            return;

        foreach (var section in _propertyHost.Children.OfType<StackPanel>())
        {
            foreach (var body in section.Children.OfType<StackPanel>())
            {
                foreach (var row in body.Children.OfType<Border>())
                    RefinePropertyRow(row);
            }
        }
    }

    private static void RefinePropertyRow(Border row)
    {
        if (row.Classes.Contains(PropertyRowClass))
            return;

        row.Classes.Add(PropertyRowClass);
        row.MinHeight = CadTheme.PropertyRowHeight;
        row.Padding = new Thickness(0);
        row.Margin = new Thickness(0);
        row.Background = CadTheme.Surface;
        row.BorderBrush = CadTheme.Border;
        row.BorderThickness = new Thickness(0, 0, 0, 1);

        if (row.Child is not Grid grid || grid.ColumnDefinitions.Count < 3)
            return;

        var labelCell = new Border
        {
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 0),
            IsHitTestVisible = false
        };
        labelCell.Classes.Add(PropertyLabelCellClass);
        Grid.SetColumn(labelCell, 0);
        grid.Children.Insert(0, labelCell);

        foreach (var control in grid.Children
                     .Where(control => Grid.GetColumn(control) == 2)
                     .ToArray())
        {
            RefinePropertyValueControl(control);
        }
    }

    private static void RefinePropertyValueControl(Control control)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.Margin = new Thickness(0);
                textBox.MinHeight = CadTheme.ControlHeight;
                textBox.Padding = new Thickness(5, 0);
                textBox.Background = CadTheme.Surface;
                textBox.BorderThickness = new Thickness(0);
                textBox.CornerRadius = new CornerRadius(0);
                break;

            case ComboBox comboBox:
                comboBox.Margin = new Thickness(0);
                comboBox.MinHeight = CadTheme.ControlHeight;
                comboBox.Background = CadTheme.Surface;
                comboBox.BorderThickness = new Thickness(0);
                comboBox.CornerRadius = new CornerRadius(0);
                break;

            case Button button:
                button.Margin = new Thickness(0);
                button.MinHeight = CadTheme.ControlHeight;
                button.BorderThickness = new Thickness(0);
                button.CornerRadius = new CornerRadius(0);
                break;

            case CheckBox checkBox:
                checkBox.Margin = new Thickness(5, 0, 0, 0);
                checkBox.MinHeight = CadTheme.ControlHeight;
                break;

            case TextBlock text:
                text.Margin = new Thickness(5, 0, 3, 0);
                text.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
                break;

            case Grid grid:
                grid.Margin = new Thickness(0);
                grid.ColumnSpacing = 4;
                foreach (var child in grid.Children.OfType<Control>())
                    RefinePropertyValueControl(child);
                break;

            case Panel panel:
                panel.Margin = new Thickness(0);
                foreach (var child in panel.Children.OfType<Control>())
                    RefinePropertyValueControl(child);
                break;
        }
    }
}
