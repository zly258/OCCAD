using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OCCAD;
using OcctNet;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private CadModelPanelController? _modelPanelController;

    private CadModelPanelController ModelPanel =>
        _modelPanelController ??= new CadModelPanelController(
            _workspace,
            _modelSearch,
            _modelTree,
            entity =>
            {
                _propertyInspector.InspectEntities([entity]);
                SetPropertyPanelVisible(true);
            },
            message => _commandLine.ShowFeedback(message));

    private void RefreshAll()
    {
        ModelPanel.Refresh();
        RefreshLayerUi();
        UpdateToolUi(_workspace.Tools.ActiveTool);
        UpdateSelectionStatus();
        UpdateHistoryUi();
        RefreshInteractionUi();
        RefreshCommandStatusLanguage();
        RefreshActionUi();
        RefreshPanelMenuState();
    }

    private void RefreshLanguageUi()
    {
        if (_modelHeaderText is not null)
            _modelHeaderText.Text = UiText("Cad.Text.Model", "Model");
        if (_layerHeaderText is not null)
            _layerHeaderText.Text = UiText("Cad.Text.Layers", "Layers");
        if (_propertyHeaderText is not null)
            _propertyHeaderText.Text = UiText("Cad.Text.Properties", "Properties");

        _layerPanel.RefreshLanguage();
        _propertyInspector.RefreshLanguage();
        _commandLine.RefreshLanguage();
        ModelPanel.RefreshLanguage();
        RefreshCommandStatusLanguage();
        RefreshRibbonLanguage();

        UpdateToolUi(_workspace.Tools.ActiveTool);
        UpdateSelectionStatus();
        UpdateHistoryUi();
        RefreshInteractionUi();
        UpdateWindowTitle();
        SaveInteractionPreferences();
    }

    private void ApplyDocumentChangeSet(CadDocumentChangeSetEventArgs args)
    {
        ModelPanel.ApplyDocumentChangeSet(args);
        _propertyInspector.ApplyDocumentChangeSet(args);
        RefreshActionUi();
    }

    private void ApplySelection(CadSelectionChangedEventArgs args)
    {
        if (!_propertyInspector.IsInspectingLayer || args.Entities.Count > 0)
            _propertyInspector.InspectEntities(args.Entities);

        UpdateSelectionStatus();
        ModelPanel.SelectEntity(args.Primary);
        RefreshActionUi();
    }

    private void ApplySubobjectSelection(CadSubobjectSelectionChangedEventArgs args)
    {
        if (args.Primary is { } primary)
            _propertyInspector.InspectSubobject(primary);
        else if (!_propertyInspector.IsInspectingLayer)
            _propertyInspector.InspectEntities(_workspace.Selection.Selected.ToArray());

        UpdateSelectionStatus();
        RefreshActionUi();
    }

    private void UpdateSelectionStatus()
    {
        var selectionLabel = UiText("Cad.Text.Selection", "Selection");
        var hoverLabel = UiText("Cad.Text.Hover", "Hover");
        var count = _workspace.Selection.Selected.Count;
        var subobjectCount = _workspace.Subobjects.Selected.Count;

        var text = subobjectCount > 0
            ? UiFormat(
                "Cad.Text.SelectionWithSubobjects",
                "{0}: {1} ({2} subobjects)",
                selectionLabel,
                count,
                subobjectCount)
            : $"{selectionLabel}: {count}";

        if (_workspace.Subobjects.Primary is { } primary &&
            primary.TryGetPathSegment(out var pathSegment))
        {
            var type = pathSegment.Type == CadPathSegmentType.Line
                ? UiText("Cad.Text.PathSegmentLine", "Line")
                : UiText("Cad.Text.PathSegmentArc", "Arc");
            text += " | " + UiFormat(
                "Cad.Text.PathSegmentStatus",
                "Path segment {0}: {1}",
                pathSegment.Index + 1,
                type);
        }

        _selectionStatus.Text = text;

        if (_workspace.Preselection.Current is { } hover)
            ToolTip.SetTip(_modelTree, $"{text} | {hoverLabel}: {hover.Entity.Name}");
        else
            ToolTip.SetTip(_modelTree, text);
    }

    private void RefreshTree() => ModelPanel.Refresh();

    private void ModelTreeSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        ModelPanel.HandleSelectionChanged();

    private void LayerManagerChanged(CadLayerManagerChangedEventArgs args)
    {
        RefreshLayerUi();
        ModelPanel.ApplyLayerManagerChange(args);
        _layerPanel.Refresh();
        _propertyInspector.ApplyLayerManagerChange(args);
    }

    private void RefreshLayerUi()
    {
        _refreshingUi = true;
        try
        {
            _layerCombo.ItemsSource = _workspace.Layers.Layers.ToArray();
            _layerCombo.SelectedItem = _workspace.Layers.Current;
        }
        finally
        {
            _refreshingUi = false;
        }
    }

    private void UpdateToolUi(CadTool? tool)
    {
        if (tool is null)
        {
            _viewport.InteractionFeatures = OcctViewportInteractionFeatures.Default;
            if (_dynamicInputHud is not null)
                _dynamicInputHud.IsVisible = false;
            _snapAperture.IsVisible = false;
        }
        else
        {
            var features = OcctViewportInteractionFeatures.Default;
            var policy = tool.InteractionPolicy;

            if (!policy.SelectionEnabled)
                features &= ~OcctViewportInteractionFeatures.Selection;
            if (!policy.PreselectionEnabled)
                features &= ~OcctViewportInteractionFeatures.HoverDetection;

            _viewport.InteractionFeatures = features;

            if (_workspace.LastResolvedPoint is { } resolved)
                UpdateDynamicInputHud(resolved);
            else if (_dynamicInputHud is not null)
                _dynamicInputHud.IsVisible = false;
        }

        RefreshInteractionUi();
        RefreshActionUi();
        RefreshPanelMenuState();
    }

    private void WorkPlaneChanged()
    {
        _workspace.Tracking.Clear();
        RefreshInteractionUi();
    }

    private void DraftingChanged()
    {
        _workspace.Tracking.Clear();
        RefreshInteractionUi();
    }

    private void RefreshInteractionUi()
    {
        _refreshingUi = true;
        try
        {
            _snapToggle.Content = "SNAP";
            _snapToggle.IsChecked = _workspace.Snap.Enabled;

            _orthoToggle.Content = "ORTHO";
            _orthoToggle.IsChecked = _workspace.Drafting.OrthogonalTrackingEnabled;

            _polarToggle.Content = "POLAR";
            _polarToggle.IsChecked = _workspace.Drafting.PolarTrackingEnabled;

            var explicitDirectionLock =
                _workspace.Drafting.AxisLockEnabled ||
                _workspace.Drafting.AngleLockEnabled;
            _orthoToggle.IsEnabled = !explicitDirectionLock;
            _polarToggle.IsEnabled = !explicitDirectionLock;

            ToolTip.SetTip(
                _polarToggle,
                $"{UiText("Cad.Text.PolarIncrement", "Polar Increment")}: " +
                $"{_workspace.Drafting.PolarIncrementDegrees:0}°");

            RefreshWorkPlaneUi();
            RefreshSnapStatus();
        }
        finally
        {
            _refreshingUi = false;
        }
    }

    private void RefreshWorkPlaneUi()
    {
        var preset = _workspace.WorkPlane.Preset;
        _planeXy.IsChecked = preset == CadWorkPlanePreset.XY;
        _planeYz.IsChecked = preset == CadWorkPlanePreset.YZ;
        _planeXz.IsChecked = preset == CadWorkPlanePreset.XZ;

        var canChange = _workspace.Tools.CanChangeDrawingPlane;
        _planeXy.IsEnabled = canChange;
        _planeYz.IsEnabled = canChange;
        _planeXz.IsEnabled = canChange;

        var label = UiText("Cad.Text.WorkPlane", "Work Plane");
        var userLocked = _workspace.WorkPlane.UserPlaneLocked;
        var toolFixed = _workspace.WorkPlane.ToolPlaneFixed ||
                        _workspace.WorkPlane.GripPlaneFixed;
        var state = userLocked
            ? UiText("Cad.Text.Locked", "Locked")
            : toolFixed
                ? UiText("Cad.Text.ToolFixed", "Tool fixed")
                : null;

        if (_workPlaneUiLabel is not null)
        {
            _workPlaneUiLabel.Text = state is null
                ? $"{label}:"
                : $"{label} ({state}):";
            ToolTip.SetTip(
                _workPlaneUiLabel,
                state is null
                    ? $"{label}: {preset}"
                    : $"{label}: {preset} · {state}");
        }
    }

    private void RefreshSnapStatus()
    {
        UpdateSnapAperture();

        var objectSnap = UiText("Cad.Text.ObjectSnap", "Object Snap");
        string detail;
        if (!_workspace.Snap.Enabled)
        {
            detail = UiText("Cad.Text.SnapOff", "Snap: off");
        }
        else if (_workspace.Snap.Current is { } snap)
        {
            var name = LocalizeSnapType(snap.Type);
            var count = _workspace.Snap.Candidates.Count;
            var index = _workspace.Snap.CurrentCandidateIndex;
            var current = UiFormat(
                _workspace.Snap.TemporaryModes is null
                    ? "Cad.Text.SnapCurrent"
                    : "Cad.Text.SnapCurrentTemporary",
                _workspace.Snap.TemporaryModes is null
                    ? "Snap: {0}"
                    : "Snap: TEMP {0}",
                name);

            detail = count > 1 && index >= 0
                ? $"{current} {index + 1}/{count}"
                : current;
        }
        else
        {
            detail = UiText("Cad.Text.SnapReady", "Snap: ready");
        }

        ToolTip.SetTip(_snapToggle, $"{objectSnap} · {detail}");
    }

    private void UpdateHistoryUi() =>
        RefreshActionUi();

    private void UpdateCoordinateStatus(CadResolvedPoint resolved)
    {
        _coordinateStatus.Text =
            $"X {resolved.Point.X:F3}  " +
            $"Y {resolved.Point.Y:F3}  " +
            $"Z {resolved.Point.Z:F3}";

        UpdateDynamicInputHud(resolved);
        UpdateSnapAperture();
    }

    private void ClearCoordinateStatus()
    {
        _coordinateStatus.Text = string.Empty;
        if (_dynamicInputHud is not null)
            _dynamicInputHud.IsVisible = false;
        _snapAperture.IsVisible = false;
    }

    private void UpdateSnapAperture()
    {
        if (!_workspace.Snap.Enabled ||
            !_workspace.Snap.Active ||
            _workspace.Snap.EffectiveModes == CadSnapType.None ||
            _workspace.Tools.ActiveTool is not { State: CadToolState.Drawing } apertureTool ||
            !apertureTool.CurrentStep.RequiresPointer ||
            _workspace.LastPointerPosition is not { } pointer)
        {
            _snapAperture.IsVisible = false;
            return;
        }

        var radius = _workspace.Snap.PixelTolerance;
        if (!double.IsFinite(radius) || radius <= 0)
        {
            _snapAperture.IsVisible = false;
            return;
        }

        var scaling = TopLevel.GetTopLevel(_viewport)?.RenderScaling ?? 1.0;
        var diameter = Math.Max(4.0, radius * 2.0 / scaling);
        var x = pointer.X / scaling;
        var y = pointer.Y / scaling;

        _snapAperture.Width = diameter;
        _snapAperture.Height = diameter;
        Canvas.SetLeft(_snapAperture, x - diameter * 0.5);
        Canvas.SetTop(_snapAperture, y - diameter * 0.5);
        _snapAperture.Opacity = _workspace.Snap.Current is null ? 0.45 : 0.82;
        _snapAperture.IsVisible = true;
    }

    private void UpdateDynamicInputHud(CadResolvedPoint resolved)
    {
        if (_dynamicInputHud is null ||
            _workspace.LastPointerPosition is not { } pointer)
        {
            if (_dynamicInputHud is not null)
                _dynamicInputHud.IsVisible = false;
            return;
        }

        var scaling = TopLevel.GetTopLevel(_viewport)?.RenderScaling ?? 1.0;
        _dynamicInputHud.UpdateHud(resolved, pointer, scaling, _viewport.Bounds.Size);
    }

    private static string UiText(string key, string fallback) =>
        CadLanguageManager.Text(key, fallback);

    private static string UiFormat(
        string key,
        string fallback,
        params object?[] arguments)
    {
        var template = UiText(key, fallback);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, fallback, arguments);
        }
    }

    private static string LocalizeSnapType(CadSnapType type)
    {
        var key = type switch
        {
            CadSnapType.Endpoint => "Cad.Text.SnapEndpoint",
            CadSnapType.Midpoint => "Cad.Text.SnapMidpoint",
            CadSnapType.Center => "Cad.Text.SnapCenter",
            CadSnapType.Vertex => "Cad.Text.SnapVertex",
            CadSnapType.Quadrant => "Cad.Text.SnapQuadrant",
            CadSnapType.Nearest => "Cad.Text.SnapNearest",
            CadSnapType.Intersection => "Cad.Text.SnapIntersection",
            CadSnapType.Perpendicular => "Cad.Text.SnapPerpendicular",
            CadSnapType.Tangent => "Cad.Text.SnapTangent",
            _ => string.Empty
        };

        return key.Length == 0
            ? type.ToString()
            : UiText(key, type.ToString());
    }

    private static string LocalizeToolName(CadTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return CadLanguageManager.Text(tool.LocalizationKey, tool.DisplayName);
    }
}
