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

        // Keep the legacy panel as the compatibility state owner while the
        // new presenter becomes the visible interaction surface. This allows
        // existing menu/ribbon visibility commands to continue working during
        // migration without rendering two parameter panels.
        if (_toolPanel.Parent is Panel legacyParent)
            legacyParent.Children.Remove(_toolPanel);

        _floatingToolPanel = new CadFloatingToolPanel(_workspace);
        _floatingToolPanel.UserVisibilityRequested +=
            FloatingToolPanelVisibilityRequested;
        _toolPanel.PanelVisibilityChanged += LegacyToolPanelVisibilityChanged;
        Closed += FloatingToolPanelClosed;

        _viewportHost.Children.Add(_floatingToolPanel);
        SynchronizeFloatingToolPanelVisibility();

        CadDiagnostics.Trace("Floating tool parameter panel applied.");
    }

    private void FloatingToolPanelVisibilityRequested(bool visible)
    {
        if (visible)
            _toolPanel.ShowPanel();
        else
            _toolPanel.HidePanel();
    }

    private void LegacyToolPanelVisibilityChanged(object? sender, EventArgs e) =>
        SynchronizeFloatingToolPanelVisibility();

    private void SynchronizeFloatingToolPanelVisibility()
    {
        if (_floatingToolPanel is null)
            return;

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
            _floatingToolPanel.Dispose();
        }
        Closed -= FloatingToolPanelClosed;
    }
}
