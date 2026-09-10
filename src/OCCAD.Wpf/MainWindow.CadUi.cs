using System.Windows.Input;
using OCCAD;

namespace OCCAD.Wpf;

public partial class MainWindow
{
    private bool _cadUiInitialized;
    private CadParameterToolPanel? _toolPanel;
    private CadCommandLineController? _commandLine;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_cadUiInitialized) return;

        _cadUiInitialized = true;
        PruneUnregisteredActionMenuItems(MainMenu.Items);
        RefineLayerManagerLayout();
        RefineCadUiLayout();

        _toolPanel = new CadParameterToolPanel(_workspace);
        ViewportHost.Children.Add(_toolPanel);
        _commandLine = new CadCommandLineController(this, _workspace);
        CadLanguageManager.Changed += CadLanguageChanged;

        _toolPanel.SetTool(_workspace.Tools.ActiveTool);
        RefreshWorkPlaneStatusUi();
        RefreshExtendedLocalization();
    }

    private void PruneUnregisteredActionMenuItems(System.Windows.Controls.ItemCollection items)
    {
        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (items[index] is not System.Windows.Controls.MenuItem item)
                continue;

            if (item.Items.Count > 0)
                PruneUnregisteredActionMenuItems(item.Items);

            if (item.Tag is not string id || !IsActionMenuTag(id))
                continue;

            if (_workspace.Actions.Find(id) is null)
                items.RemoveAt(index);
        }
    }

    private static bool IsActionMenuTag(string id) =>
        string.Equals(id, "select", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("select.", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("edit.", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("view.", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("display.", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("modify.", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("draw.", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("solid.", StringComparison.OrdinalIgnoreCase);

    private void RefineLayerManagerLayout()
    {
        LayerList.RowHeight = 24;
        LayerList.ColumnHeaderHeight = 25;
        LayerList.CanUserReorderColumns = false;
        LayerList.CanUserResizeRows = false;
    }

    private void WorkPlaneStatusClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string value } ||
            !Enum.TryParse<CadWorkPlanePreset>(value, ignoreCase: true, out var preset))
            return;

        SetDrawingPlane(preset);
    }

    private void SetDrawingPlane(CadWorkPlanePreset preset)
    {
        if (!_workspace.Tools.TryChangeDrawingPlane(preset))
        {
            ToolStatus.Text = CadLanguageManager.Text(
                "Cad.Text.WorkPlaneChangeBlocked",
                "Finish the active tool before changing the work plane.");
            System.Media.SystemSounds.Beep.Play();
            RefreshWorkPlaneStatusUi();
            return;
        }

        ToolStatus.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            CadLanguageManager.Text("Cad.Text.WorkPlaneChanged", "Work plane: {0}"),
            preset);
        _workspace.Snap.Clear();
    }

    private void RefreshWorkPlaneStatusUi()
    {
        var preset = _workspace.WorkPlane.Preset;
        var enabled = _workspace.Tools.CanChangeDrawingPlane;
        WorkPlaneStatusLabel.Text = CadLanguageManager.Text("Cad.Text.WorkPlane", "Work Plane");
        WorkPlaneXyToggle.IsChecked = preset == CadWorkPlanePreset.XY;
        WorkPlaneYzToggle.IsChecked = preset == CadWorkPlanePreset.YZ;
        WorkPlaneXzToggle.IsChecked = preset == CadWorkPlanePreset.XZ;
        WorkPlaneXyToggle.IsEnabled = enabled;
        WorkPlaneYzToggle.IsEnabled = enabled;
        WorkPlaneXzToggle.IsEnabled = enabled;
    }

    private void CadLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(RefreshExtendedLocalization);

    private void RefreshExtendedLocalization()
    {
        SnapToggle.Content = CadLanguageManager.Text("Cad.Text.StatusSnap", "SNAP");
        OrthoTrackingToggle.Content = CadLanguageManager.Text("Cad.Text.StatusOrtho", "ORTHO");
        PolarTrackingToggle.Content = CadLanguageManager.Text("Cad.Text.StatusPolar", "POLAR");
        RefreshWorkPlaneStatusUi();
        RefreshPrecisionUi();
        _toolPanel?.RefreshLanguage();
        _commandLine?.RefreshLanguage();
        _propertyInspector.Refresh();
    }
}
