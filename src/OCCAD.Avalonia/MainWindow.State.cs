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
            message => _toolStatus.Text = message);

    private bool ChineseUi => string.Equals(
        CadLanguageManager.CurrentLanguage,
        "zh-CN",
        StringComparison.OrdinalIgnoreCase);

    private void RefreshAll()
    {
        ModelPanel.Refresh();
        RefreshLayerUi();
        UpdateToolUi(_workspace.Tools.ActiveTool);
        UpdateSelectionStatus();
        UpdateHistoryUi();
        RefreshInteractionUi();
        RefreshActionUi();
        RefreshPanelMenuState();
    }

    private void RefreshLanguageUi()
    {
        BuildMenu();

        if (_modelHeaderText is not null)
            _modelHeaderText.Text = UiText("Cad.Text.Model", "Model");
        if (_layerHeaderText is not null)
            _layerHeaderText.Text = UiText("Cad.Text.Layers", "Layers");
        if (_propertyHeaderText is not null)
            _propertyHeaderText.Text = UiText("Cad.Text.Properties", "Properties");

        _layerPanel.RefreshLanguage();
        _propertyInspector.RefreshLanguage();
        _commandLine.RefreshLanguage();
        _toolPanel.RefreshLanguage();
        ModelPanel.RefreshLanguage();
        RefreshRefinementLanguage();
        RefreshCompactToolbarLanguage();

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

    private void SelectTreeEntity(CadEntity? entity) => ModelPanel.SelectEntity(entity);

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
        _toolPanel.SetTool(tool);

        if (tool is null)
        {
            _viewport.InteractionFeatures = OcctViewportInteractionFeatures.Default;
            SetOperationStatus(UiText("Cad.Text.Ready", "Ready"));
            _dynamicHud.IsVisible = false;
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

            var prompt = tool.Prompt is { } activePrompt
                ? CadLanguageManager.ToolPrompt(activePrompt)
                : LocalizeToolName(tool);
            SetOperationStatus(prompt);

            if (_workspace.LastResolvedPoint is { } resolved)
                UpdateDynamicInputHud(resolved);
            else
                _dynamicHud.IsVisible = false;
        }

        RefreshInteractionUi();
        RefreshActionUi();
        RefreshPanelMenuState();
    }

    private void SetOperationStatus(string message)
    {
        var label = ChineseUi ? "操作提示" : "Prompt";
        _toolStatus.Text = $"{label}: {message}";
        ToolTip.SetTip(_toolStatus, _toolStatus.Text);
    }

    private void WorkPlaneChanged()
    {
        _workspace.Tracking.Clear();
        _toolPanel.SetTool(_workspace.Tools.ActiveTool);
        RefreshInteractionUi();
        _viewportInteraction.RefreshCurrentDrawingPointer();
    }

    private void DraftingChanged()
    {
        _workspace.Tracking.Clear();
        _toolPanel.SetTool(_workspace.Tools.ActiveTool);
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

            foreach (var pair in _snapModeItems)
                pair.Value.IsChecked = (_workspace.Snap.Modes & pair.Key) != 0;

            foreach (var pair in _polarItems)
            {
                pair.Value.IsChecked = Math.Abs(
                    _workspace.Drafting.PolarIncrementDegrees - pair.Key) <= 1e-12;
            }

            RefreshWorkPlaneUi();
            RefreshSnapStatus();
            RefreshPrecisionUi();
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
        _workPlaneStatus.Text =
            userLocked
                ? $"{label}: {preset} · {UiText("Cad.Text.Locked", "Locked")}"
                : toolFixed
                    ? $"{label}: {preset} · {UiText("Cad.Text.ToolFixed", "Tool fixed")}"
                    : $"{label}: {preset}";
    }

    private void RefreshSnapStatus()
    {
        UpdateSnapAperture();

        if (!_workspace.Snap.Enabled)
        {
            _snapStatus.Text = UiText("Cad.Text.SnapOff", "Snap: off");
            return;
        }

        if (!_workspace.Snap.Active)
        {
            _snapStatus.Text = UiText("Cad.Text.SnapReady", "Snap: ready");
            return;
        }

        if (_workspace.Snap.Current is { } snap)
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

            _snapStatus.Text = count > 1 && index >= 0
                ? $"{current} {index + 1}/{count}"
                : current;
            return;
        }

        _snapStatus.Text = UiText("Cad.Text.SnapReady", "Snap: ready");
    }

    private void RefreshPrecisionUi()
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null)
        {
            _precisionStatus.Text = UiText("Cad.Text.PrecisionReady", "Precision: ready");
            return;
        }

        var values = new List<string>(3);

        if (_workspace.Drafting.LengthLockEnabled)
            values.Add($"L={_workspace.Drafting.LockedLength:0.###}");

        if (_workspace.Drafting.AngleLockEnabled)
            values.Add($"A={_workspace.Drafting.LockedAngleDegrees:0.###}°");

        if (_workspace.Precision.Factor is { } factor)
            values.Add($"F={factor:0.###}");

        _precisionStatus.Text = values.Count == 0
            ? UiText("Cad.Text.PrecisionFree", "Precision: free")
            : UiFormat(
                "Cad.Text.PrecisionStatus",
                "Precision: {0}",
                string.Join("  ", values));
    }

    private void UpdateHistoryUi()
    {
        var undoName = CadLanguageManager.HistoryName(_workspace.History.UndoName);
        var redoName = CadLanguageManager.HistoryName(_workspace.History.RedoName);

        _historyStatus.Text = _workspace.History.CanUndo
            ? UiFormat("Cad.Text.HistoryCurrent", "History: {0}", undoName)
            : UiText("Cad.Text.HistoryEmpty", "History: empty");

        if (_actionItems.TryGetValue("edit.undo", out var undoItems))
        {
            var label = UiText("Cad.Text.Undo", "Undo");
            foreach (var item in undoItems)
            {
                item.Header = _workspace.History.CanUndo
                    ? $"{label} · {undoName}"
                    : label;
            }
        }

        if (_actionItems.TryGetValue("edit.redo", out var redoItems))
        {
            var label = UiText("Cad.Text.Redo", "Redo");
            foreach (var item in redoItems)
            {
                item.Header = _workspace.History.CanRedo
                    ? $"{label} · {redoName}"
                    : label;
            }
        }

        RefreshActionUi();
    }

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
        _dynamicHud.IsVisible = false;
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
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null ||
            tool.State != CadToolState.Drawing ||
            !tool.CurrentStep.RequiresPointer ||
            _workspace.LastPointerPosition is not { } pointer)
        {
            _dynamicHud.IsVisible = false;
            return;
        }

        var values = new List<string>(4);
        var precision = tool.PrecisionInputs;

        if (precision != CadPrecisionInputKind.None && _workspace.WorkPlane.IsActive)
        {
            var reference = tool.PrecisionReferencePoint ?? _workspace.WorkPlane.Origin;
            var referenceLocal = _workspace.WorkPlane.WorldToLocal(reference);
            var pointLocal = _workspace.WorkPlane.WorldToLocal(resolved.Point);
            var dx = pointLocal.X - referenceLocal.X;
            var dy = pointLocal.Y - referenceLocal.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            if ((precision & CadPrecisionInputKind.Length) != 0)
                values.Add($"L {length:F3}");
            if ((precision & CadPrecisionInputKind.Angle) != 0)
                values.Add($"A {angle:F2}°");
        }

        if ((precision & CadPrecisionInputKind.Factor) != 0 &&
            _workspace.Precision.Factor is { } factor)
            values.Add($"F {factor:G}");

        if (resolved.Snap is { } snap)
        {
            values.Add($"SNAP {LocalizeSnapType(snap.Type)}");
        }
        else if (resolved.Tracking is { } tracking)
        {
            var trackingName = tracking.Kind switch
            {
                CadTrackingKind.Orthogonal => "ORTHO",
                CadTrackingKind.Polar => "POLAR",
                _ => tracking.Kind.ToString().ToUpperInvariant()
            };
            values.Add($"{trackingName} {tracking.AngleDegrees:F1}°");
        }

        if (values.Count == 0)
        {
            values.Add(
                $"X {resolved.Point.X:F3}  " +
                $"Y {resolved.Point.Y:F3}  " +
                $"Z {resolved.Point.Z:F3}");
        }

        _dynamicValue.Text = string.Join("   ", values);
        _dynamicHud.IsVisible = true;

        var scaling = TopLevel.GetTopLevel(_viewport)?.RenderScaling ?? 1.0;
        var x = pointer.X / scaling;
        var y = pointer.Y / scaling;
        var offset = CadTheme.DynamicHudOffset;
        var estimatedWidth = CadTheme.DynamicHudMaxWidth;
        var estimatedHeight = CadTheme.DynamicHudEstimatedHeight;
        var margin = CadTheme.OverlayMargin;

        var maxLeft = Math.Max(
            margin,
            _viewport.Bounds.Width - estimatedWidth - margin);
        var maxTop = Math.Max(
            margin,
            _viewport.Bounds.Height - estimatedHeight - margin);

        Canvas.SetLeft(
            _dynamicHud,
            Math.Clamp(x + offset, margin, maxLeft));
        Canvas.SetTop(
            _dynamicHud,
            Math.Clamp(y + offset, margin, maxTop));
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
