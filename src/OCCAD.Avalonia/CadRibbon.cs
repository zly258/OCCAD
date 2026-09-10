using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace OCCAD.Avalonia;

internal sealed record CadRibbonItemDefinition(
    string Text,
    Action Execute,
    Func<bool>? CanExecute = null,
    Func<bool>? IsChecked = null,
    string? ToolTip = null);

internal sealed record CadRibbonGroupDefinition(
    string Caption,
    IReadOnlyList<CadRibbonItemDefinition> Items);

internal sealed record CadRibbonTabDefinition(
    string Header,
    IReadOnlyList<CadRibbonGroupDefinition> Groups);

/// <summary>
/// Compact text-first ribbon used by OCCAD. The control intentionally avoids
/// Office-style large icons: CAD actions are arranged in three-row columns so
/// the viewport keeps most of the available vertical space.
/// </summary>
internal sealed class CadRibbon : Border
{
    private readonly StackPanel _tabStrip = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 0,
        VerticalAlignment = VerticalAlignment.Bottom
    };

    private readonly StackPanel _groupStrip = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 0,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    private readonly List<ToggleButton> _tabButtons = [];
    private readonly List<ItemBinding> _itemBindings = [];
    private IReadOnlyList<CadRibbonTabDefinition> _tabs = [];

    public CadRibbon(
        IReadOnlyList<CadRibbonTabDefinition> tabs,
        int selectedIndex = 0)
    {
        Background = CadTheme.Toolbar;
        BorderBrush = CadTheme.Border;
        BorderThickness = new Thickness(0, 0, 0, 1);

        var tabHeader = new Border
        {
            Height = 27,
            Background = CadTheme.Toolbar,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = _tabStrip
        };

        var scroll = new ScrollViewer
        {
            Content = _groupStrip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var content = new Border
        {
            MinHeight = 62,
            MaxHeight = 70,
            Background = CadTheme.Panel,
            Child = scroll
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.Children.Add(tabHeader);
        Grid.SetRow(content, 1);
        root.Children.Add(content);
        Child = root;

        SetTabs(tabs, selectedIndex);
    }

    public int SelectedIndex { get; private set; }

    public void SetTabs(
        IReadOnlyList<CadRibbonTabDefinition> tabs,
        int selectedIndex)
    {
        _tabs = tabs;
        _tabStrip.Children.Clear();
        _tabButtons.Clear();

        for (var index = 0; index < _tabs.Count; index++)
        {
            var currentIndex = index;
            var button = new ToggleButton
            {
                Content = _tabs[index].Header,
                Height = 27,
                MinWidth = index == 0 ? 48 : 58,
                Padding = new Thickness(10, 0),
                Margin = new Thickness(0),
                FontSize = CadTheme.FontSize,
                FontWeight = FontWeight.SemiBold,
                Foreground = CadTheme.Text,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            button.Click += (_, _) => ShowTab(currentIndex);
            _tabButtons.Add(button);
            _tabStrip.Children.Add(button);
        }

        if (_tabs.Count == 0)
        {
            SelectedIndex = -1;
            _groupStrip.Children.Clear();
            _itemBindings.Clear();
            return;
        }

        ShowTab(Math.Clamp(selectedIndex, 0, _tabs.Count - 1));
    }

    public void RefreshState()
    {
        foreach (var binding in _itemBindings)
        {
            binding.Control.IsEnabled =
                binding.Definition.CanExecute?.Invoke() ?? true;

            if (binding.Control is ToggleButton toggle &&
                binding.Definition.IsChecked is not null)
            {
                toggle.IsChecked = binding.Definition.IsChecked();
            }
        }
    }

    private void ShowTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
            return;

        SelectedIndex = index;
        for (var tabIndex = 0; tabIndex < _tabButtons.Count; tabIndex++)
        {
            var selected = tabIndex == SelectedIndex;
            var button = _tabButtons[tabIndex];
            button.IsChecked = selected;
            button.Background = selected
                ? CadTheme.AccentSoft
                : Brushes.Transparent;
            button.BorderBrush = selected
                ? CadTheme.Accent
                : Brushes.Transparent;
            button.BorderThickness = selected
                ? new Thickness(0, 0, 0, 2)
                : new Thickness(0);
        }

        _groupStrip.Children.Clear();
        _itemBindings.Clear();
        foreach (var group in _tabs[index].Groups)
            _groupStrip.Children.Add(BuildGroup(group));

        RefreshState();
    }

    private Control BuildGroup(CadRibbonGroupDefinition definition)
    {
        var columns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(4, 2, 4, 0)
        };

        for (var index = 0; index < definition.Items.Count; index += 3)
        {
            var column = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 1
            };

            var end = Math.Min(index + 3, definition.Items.Count);
            for (var itemIndex = index; itemIndex < end; itemIndex++)
                column.Children.Add(BuildItem(definition.Items[itemIndex]));

            columns.Children.Add(column);
        }

        var caption = new TextBlock
        {
            Text = definition.Caption,
            Height = 16,
            Margin = new Thickness(4, 0),
            FontSize = CadTheme.CaptionFontSize,
            Foreground = CadTheme.Muted,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid
        {
            MinWidth = 76
        };
        grid.RowDefinitions.Add(
            new RowDefinition(new GridLength(1, GridUnitType.Star)));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.Children.Add(columns);
        Grid.SetRow(caption, 1);
        grid.Children.Add(caption);

        return new Border
        {
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = grid
        };
    }

    private Control BuildItem(CadRibbonItemDefinition definition)
    {
        Control control;
        if (definition.IsChecked is not null)
        {
            var toggle = new ToggleButton
            {
                Content = definition.Text,
                Height = 20,
                MinWidth = 64,
                Padding = new Thickness(7, 0),
                Margin = new Thickness(0),
                FontSize = CadTheme.SmallFontSize,
                Foreground = CadTheme.Text,
                Background = CadTheme.Surface,
                BorderBrush = CadTheme.Border,
                BorderThickness = new Thickness(1)
            };
            toggle.Click += (_, _) => Execute(definition);
            control = toggle;
        }
        else
        {
            var button = new Button
            {
                Content = definition.Text,
                Height = 20,
                MinWidth = 64,
                Padding = new Thickness(7, 0),
                Margin = new Thickness(0),
                FontSize = CadTheme.SmallFontSize,
                Foreground = CadTheme.Text,
                Background = CadTheme.Surface,
                BorderBrush = CadTheme.Border,
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Click += (_, _) => Execute(definition);
            control = button;
        }

        if (!string.IsNullOrWhiteSpace(definition.ToolTip))
            ToolTip.SetTip(control, definition.ToolTip);

        _itemBindings.Add(new ItemBinding(control, definition));
        return control;
    }

    private void Execute(CadRibbonItemDefinition definition)
    {
        if (definition.CanExecute?.Invoke() == false)
        {
            RefreshState();
            return;
        }

        definition.Execute();
        RefreshState();
    }

    private sealed record ItemBinding(
        Control Control,
        CadRibbonItemDefinition Definition);
}
