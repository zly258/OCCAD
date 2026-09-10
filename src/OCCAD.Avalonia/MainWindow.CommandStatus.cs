using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

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

        // The command surface is the single owner of active tool prompts and
        // command history/result feedback. Keep the status bar focused on
        // persistent drafting state instead of repeating the same message.
        if (_toolStatus.Parent is Panel toolParent)
            toolParent.Children.Remove(_toolStatus);
        if (_historyStatus.Parent is Panel historyParent)
            historyParent.Children.Remove(_historyStatus);

        MoveDraftingTogglesToStatusBar(statusPanel);
        RefreshCommandStatusLanguage();

        CadLanguageManager.Changed += CommandStatusLanguageChanged;
        Closed += CommandStatusClosed;
    }

    private void MoveDraftingTogglesToStatusBar(DockPanel statusPanel)
    {
        if (_snapToggle.Parent is Panel snapParent)
            snapParent.Children.Remove(_snapToggle);
        if (_orthoToggle.Parent is Panel orthoParent)
            orthoParent.Children.Remove(_orthoToggle);
        if (_polarToggle.Parent is Panel polarParent)
            polarParent.Children.Remove(_polarToggle);

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

        // Insert near the right-side drafting/coordinate indicators. DockPanel
        // right docking keeps the central status region available for selection.
        DockPanel.SetDock(panel, Dock.Right);
        var coordinateIndex = statusPanel.Children.IndexOf(_coordinateStatus);
        statusPanel.Children.Insert(Math.Max(0, coordinateIndex), panel);
    }

    private static void ConfigureStatusDraftingToggle(
        global::Avalonia.Controls.Primitives.ToggleButton button)
    {
        button.MinWidth = 42;
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
    }

    private void CommandStatusLanguageChanged(object? sender, EventArgs e) =>
        Ui(RefreshCommandStatusLanguage);

    private void CommandStatusClosed(object? sender, EventArgs e)
    {
        CadLanguageManager.Changed -= CommandStatusLanguageChanged;
        Closed -= CommandStatusClosed;
    }
}
