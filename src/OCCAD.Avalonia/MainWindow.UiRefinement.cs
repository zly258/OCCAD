using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _uiRefinementApplied;
    private TextBlock? _workPlaneUiLabel;

    internal void ApplyUiRefinement()
    {
        if (_uiRefinementApplied)
            return;

        _uiRefinementApplied = true;
        ApplyWindowLogo();
        RefreshRefinementLanguage();

        KeyDown += AdditionalShortcutKeyDown;

        _snapToggle.IsCheckedChanged += InteractionPreferenceChanged;
        _orthoToggle.IsCheckedChanged += InteractionPreferenceChanged;
        _polarToggle.IsCheckedChanged += InteractionPreferenceChanged;
        _planeXy.Click += PlanePreferenceChanged;
        _planeYz.Click += PlanePreferenceChanged;
        _planeXz.Click += PlanePreferenceChanged;
        Closed += RefinementClosed;
    }

    private void InteractionPreferenceChanged(object? sender, EventArgs e) =>
        SaveInteractionPreferences();

    private void PlanePreferenceChanged(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs e) =>
        SaveInteractionPreferences();

    private void RefinementClosed(object? sender, EventArgs e)
    {
        KeyDown -= AdditionalShortcutKeyDown;
        _snapToggle.IsCheckedChanged -= InteractionPreferenceChanged;
        _orthoToggle.IsCheckedChanged -= InteractionPreferenceChanged;
        _polarToggle.IsCheckedChanged -= InteractionPreferenceChanged;
        _planeXy.Click -= PlanePreferenceChanged;
        _planeYz.Click -= PlanePreferenceChanged;
        _planeXz.Click -= PlanePreferenceChanged;
        Closed -= RefinementClosed;
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

    private static void ConfigureStatusPlaneButton(ToggleButton button)
    {
        button.MinWidth = 30;
        button.Height = 19;
        button.Padding = new Thickness(5, 0);
        button.Margin = new Thickness(0);
    }

    private void RefreshRefinementLanguage() =>
        RefreshWorkPlaneUi();

    private void AdditionalShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        var focused = TopLevel.GetTopLevel(this)?
            .FocusManager?
            .GetFocusedElement();
        if (focused is TextBox)
            return;

        // F3/F8/F10 belong exclusively to CadViewportInteractionController.
        if (e.Key == Key.Home)
        {
            ExecuteAction("view.fit");
            e.Handled = true;
            return;
        }

        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key == Key.D1)
        {
            SetPropertyPanelVisible(!_propertyPanelBorder.IsVisible);
            e.Handled = true;
        }
    }
}
