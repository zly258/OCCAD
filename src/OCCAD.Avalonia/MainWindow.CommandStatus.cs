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

        _commandStatusRefinementApplied = true;

        // The status bar is built in its final shape by MainWindow. This pass
        // only attaches localized drafting menus/tooltips and language updates.
        RefreshCommandStatusLanguage();

        CadLanguageManager.Changed += CommandStatusLanguageChanged;
        Closed += CommandStatusClosed;
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
            _orthoToggle,
            CadLanguageManager.Text("Cad.Text.Ortho", "Ortho"));
        ToolTip.SetTip(
            _polarToggle,
            $"{CadLanguageManager.Text("Cad.Text.PolarIncrement", "Polar Increment")}: " +
            $"{_workspace.Drafting.PolarIncrementDegrees:0}°");

        RefreshSnapStatus();
    }

    private ContextMenu BuildSnapStatusMenu()
    {
        var definitions = new[]
        {
            (CadSnapType.Endpoint, "Cad.Text.SnapEndpoint", "Endpoint"),
            (CadSnapType.Midpoint, "Cad.Text.SnapMidpoint", "Midpoint"),
            (CadSnapType.Center, "Cad.Text.SnapCenter", "Center"),
            (CadSnapType.Quadrant, "Cad.Text.SnapQuadrant", "Quadrant"),
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
