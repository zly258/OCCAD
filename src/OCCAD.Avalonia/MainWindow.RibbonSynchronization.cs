using Avalonia;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _ribbonSynchronizationApplied;

    internal void ApplyRibbonSynchronization()
    {
        if (_ribbonSynchronizationApplied || !_ribbonApplied)
            return;

        _ribbonSynchronizationApplied = true;

        _workspace.Actions.ActionFinished += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.Actions.ActionFailed += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.Tools.ToolChanged += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.Tools.ToolUpdated += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.Selection.Changed += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.Subobjects.Changed += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.History.Changed += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.Document.ChangeSetCommitted += (_, _) => Ui(RefreshRibbonActionUi);
        _workspace.Layers.Changed += (_, _) => Ui(RefreshRibbonActionUi);

        _modelPanel.PropertyChanged += (_, _) => Ui(RefreshRibbonPanelState);
        _layerPanelBorder.PropertyChanged += (_, _) => Ui(RefreshRibbonPanelState);
        _propertyPanelBorder.PropertyChanged += (_, _) => Ui(RefreshRibbonPanelState);
        if (_floatingToolPanel is not null)
        {
            _floatingToolPanel.PanelVisibilityChanged += (_, _) =>
                Ui(RefreshRibbonPanelState);
        }

        CadLanguageManager.Changed += RibbonLanguageChanged;
        Closed += RibbonSynchronizationClosed;

        RefreshRibbonActionUi();
        RefreshRibbonPanelState();
    }

    private void RibbonLanguageChanged(object? sender, EventArgs e) =>
        Ui(RefreshRibbonLanguage);

    private void RibbonSynchronizationClosed(object? sender, EventArgs e)
    {
        CadLanguageManager.Changed -= RibbonLanguageChanged;
        Closed -= RibbonSynchronizationClosed;
    }
}
