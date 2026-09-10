using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _uiRefinementApplied;
    private TextBlock? _workPlaneUiLabel;
    private readonly List<Button> _quickAccessButtons = [];

    internal void ApplyUiRefinement()
    {
        if (_uiRefinementApplied)
            return;

        _uiRefinementApplied = true;
        ApplyWindowLogo();
        MoveWorkPlaneControlsToStatusBar();
        AddQuickAccessToolbar();
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

    private void PlanePreferenceChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
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

    private void MoveWorkPlaneControlsToStatusBar()
    {
        if (_planeXy.Parent is StackPanel toolbar)
        {
            var wpLabel = toolbar.Children
                .OfType<TextBlock>()
                .FirstOrDefault(item =>
                    string.Equals(item.Text, "WP", StringComparison.OrdinalIgnoreCase));
            if (wpLabel is not null)
                toolbar.Children.Remove(wpLabel);

            var planeEndIndex = toolbar.Children.IndexOf(_planeXz);
            var trailingSeparator =
                planeEndIndex >= 0 && planeEndIndex + 1 < toolbar.Children.Count
                    ? toolbar.Children[planeEndIndex + 1]
                    : null;

            toolbar.Children.Remove(_planeXy);
            toolbar.Children.Remove(_planeYz);
            toolbar.Children.Remove(_planeXz);
            if (IsToolbarSeparator(trailingSeparator))
                toolbar.Children.Remove(trailingSeparator!);
        }

        if (_workPlaneStatus.Parent is not DockPanel statusPanel)
            return;

        var index = statusPanel.Children.IndexOf(_workPlaneStatus);
        if (index < 0)
            return;

        statusPanel.Children.Remove(_workPlaneStatus);

        _workPlaneUiLabel = new TextBlock
        {
            Foreground = CadTheme.Muted,
            FontSize = CadTheme.CaptionFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 3, 0)
        };

        ConfigureStatusPlaneButton(_planeXy);
        ConfigureStatusPlaneButton(_planeYz);
        ConfigureStatusPlaneButton(_planeXz);

        var planePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0)
        };
        planePanel.Children.Add(_workPlaneUiLabel);
        planePanel.Children.Add(_planeXy);
        planePanel.Children.Add(_planeYz);
        planePanel.Children.Add(_planeXz);

        DockPanel.SetDock(planePanel, Dock.Right);
        statusPanel.Children.Insert(index, planePanel);
    }

    private static bool IsToolbarSeparator(Control? control) =>
        control is Border
        {
            Width: 1
        };

    private static void ConfigureStatusPlaneButton(ToggleButton button)
    {
        button.MinWidth = 30;
        button.Height = 19;
        button.Padding = new Thickness(5, 0);
        button.Margin = new Thickness(0);
    }

    private void AddQuickAccessToolbar()
    {
        if (_snapToggle.Parent is not StackPanel toolbar ||
            _quickAccessButtons.Count != 0)
            return;

        var controls = new Control[]
        {
            QuickButton("new", () => _ = NewDocumentAsync(), "Ctrl+N"),
            QuickButton("open", () => _ = OpenDocumentAsync(), "Ctrl+O"),
            QuickButton("save", () => _ = SaveDocumentAsync(saveAs: false), "Ctrl+S"),
            ToolbarSeparator(),
            QuickButton("undo", () => ExecuteAction("edit.undo"), "Ctrl+Z"),
            QuickButton("redo", () => ExecuteAction("edit.redo"), "Ctrl+Y"),
            ToolbarSeparator(),
            QuickButton("fit", () => ExecuteAction("view.fit"), "Home"),
            ToolbarSeparator()
        };

        for (var index = controls.Length - 1; index >= 0; index--)
            toolbar.Children.Insert(0, controls[index]);
    }

    private Button QuickButton(string key, Action action, string shortcut)
    {
        var button = new Button
        {
            Tag = key,
            Height = CadTheme.ControlHeight,
            MinWidth = 42,
            Padding = new Thickness(7, 0),
            Margin = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        button.Click += (_, _) => action();
        ToolTip.SetTip(button, shortcut);
        _quickAccessButtons.Add(button);
        return button;
    }

    private void RefreshRefinementLanguage()
    {
        if (_workPlaneUiLabel is not null)
            _workPlaneUiLabel.Text = UiText("Cad.Text.WorkPlane", "Work Plane") + ":";

        foreach (var button in _quickAccessButtons)
        {
            button.Content = button.Tag?.ToString() switch
            {
                "new" => UiText("Cad.Text.New", "New"),
                "open" => UiText("Cad.Text.Open", "Open"),
                "save" => UiText("Cad.Text.Save", "Save"),
                "undo" => UiText("Cad.Text.Undo", "Undo"),
                "redo" => UiText("Cad.Text.Redo", "Redo"),
                "fit" => UiText("Cad.Text.Fit", "Fit"),
                _ => button.Content
            };
        }
    }

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
