using Avalonia.Controls;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _floatingToolPanelApplied;
    private CadFloatingToolPanel? _floatingToolPanel;

    private bool CanShowToolParameterPanel =>
        _floatingToolPanel?.CanDisplayCurrentTool == true;

    private bool IsToolParameterPanelVisible =>
        _floatingToolPanel?.IsPanelVisible == true;

    internal void ApplyFloatingToolPanel()
    {
        if (_floatingToolPanelApplied)
            return;

        _floatingToolPanelApplied = true;

        // Keep the old presenter detached. The floating presenter is now the
        // single visible tool-parameter surface and follows ActiveTool directly,
        // including selection and confirmation stages that the legacy panel did
        // not represent.
        if (_toolPanel.Parent is Panel legacyParent)
            legacyParent.Children.Remove(_toolPanel);

        _floatingToolPanel = new CadFloatingToolPanel(_workspace);
        _floatingToolPanel.UserVisibilityRequested +=
            FloatingToolPanelVisibilityRequested;
        _floatingToolPanel.PanelVisibilityChanged +=
            FloatingToolPanelVisibilityChanged;
        Closed += FloatingToolPanelClosed;

        _viewportHost.Children.Add(_floatingToolPanel);
        RefreshPanelMenuState();

        CadDiagnostics.Trace("Floating tool parameter panel applied.");
    }

    private void SetToolParameterPanelVisible(bool visible)
    {
        if (_floatingToolPanel is null || !CanShowToolParameterPanel)
        {
            RefreshPanelMenuState();
            return;
        }

        if (visible)
            _floatingToolPanel.ShowPanel();
        else
            _floatingToolPanel.HidePanel();

        RefreshPanelMenuState();
    }

    private void FloatingToolPanelVisibilityRequested(bool visible) =>
        RefreshPanelMenuState();

    private void FloatingToolPanelVisibilityChanged(object? sender, EventArgs e) =>
        RefreshPanelMenuState();

    private void FloatingToolPanelClosed(object? sender, EventArgs e)
    {
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
