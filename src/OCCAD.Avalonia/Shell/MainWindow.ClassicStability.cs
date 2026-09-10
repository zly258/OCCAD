using Avalonia.Controls;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _classicStableLayoutApplied;
    private DateTime _lastClassicParameterRefreshUtc = DateTime.MinValue;

    internal void StabilizeClassicShell()
    {
        if (_classicStableLayoutApplied)
            return;

        _classicStableLayoutApplied = true;

        // LanguageChanged rebuilds the classic top host. Put a detach handler
        // before it and a re-dock handler after it so the same parameter surface
        // is never attached to two visual parents during a language switch.
        CadLanguageManager.Changed -= LanguageChanged;
        CadLanguageManager.Changed += ClassicLanguageChanging;
        CadLanguageManager.Changed += LanguageChanged;
        CadLanguageManager.Changed += ClassicLanguageChanged;

        _workspace.Tools.ToolChanged += StableToolChanged;
        _workspace.Tools.ToolUpdated += StableToolUpdated;
        _viewportInteraction.CoordinateChanged += StableCoordinateChanged;

        MoveClassicToolOptionsToBottom();

        Closed += (_, _) =>
        {
            CadLanguageManager.Changed -= ClassicLanguageChanging;
            CadLanguageManager.Changed -= ClassicLanguageChanged;
            _workspace.Tools.ToolChanged -= StableToolChanged;
            _workspace.Tools.ToolUpdated -= StableToolUpdated;
            _viewportInteraction.CoordinateChanged -= StableCoordinateChanged;
        };
    }

    private void ClassicLanguageChanging(object? sender, EventArgs e) =>
        DetachClassicToolOptionsSurface();

    private void ClassicLanguageChanged(object? sender, EventArgs e) =>
        MoveClassicToolOptionsToBottom();

    private void StableToolChanged(object? sender, CadToolChangedEventArgs e) =>
        MoveClassicToolOptionsToBottom();

    private void StableToolUpdated(object? sender, CadToolEventArgs e) =>
        MoveClassicToolOptionsToBottom();

    private void StableCoordinateChanged(object? sender, CadCoordinateChangedEventArgs e)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null || tool.ParameterPanel.Parameters.Count == 0)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastClassicParameterRefreshUtc).TotalMilliseconds < 120.0)
            return;

        _lastClassicParameterRefreshUtc = now;

        // Never replace an editor while the user is typing/selecting a value.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox or ComboBox or CheckBox)
            return;

        RefreshClassicToolOptions();
        MoveClassicToolOptionsToBottom();
    }

    private void MoveClassicToolOptionsToBottom()
    {
        if (Content is not DockPanel root)
            return;

        DetachClassicToolOptionsSurface();

        // Reserve one stable row above the status bar. The row never collapses,
        // so activating/canceling a parameterized Tool does not move the viewport.
        _classicToolOptionsSurface.MinHeight = 34;
        _classicToolOptionsSurface.Height = 34;
        _classicToolOptionsSurface.IsVisible = true;
        DockPanel.SetDock(_classicToolOptionsSurface, Dock.Bottom);

        // BuildShell keeps the workspace as the final LastChildFill child.
        // Insert the parameter row immediately before it.
        var index = Math.Max(0, root.Children.Count - 1);
        root.Children.Insert(index, _classicToolOptionsSurface);
    }

    private void DetachClassicToolOptionsSurface()
    {
        if (_classicToolOptionsSurface.Parent is Panel panel)
            panel.Children.Remove(_classicToolOptionsSurface);
    }
}
