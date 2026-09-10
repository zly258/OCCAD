using Avalonia.Controls;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _floatingToolPanelApplied;
    private CadFloatingToolPanel? _floatingToolPanel;

    internal void ApplyFloatingToolPanel()
    {
        if (_floatingToolPanelApplied)
            return;

        _floatingToolPanelApplied = true;

        // The legacy panel remains detached as a temporary compatibility
        // switch for existing menu/ribbon commands. The visible floating panel
        // owns its own ActiveTool lifetime and must not be hidden merely because
        // the legacy presenter cannot describe a selection/confirmation stage.
        if (_toolPanel.Parent is Panel legacyParent)
            legacyParent.Children.Remove(_toolPanel);

        _floatingToolPanel = new CadFloatingToolPanel(_workspace);
        _floatingToolPanel.UserVisibilityRequested +=
            FloatingToolPanelVisibilityRequested;
        _floatingToolPanel.PanelVisibilityChanged +=
            FloatingToolPanelVisibilityChanged;
        _toolPanel.PanelVisibilityChanged += LegacyToolPanelVisibilityChanged;
        Closed += FloatingToolPanelClosed;

        _viewportHost.Children.Add(_floatingToolPanel);

        CadDiagnostics.Trace("Floating tool parameter panel applied.");
    }

    private void FloatingToolPanelVisibilityRequested(bool visible)
    {
        // Preserve compatibility state only when the legacy presenter supports
        // this stage. The floating presenter has already applied the requested
        // visibility itself.
        if (!_toolPanel.CanDisplayCurrentTool)
        {
            RefreshRibbonState();
            return;
        }

        if (visible)
            _toolPanel.ShowPanel();
        else
            _toolPanel.HidePanel();
    }

    private void FloatingToolPanelVisibilityChanged(object? sender, EventArgs e) =>
        RefreshRibbonState();

    private void LegacyToolPanelVisibilityChanged(object? sender, EventArgs e)
    {
        if (_floatingToolPanel is null ||
            !_floatingToolPanel.CanDisplayCurrentTool ||
            !_toolPanel.CanDisplayCurrentTool)
        {
            return;
        }

        if (_toolPanel.IsPanelVisible)
            _floatingToolPanel.ShowPanel();
        else
            _floatingToolPanel.HidePanel();
    }

    private void FloatingToolPanelClosed(object? sender, EventArgs e)
    {
        _toolPanel.PanelVisibilityChanged -= LegacyToolPanelVisibilityChanged;
        if (_floatingToolPanel is not null)
        {
            _floatingToolPanel.UserVisibilityRequested -=
                FloatingToolPanelVisibilityRequested;
            _floatingToolPanel.PanelVisibilityChanged -=
                FloatingToolPanelVisibilityChanged;
            _floatingToolPanel.Dispose();
        }
        Closed -= FloatingToolPanelClosed;
    }
}
