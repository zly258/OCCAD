using Avalonia.Media;
using DrawingColor = System.Drawing.Color;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private readonly CadApplicationSettings _applicationSettings =
        CadApplicationSettings.Load();

    private void ApplyApplicationSettingsToCore()
    {
        _applicationSettings.Validate();

        _workspace.Grips.MarkerSize = _applicationSettings.GripSize;
        _workspace.Grips.PixelTolerance = _applicationSettings.GripTolerance;

        _workspace.Snap.MarkerSize = _applicationSettings.SnapMarkerSize;
        _workspace.Snap.MarkerColor = _applicationSettings.SnapMarkerColorValue;
        _workspace.Snap.PixelTolerance = _applicationSettings.SnapTolerance;
        _workspace.Snap.Modes = (OCCAD.CadSnapType)_applicationSettings.SnapModes;
        _workspace.Snap.Enabled = _applicationSettings.SnapEnabled;

        _workspace.Drafting.PolarIncrementDegrees =
            _applicationSettings.PolarIncrementDegrees;

        _workspace.Drafting.OrthogonalTrackingEnabled = false;
        _workspace.Drafting.PolarTrackingEnabled = false;
        if (_applicationSettings.OrthogonalTrackingEnabled)
            _workspace.Drafting.OrthogonalTrackingEnabled = true;
        else if (_applicationSettings.PolarTrackingEnabled)
            _workspace.Drafting.PolarTrackingEnabled = true;

        if (_applicationSettings.WorkPlanePreset != OCCAD.CadWorkPlanePreset.Custom)
            _workspace.WorkPlane.SetPreset(_applicationSettings.WorkPlanePreset);

        _viewport.ZoomSensitivity = _applicationSettings.ZoomSensitivity;
    }

    private void ApplyApplicationSettings()
    {
        ApplyApplicationSettingsToCore();

        var background = _applicationSettings.SceneBackgroundColor;
        _viewportHost.Background = new SolidColorBrush(ToMediaColor(background));

        if (_workspace.Engine is { IsInitialized: true } engine)
            ApplyEngineSettings(engine, applyExistingPrecision: true);

        RefreshInteractionUi();
        _viewport.Focus();
    }

    private void ApplyEngineSettings(
        OcctNet.OcctEngine engine,
        bool applyExistingPrecision)
    {
        var background = _applicationSettings.SceneBackgroundColor;

        using var batch = engine.BeginDisplayBatch();
        engine.SetGradientBackground(background, background);
        _workspace.Selection.PixelTolerance = _applicationSettings.SelectionTolerance;
        engine.SetDisplayPrecision(
            _applicationSettings.DisplayDeviationCoefficient,
            _applicationSettings.DisplayDeviationAngleDegrees,
            applyExistingPrecision);
    }

    private async Task ShowApplicationSettingsAsync()
    {
        var next = await CadSettingsDialog.ShowAsync(this, _applicationSettings);
        if (next is null)
            return;

        next.Validate();

        _applicationSettings.SceneBackground = next.SceneBackground;
        _applicationSettings.GripSize = next.GripSize;
        _applicationSettings.GripTolerance = next.GripTolerance;
        _applicationSettings.SnapMarkerSize = next.SnapMarkerSize;
        _applicationSettings.SnapMarkerColor = next.SnapMarkerColor;
        _applicationSettings.SnapTolerance = next.SnapTolerance;
        _applicationSettings.ZoomSensitivity = next.ZoomSensitivity;
        _applicationSettings.SelectionTolerance = next.SelectionTolerance;
        _applicationSettings.DisplayDeviationCoefficient = next.DisplayDeviationCoefficient;
        _applicationSettings.DisplayDeviationAngleDegrees = next.DisplayDeviationAngleDegrees;

        CaptureInteractionPreferences();
        ApplyApplicationSettings();

        try
        {
            _applicationSettings.Save();
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException and
                  not StackOverflowException and
                  not AccessViolationException)
        {
            CadDiagnostics.Report(exception, "Save application settings");
            await CadMessageDialog.ShowAsync(
                this,
                CadLanguageManager.Text("Cad.Text.ErrorTitle", "OCCAD Error"),
                exception.GetBaseException().Message,
                kind: CadMessageDialogKind.Error);
        }
    }

    private void CaptureInteractionPreferences()
    {
        _applicationSettings.Language = CadLanguageManager.CurrentLanguage;
        _applicationSettings.SnapEnabled = _workspace.Snap.Enabled;
        _applicationSettings.SnapModes = (int)_workspace.Snap.Modes;
        _applicationSettings.OrthogonalTrackingEnabled =
            _workspace.Drafting.OrthogonalTrackingEnabled;
        _applicationSettings.PolarTrackingEnabled =
            _workspace.Drafting.PolarTrackingEnabled;
        _applicationSettings.PolarIncrementDegrees =
            _workspace.Drafting.PolarIncrementDegrees;

        var preset = _workspace.WorkPlane.Preset;
        _applicationSettings.WorkPlanePreset =
            preset == OCCAD.CadWorkPlanePreset.Custom
                ? OCCAD.CadWorkPlanePreset.XY
                : preset;
    }

    private void SaveInteractionPreferences()
    {
        CaptureInteractionPreferences();
        try
        {
            _applicationSettings.Save();
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException and
                  not StackOverflowException and
                  not AccessViolationException)
        {
            CadDiagnostics.Report(exception, "Save interaction settings");
        }
    }

    private static Color ToMediaColor(DrawingColor value) =>
        Color.FromArgb(value.A, value.R, value.G, value.B);
}
