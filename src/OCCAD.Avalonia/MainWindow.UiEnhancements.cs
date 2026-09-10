using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, ToggleButton> _compactActionButtons =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _refreshingCompactToolbar;

    // Keep only the most-used camera directions on the toolbar. The complete
    // directional view set remains available from the View menu.
    private static readonly string[] ViewActionIds =
    [
        "view.isometric",
        "view.top",
        "view.front",
        "view.right"
    ];

    private static readonly string[] DisplayActionIds =
    [
        "display.wireframe",
        "display.shaded"
    ];

    internal void ApplyCompactUiEnhancements()
    {
        ConfigureCompactToolbar();
        AppendCompactViewButtons();
        SetCompactActionState("view.isometric");
        SetCompactActionState("display.shaded");

        _workspace.Actions.ActionFinished += CompactActionFinished;
        Closing += (_, _) => SaveInteractionPreferences();
        Closed += CompactUiClosed;
    }

    private void CompactUiClosed(object? sender, EventArgs e)
    {
        _workspace.Actions.ActionFinished -= CompactActionFinished;
        Closed -= CompactUiClosed;
    }

    private bool IsChineseUi => string.Equals(
        CadLanguageManager.CurrentLanguage,
        "zh-CN",
        StringComparison.OrdinalIgnoreCase);

    private void ConfigureCompactToolbar()
    {
        _snapToggle.MinWidth = 38;
        _orthoToggle.MinWidth = 38;
        _polarToggle.MinWidth = 38;
        RefreshDraftingToggleToolTips();
    }

    private void RefreshDraftingToggleToolTips()
    {
        ToolTip.SetTip(
            _snapToggle,
            CadLanguageManager.Text("Cad.Text.ObjectSnap", "Object Snap"));
        ToolTip.SetTip(
            _orthoToggle,
            CadLanguageManager.Text("Cad.Text.Ortho", "Orthogonal"));
        ToolTip.SetTip(
            _polarToggle,
            CadLanguageManager.Text("Cad.Text.Polar", "Polar"));
    }

    private void AppendCompactViewButtons()
    {
        if (Content is not DockPanel root)
            return;

        var toolbar = root.Children
            .OfType<Border>()
            .Select(static border => border.Child)
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Children.Contains(_snapToggle));
        if (toolbar is null || _compactActionButtons.Count != 0)
            return;

        toolbar.Children.Add(ToolbarSeparator());

        AddActionToggle(toolbar, "IS", "轴测", "view.isometric", "Cad.Text.Isometric", "Isometric");
        AddActionToggle(toolbar, "TP", "俯视", "view.top", "Cad.Text.Top", "Top");
        AddActionToggle(toolbar, "FR", "前视", "view.front", "Cad.Text.Front", "Front");
        AddActionToggle(toolbar, "RT", "右视", "view.right", "Cad.Text.Right", "Right");

        toolbar.Children.Add(ToolbarSeparator());
        AddActionToggle(toolbar, "WF", "线框", "display.wireframe", "Cad.Text.Wireframe", "Wireframe");
        AddActionToggle(toolbar, "SD", "着色", "display.shaded", "Cad.Text.Shaded", "Shaded");
    }

    private void AddActionToggle(
        StackPanel toolbar,
        string englishShort,
        string chineseShort,
        string actionId,
        string resourceKey,
        string fallback)
    {
        if (_workspace.Actions.Find(actionId) is null)
            return;

        var button = new ToggleButton
        {
            Content = IsChineseUi ? chineseShort : englishShort,
            MinWidth = 38,
            Height = CadTheme.ControlHeight,
            Tag = new CompactButtonText(
                actionId,
                englishShort,
                chineseShort,
                resourceKey,
                fallback),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("cad-toggle");
        button.Click += (_, _) =>
        {
            if (_refreshingCompactToolbar)
                return;

            ExecuteAction(actionId);
            _viewport.Focus();
        };
        ToolTip.SetTip(button, CadLanguageManager.Text(resourceKey, fallback));
        toolbar.Children.Add(button);
        _compactActionButtons[actionId] = button;
    }

    private void CompactActionFinished(object? sender, CadActionEventArgs args)
    {
        var id = args.Action.Id;
        if (ViewActionIds.Contains(id, StringComparer.OrdinalIgnoreCase) ||
            DisplayActionIds.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            Ui(() => SetCompactActionState(id));
        }
    }

    private void SetCompactActionState(string actionId)
    {
        _refreshingCompactToolbar = true;
        try
        {
            var group = ViewActionIds.Contains(actionId, StringComparer.OrdinalIgnoreCase)
                ? ViewActionIds
                : DisplayActionIds.Contains(actionId, StringComparer.OrdinalIgnoreCase)
                    ? DisplayActionIds
                    : [];

            foreach (var id in group)
            {
                if (_compactActionButtons.TryGetValue(id, out var button))
                    button.IsChecked = string.Equals(id, actionId, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            _refreshingCompactToolbar = false;
        }
    }

    private void RefreshCompactToolbarLanguage()
    {
        RefreshDraftingToggleToolTips();

        foreach (var button in _compactActionButtons.Values)
        {
            if (button.Tag is not CompactButtonText text)
                continue;

            button.Content = IsChineseUi
                ? text.ChineseShort
                : text.EnglishShort;
            ToolTip.SetTip(
                button,
                CadLanguageManager.Text(text.ResourceKey, text.Fallback));
        }
    }

    private sealed record CompactButtonText(
        string ActionId,
        string EnglishShort,
        string ChineseShort,
        string ResourceKey,
        string Fallback);
}
