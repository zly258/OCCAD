using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private void ExecuteAction(string id)
    {
        var action = _workspace.Actions.Find(id);
        if (action is null)
            return;

        if (!action.CanExecute())
        {
            _commandLine.ShowFeedback(
                UiFormat(
                    "Cad.Text.ActionUnavailable",
                    "{0} is not available in the current state.",
                    CadLanguageManager.Text(
                        $"Cad.Text.{action.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)}",
                        action.DisplayName)));
            RefreshActionUi();
            return;
        }

        _workspace.Actions.Execute(id);
        RefreshActionUi();
    }

    private void RefreshActionUi() =>
        RefreshRibbonActionUi();

    private void RefreshPanelMenuState() =>
        RefreshRibbonPanelState();

    private void MainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox)
            return;

        var modifiers = e.KeyModifiers;

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            if (e.Key == Key.N)
            {
                _ = NewDocumentAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                _ = OpenDocumentAsync();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                _ = SaveDocumentAsync(
                    saveAs: (modifiers & KeyModifiers.Shift) != 0);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D1)
            {
                SetPropertyPanelVisible(!_propertyPanelBorder.IsVisible);
                e.Handled = true;
                return;
            }
        }

        // F3/F8/F10 belong exclusively to CadViewportInteractionController.
        if (e.Key is Key.Enter or Key.Space &&
            _workspace.Tools.ActiveTool is null &&
            _workspace.Actions.ExecuteLast())
        {
            e.Handled = true;
            return;
        }

        // Active-tool Enter/Space/Escape/Backspace belong exclusively to the
        // viewport/tool route and are dispatched once by CadToolManager.
        if (_workspace.Tools.ActiveTool is not null)
            return;

        var shortcut = ShortcutText(e.Key, modifiers);
        if (shortcut is null)
            return;

        if (_workspace.Actions.ExecuteShortcut(shortcut))
            e.Handled = true;
    }

    private void ApplyWindowLogo()
    {
        try
        {
            using var stream = AssetLoader.Open(
                new Uri("avares://OCCAD/Assets/OCCAD.png"));
            Icon = new WindowIcon(stream);
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException and
                  not StackOverflowException and
                  not AccessViolationException)
        {
            CadDiagnostics.Report(exception, "Window logo");
        }
    }

    private static string? ShortcutText(
        Key key,
        KeyModifiers modifiers)
    {
        if (key == Key.None)
            return null;

        var parts = new List<string>(4);
        if ((modifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((modifiers & KeyModifiers.Meta) != 0) parts.Add("Meta");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
