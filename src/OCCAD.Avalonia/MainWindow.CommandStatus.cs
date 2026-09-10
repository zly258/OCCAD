using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _commandStatusRefinementApplied;
    private TextBlock? _draftingStatusLabel;

    internal void ApplyCommandStatusRefinement()
    {
        if (_commandStatusRefinementApplied)
            return;

        if (_coordinateStatus.Parent is not DockPanel statusPanel)
            return;

        _commandStatusRefinementApplied = true;

        // The command surface owns command prompts, command history/result
        // feedback and exact-input state. The status bar stays compact and
        // limited to persistent selection/drafting/work-plane/coordinate state.
        RemoveStatusText(_toolStatus);
        RemoveStatusText(_historyStatus);
        RemoveStatusText(_snapStatus);
        RemoveStatusText(_precisionStatus);

        MoveDraftingTogglesToStatusBar(statusPanel);
        RefreshCommandStatusLanguage();

        CadLanguageManager.Changed += CommandStatusLanguageChanged;
        Closed += CommandStatusClosed;
    }

    private static void RemoveStatusText(Control control)
    {
        if (control.Parent is Panel parent)
            parent.Children.Remove(control);
    }

    private void MoveDraftingTogglesToStatusBar(DockPanel statusPanel)
    {
        Control? leadingSeparator = null;
        if (_snapToggle.Parent is StackPanel toolbar)
        {
            var snapIndex = toolbar.Children.IndexOf(_snapToggle);
            if (snapIndex > 0)
                leadingSeparator = toolbar.Children[snapIndex - 1];
        }

        if (_snapToggle.Parent is Panel snapParent)
            snapParent.Children.Remove(_snapToggle);
        if (_orthoToggle.Parent is Panel orthoParent)
            orthoParent.Children.Remove(_orthoToggle);
        if (_polarToggle.Parent is Panel polarParent)
            polarParent.Children.Remove(_polarToggle);

        if (leadingSeparator?.Parent is Panel separatorParent &&
            IsToolbarSeparator(leadingSeparator))
        {
            separatorParent.Children.Remove(leadingSeparator);
        }

        ConfigureStatusDraftingToggle(_snapToggle);
        ConfigureStatusDraftingToggle(_orthoToggle);
        ConfigureStatusDraftingToggle(_polarToggle);

        _draftingStatusLabel = new TextBlock
        {
            Foreground = CadTheme.Muted,
            FontSize = CadTheme.CaptionFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 3, 0)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 5, 0)
        };
        panel.Children.Add(_draftingStatusLabel);
        panel.Children.Add(_snapToggle);
        panel.Children.Add(_orthoToggle);
        panel.Children.Add(_polarToggle);

        DockPanel.SetDock(panel, Dock.Right);
        var coordinateIndex = statusPanel.Children.IndexOf(_coordinateStatus);
        statusPanel.Children.Insert(
            coordinateIndex >= 0 ? coordinateIndex + 1 : 0,
            panel);
    }

    private static void ConfigureStatusDraftingToggle(
        global::Avalonia.Controls.Primitives.ToggleButton button)
    {
        button.MinWidth = 58;
        button.Height = 19;
        button.Padding = new Thickness(5, 0);
        button.Margin = new Thickness(0);
        button.FontSize = CadTheme.CaptionFontSize;
    }

    private void RefreshCommandStatusLanguage()
    {
        if (_draftingStatusLabel is not null)
        {
            _draftingStatusLabel.Text =
                CadLanguageManager.Text("Cad.Text.Drafting", "Drafting") + ":";
        }

        _snapToggle.ContextMenu = BuildSnapStatusMenu();
        _polarToggle.ContextMenu = BuildPolarStatusMenu();

        ToolTip.SetTip(
            _snapToggle,
            CadLanguageManager.Text("Cad.Text.ObjectSnap", "Object Snap"));
        ToolTip.SetTip(
            _polarToggle,
            CadLanguageManager.Text("Cad.Text.PolarIncrement", "Polar Increment"));
    }

    private ContextMenu BuildSnapStatusMenu()
    {
        var definitions = new[]
        {
            (CadSnapType.Endpoint, "Cad.Text.SnapEndpoint", "Endpoint"),
            (CadSnapType.Midpoint, "Cad.Text.SnapMidpoint", "Midpoint"),
            (CadSnapType.Center, "Cad.Text.SnapCenter", "Center"),
            (CadSnapType.Vertex, "Cad.Text.SnapVertex", "Vertex"),
            (CadSnapType.Intersection, "Cad.Text.SnapIntersection", "Intersection"),
            (CadSnapType.Perpendicular, "Cad.Text.SnapPerpendicular", "Perpendicular"),
            (CadSnapType.Nearest, "Cad.Text.SnapNearest", "Nearest"),
            (CadSnapType.Tangent, "Cad.Text.SnapTangent", "Tangent")
        };

        var items = definitions
            .Select(definition =>
            {
                var item = new MenuItem
                {
                    Header = CadLanguageManager.Text(definition.Item2, definition.Item3),
                    ToggleType = MenuItemToggleType.CheckBox,
                    IsChecked = (_workspace.Snap.Modes & definition.Item1) != 0
                };
                item.Click += (_, _) =>
                {
                    if (item.IsChecked)
                        _workspace.Snap.Modes |= definition.Item1;
                    else
                        _workspace.Snap.Modes &= ~definition.Item1;

                    _workspace.Snap.Clear();
                    RefreshInteractionUi();
                    SaveInteractionPreferences();
                };
                return (Mode: definition.Item1, Item: item);
            })
            .ToArray();

        var menu = new ContextMenu
        {
            ItemsSource = items.Select(static entry => entry.Item).ToArray()
        };
        menu.Opening += (_, _) =>
        {
            foreach (var entry in items)
            {
                entry.Item.IsChecked =
                    (_workspace.Snap.Modes & entry.Mode) != 0;
            }
        };
        return menu;
    }

    private ContextMenu BuildPolarStatusMenu()
    {
        var increments = new[] { 15.0, 30.0, 45.0, 90.0 };
        var items = increments
            .Select(increment =>
            {
                var item = new MenuItem
                {
                    Header = CadLanguageManager.Text(
                        $"Cad.Text.Polar{increment:0}",
                        $"{increment:0}°"),
                    ToggleType = MenuItemToggleType.Radio,
                    GroupName = "StatusPolarIncrement",
                    IsChecked = Math.Abs(
                        _workspace.Drafting.PolarIncrementDegrees - increment) <= 1e-12
                };
                item.Click += (_, _) =>
                {
                    _workspace.Drafting.PolarIncrementDegrees = increment;
                    _workspace.Tracking.Clear();
                    RefreshInteractionUi();
                    SaveInteractionPreferences();
                };
                return (Increment: increment, Item: item);
            })
            .ToArray();

        var menu = new ContextMenu
        {
            ItemsSource = items.Select(static entry => entry.Item).ToArray()
        };
        menu.Opening += (_, _) =>
        {
            foreach (var entry in items)
            {
                entry.Item.IsChecked = Math.Abs(
                    _workspace.Drafting.PolarIncrementDegrees - entry.Increment) <= 1e-12;
            }
        };
        return menu;
    }

    private void CommandStatusLanguageChanged(object? sender, EventArgs e) =>
        Ui(RefreshCommandStatusLanguage);

    private void CommandStatusClosed(object? sender, EventArgs e)
    {
        CadLanguageManager.Changed -= CommandStatusLanguageChanged;
        Closed -= CommandStatusClosed;
    }
}
