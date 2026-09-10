using System.Globalization;
using Avalonia.Controls;
using OcctNet;

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

        // LanguageChanged rebuilds the top shell. Detach the shared parameter
        // surface before that rebuild and restore its single bottom owner after it.
        CadLanguageManager.Changed -= LanguageChanged;
        CadLanguageManager.Changed += ClassicLanguageChanging;
        CadLanguageManager.Changed += LanguageChanged;
        CadLanguageManager.Changed += ClassicLanguageChanged;

        // ToolChanged/ToolUpdated UI refresh already belongs to the main shell
        // and CadWorkspaceEvents projection. Stability only owns the additional
        // coordinate-driven live-value synchronization; subscribing here as well
        // rebuilt the same parameter surface multiple times per stage change.
        _viewportInteraction.CoordinateChanged += StableCoordinateChanged;
        _viewport.PreviewKeyInput += StablePreviewKeyInput;

        MoveClassicToolOptionsToBottom();
        ApplyLivePreviewParameterValues(_workspace.Tools.ActiveTool);

        Closed += (_, _) =>
        {
            CadLanguageManager.Changed -= ClassicLanguageChanging;
            CadLanguageManager.Changed -= ClassicLanguageChanged;
            _viewportInteraction.CoordinateChanged -= StableCoordinateChanged;
            _viewport.PreviewKeyInput -= StablePreviewKeyInput;
        };
    }

    private void ClassicLanguageChanging(object? sender, EventArgs e) =>
        DetachClassicToolOptionsSurface();

    private void ClassicLanguageChanged(object? sender, EventArgs e)
    {
        MoveClassicToolOptionsToBottom();
        ApplyLivePreviewParameterValues(_workspace.Tools.ActiveTool);
    }

    private void StableCoordinateChanged(object? sender, CadCoordinateChangedEventArgs e)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null || tool.ParameterPanel?.Parameters.Count is not > 0)
            return;

        var now = DateTime.UtcNow;
        if ((now - _lastClassicParameterRefreshUtc).TotalMilliseconds < 120.0)
            return;

        _lastClassicParameterRefreshUtc = now;

        // Do not replace an editor while the user is typing or choosing a value.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox or ComboBox or CheckBox)
            return;

        RefreshClassicToolOptions();
        ApplyLivePreviewParameterValues(tool);
    }

    private void ApplyLivePreviewParameterValues(CadTool? tool)
    {
        if (tool?.ParameterPanel is not { } panel ||
            _workspace.Preview.Entity is not { } preview)
            return;

        for (var index = 0; index < panel.Parameters.Count; index++)
        {
            // RefreshClassicToolOptions lays out title, then label/editor pairs.
            var editorIndex = 2 + index * 2;
            if (editorIndex >= _classicToolOptions.Children.Count ||
                _classicToolOptions.Children[editorIndex] is not TextBox editor ||
                editor.IsFocused)
                continue;

            if (TryGetLivePreviewValue(panel.Parameters[index].Id, preview, out var value))
                editor.Text = value.ToString("0.###", CultureInfo.CurrentCulture);
        }
    }

    private static bool TryGetLivePreviewValue(
        string parameterId,
        CadEntity preview,
        out double value)
    {
        var id = parameterId.Trim().ToUpperInvariant();
        switch (preview)
        {
            case CadCircleEntity circle when id == "RADIUS":
                value = circle.Radius;
                return true;
            case CadCircleEntity circle when id == "DIAMETER":
                value = circle.Diameter;
                return true;
            case CadRectangleEntity rectangle when id == "WIDTH":
                value = rectangle.Width;
                return true;
            case CadRectangleEntity rectangle when id == "HEIGHT":
                value = rectangle.Height;
                return true;
            case CadEllipseEntity ellipse when id == "MAJORRADIUS":
                value = ellipse.MajorRadius;
                return true;
            case CadEllipseEntity ellipse when id == "MINORRADIUS":
                value = ellipse.MinorRadius;
                return true;
            case CadBoxEntity box when id == "LENGTH":
                value = box.Length;
                return true;
            case CadBoxEntity box when id == "WIDTH":
                value = box.Width;
                return true;
            case CadBoxEntity box when id == "HEIGHT":
                value = box.Height;
                return true;
            case CadCylinderEntity cylinder when id == "RADIUS":
                value = cylinder.Radius;
                return true;
            case CadCylinderEntity cylinder when id == "HEIGHT":
                value = cylinder.Height;
                return true;
            default:
                value = 0.0;
                return false;
        }
    }

    private void StablePreviewKeyInput(object? sender, OcctKeyInputEventArgs input)
    {
        if (input.Kind != OcctKeyInputKind.Pressed ||
            input.IsRepeat ||
            input.Key != OcctKey.Escape)
            return;

        ClearCadCommandAndSelection();
        input.Handled = true;
    }

    private void ClearCadCommandAndSelection()
    {
        List<Exception> failures = [];

        if (_workspace.Tools.ActiveTool is not null)
            Cleanup(() => _workspace.Tools.CancelCurrent());

        // Escape is the application-level reset gesture. One native cleanup
        // failure must not prevent the remaining interaction channels from being
        // cleared, otherwise selection/snap/tracking state can survive a canceled
        // command and make the next Tool appear corrupted.
        Cleanup(_workspace.Selection.Clear);
        Cleanup(_workspace.Subobjects.Clear);
        Cleanup(_workspace.Preselection.Clear);
        Cleanup(_workspace.Snap.Clear);
        Cleanup(_workspace.Tracking.Clear);
        Cleanup(_workspace.Grips.ClearHot);

        // CadTransientScene deliberately retains a Tool owner while native state
        // survives cleanup. After the explicit channel pass, retry the neutral
        // manager path once so a recovered owner is released before the next Tool.
        if (_workspace.Tools.ActiveTool is null &&
            _workspace.Transients.CurrentToolOwner != 0)
        {
            Cleanup(() => _workspace.Tools.CancelCurrent());
        }

        if (failures.Count == 0)
        {
            ShowStatusFeedback(null);
        }
        else
        {
            var failure = failures.Count == 1
                ? failures[0]
                : new AggregateException("Command cancel cleanup had multiple failures.", failures);
            CadDiagnostics.Report(failure, "Cancel command");
            ShowStatusFeedback(failure.GetBaseException().Message);
        }

        RefreshActionUi();
        RefreshOperationStatus();
        _viewport.Focus();
        return;

        void Cleanup(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
                when (exception is not OutOfMemoryException and
                      not StackOverflowException and
                      not AccessViolationException)
            {
                failures.Add(exception);
            }
        }
    }

    private void MoveClassicToolOptionsToBottom()
    {
        if (Content is not DockPanel root)
            return;

        DetachClassicToolOptionsSurface();

        // Keep one stable row immediately above the status bar. Its height does
        // not change when a Tool gains or loses parameters, so the viewport never
        // jumps vertically during command activation/cancelation.
        _classicToolOptionsSurface.MinHeight = 34;
        _classicToolOptionsSurface.Height = 34;
        _classicToolOptionsSurface.IsVisible = true;
        DockPanel.SetDock(_classicToolOptionsSurface, Dock.Bottom);

        var index = Math.Max(0, root.Children.Count - 1);
        root.Children.Insert(index, _classicToolOptionsSurface);
    }

    private void DetachClassicToolOptionsSurface()
    {
        if (_classicToolOptionsSurface.Parent is Panel panel)
            panel.Children.Remove(_classicToolOptionsSurface);
    }
}
