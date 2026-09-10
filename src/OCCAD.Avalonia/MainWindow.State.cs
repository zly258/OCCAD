using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OCCAD;
using OcctNet;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private void RefreshAll()
    {
        RefreshTree();
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

        _modelSearch.PlaceholderText = UiText(
            "Cad.Text.FilterModel",
            "Filter model tree");
        if (_modelHeaderText is not null)
            _modelHeaderText.Text =
                UiText("Cad.Text.Model", "Model");
        if (_layerHeaderText is not null)
            _layerHeaderText.Text =
                UiText("Cad.Text.Layers", "Layers");
        if (_propertyHeaderText is not null)
            _propertyHeaderText.Text =
                UiText("Cad.Text.Properties", "Properties");

        _snapToggle.Content = UiText(
            "Cad.Text.StatusSnap",
            "SNAP");
        _orthoToggle.Content = UiText(
            "Cad.Text.StatusOrtho",
            "ORTHO");

        _layerPanel.RefreshLanguage();
        _propertyInspector.RefreshLanguage();
        _commandLine.RefreshLanguage();
        _toolPanel.RefreshLanguage();

        RefreshTree();
        UpdateToolUi(_workspace.Tools.ActiveTool);
        UpdateSelectionStatus();
        UpdateHistoryUi();
        RefreshInteractionUi();
        UpdateWindowTitle();
    }

    private void ApplyDocumentChangeSet(
        CadDocumentChangeSetEventArgs args)
    {
        var refreshTree =
            args.Contains(CadDocumentChangeKind.Added) ||
            args.Contains(CadDocumentChangeKind.Removed) ||
            args.Contains(CadDocumentChangeKind.Reset) ||
            args.Changes.Any(change =>
                change.Kind == CadDocumentChangeKind.Changed &&
                change.EntityChangeKind ==
                    CadEntityChangeKind.Metadata);

        if (refreshTree)
            RefreshTree();

        _propertyInspector.ApplyDocumentChangeSet(args);
        RefreshActionUi();
    }

    private void ApplySelection(
        CadSelectionChangedEventArgs args)
    {
        if (!_propertyInspector.IsInspectingLayer ||
            args.Entities.Count > 0)
        {
            _propertyInspector.InspectEntities(args.Entities);
        }

        UpdateSelectionStatus();
        SelectTreeEntity(args.Primary);
        RefreshActionUi();
    }

    private void ApplySubobjectSelection(
        CadSubobjectSelectionChangedEventArgs args)
    {
        if (args.Primary is { } primary)
        {
            _propertyInspector.InspectSubobject(primary);
        }
        else if (!_propertyInspector.IsInspectingLayer)
        {
            _propertyInspector.InspectEntities(
                _workspace.Selection.Selected.ToArray());
        }

        UpdateSelectionStatus();
        RefreshActionUi();
    }

    private void UpdateSelectionStatus()
    {
        var selectionLabel = UiText(
            "Cad.Text.Selection",
            "Selection");
        var hoverLabel = UiText(
            "Cad.Text.Hover",
            "Hover");
        var count = _workspace.Selection.Selected.Count;
        var subobjectCount =
            _workspace.Subobjects.Selected.Count;

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
        {
            ToolTip.SetTip(
                _modelTree,
                $"{text} | {hoverLabel}: {hover.Entity.Name}");
        }
        else
        {
            ToolTip.SetTip(_modelTree, text);
        }
    }

    private void RefreshTree()
    {
        if (_refreshingTree)
            return;

        _refreshingTree = true;
        try
        {
            var filter =
                _modelSearch.Text?.Trim() ??
                string.Empty;
            var selected = _workspace.Selection.Primary;

            var groups = _workspace.Document.Entities
                .GroupBy(static entity => entity.EntityType)
                .Select(group =>
                {
                    var entities = group
                        .Where(entity =>
                            filter.Length == 0 ||
                            entity.Name.Contains(
                                filter,
                                StringComparison.CurrentCultureIgnoreCase) ||
                            entity.Layer.Contains(
                                filter,
                                StringComparison.CurrentCultureIgnoreCase) ||
                            LocalizeEntityType(entity).Contains(
                                filter,
                                StringComparison.CurrentCultureIgnoreCase))
                        .OrderBy(
                            static entity => entity.Name,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();

                    if (entities.Length == 0)
                        return null;

                    var groupItem = new TreeViewItem
                    {
                        Header =
                            $"{LocalizeEntityType(entities[0])} [{entities.Length}]",
                        IsExpanded = true
                    };

                    groupItem.ItemsSource = entities
                        .Select(entity =>
                        {
                            var item = new TreeViewItem
                            {
                                Header =
                                    $"{entity.Name}  [{entity.Layer}]",
                                Tag = entity,
                                IsSelected =
                                    ReferenceEquals(
                                        entity,
                                        selected),
                                Opacity =
                                    _workspace.Document.IsEntitySelectable(entity)
                                        ? 1.0
                                        : 0.55
                            };
                            return item;
                        })
                        .ToArray();

                    return groupItem;
                })
                .Where(static item => item is not null)
                .Cast<TreeViewItem>()
                .OrderBy(
                    item => item.Header?.ToString(),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            _modelTree.ItemsSource = groups;
        }
        finally
        {
            _refreshingTree = false;
        }
    }

    private void ModelTreeSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (_refreshingTree)
            return;

        if (_workspace.Tools.ActiveTool is { } activeTool &&
            !activeTool.InteractionPolicy.SelectionEnabled)
        {
            _workspace.Tools.CancelCurrent();
        }

        if (_modelTree.SelectedItem is not
            TreeViewItem { Tag: CadEntity entity } item)
        {
            _workspace.Selection.Clear();
            return;
        }

        if (!_workspace.Document.IsEntitySelectable(entity))
        {
            _refreshingTree = true;
            try
            {
                item.IsSelected = false;
            }
            finally
            {
                _refreshingTree = false;
            }

            var layer =
                _workspace.Layers.GetRequired(entity.Layer);
            _toolStatus.Text = layer.Visible
                ? UiFormat(
                    "Cad.Text.LayerLockedMessage",
                    "Layer {0} is locked.",
                    layer.Name)
                : UiFormat(
                    "Cad.Text.LayerHiddenMessage",
                    "Layer {0} is hidden.",
                    layer.Name);
            return;
        }

        _workspace.Selection.Select(entity);
    }

    private void SelectTreeEntity(CadEntity? entity)
    {
        if (_modelTree.ItemsSource is not
            IEnumerable<TreeViewItem> groups)
            return;

        _refreshingTree = true;
        try
        {
            foreach (var group in groups)
            {
                if (group.ItemsSource is not
                    IEnumerable<TreeViewItem> items)
                    continue;

                foreach (var item in items)
                {
                    var selected =
                        entity is not null &&
                        ReferenceEquals(item.Tag, entity);
                    item.IsSelected = selected;
                    if (selected)
                        group.IsExpanded = true;
                }
            }
        }
        finally
        {
            _refreshingTree = false;
        }
    }

    private void LayerManagerChanged(
        CadLayerManagerChangedEventArgs args)
    {
        RefreshLayerUi();

        if (args.Kind !=
            CadLayerManagerChangeKind.CurrentChanged)
        {
            RefreshTree();
        }

        _layerPanel.Refresh();
        _propertyInspector.ApplyLayerManagerChange(args);
    }

    private void RefreshLayerUi()
    {
        _refreshingUi = true;
        try
        {
            _layerCombo.ItemsSource =
                _workspace.Layers.Layers.ToArray();
            _layerCombo.SelectedItem =
                _workspace.Layers.Current;
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
            _viewport.InteractionFeatures =
                OcctViewportInteractionFeatures.Default;
            _toolStatus.Text = UiText(
                "Cad.Text.Ready",
                "Ready");
            _dynamicHud.IsVisible = false;
            _snapAperture.IsVisible = false;
        }
        else
        {
            var features =
                OcctViewportInteractionFeatures.Default;
            var policy = tool.InteractionPolicy;

            if (!policy.SelectionEnabled)
                features &=
                    ~OcctViewportInteractionFeatures.Selection;
            if (!policy.PreselectionEnabled)
                features &=
                    ~OcctViewportInteractionFeatures.HoverDetection;

            _viewport.InteractionFeatures = features;

            _toolStatus.Text =
                tool.Prompt is { } prompt
                    ? CadLanguageManager.ToolPrompt(prompt)
                    : UiFormat(
                        "Cad.Text.ToolActive",
                        "{0}: active",
                        LocalizeToolName(tool));

            if (_workspace.LastResolvedPoint is { } resolved)
                UpdateDynamicInputHud(resolved);
            else
                _dynamicHud.IsVisible = false;
        }

        var canAcceptStep =
            tool is not null &&
            tool.CanCommitCurrentStage &&
            !tool.CurrentStep.RequiresPointer;
        _finishButton.Content =
            canAcceptStep
                ? UiText("Cad.Text.Accept", "Accept")
                : UiText("Cad.Text.Finish", "Finish");
        _finishButton.IsEnabled =
            canAcceptStep ||
            tool?.CanFinish == true;
        _cancelButton.Content =
            UiText("Cad.Text.Cancel", "Cancel");
        _cancelButton.IsEnabled =
            tool?.CanCancel == true;

        RefreshInteractionUi();
        RefreshActionUi();
        RefreshPanelMenuState();
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
        _viewportInteraction.RefreshCurrentDrawingPointer();
    }

    private void RefreshInteractionUi()
    {
        _refreshingUi = true;
        try
        {
            _snapToggle.Content =
                UiText("Cad.Text.StatusSnap", "SNAP");
            _snapToggle.IsChecked =
                _workspace.Snap.Enabled;

            _orthoToggle.Content =
                UiText("Cad.Text.StatusOrtho", "ORTHO");
            _orthoToggle.IsChecked =
                _workspace.Drafting
                    .OrthogonalTrackingEnabled;

            _polarToggle.Content = UiFormat(
                "Cad.Text.StatusPolarValue",
                "{0} {1:G}°",
                UiText("Cad.Text.StatusPolar", "POLAR"),
                _workspace.Drafting
                    .PolarIncrementDegrees);
            _polarToggle.IsChecked =
                _workspace.Drafting.PolarTrackingEnabled;

            var explicitDirectionLock =
                _workspace.Drafting.AxisLockEnabled ||
                _workspace.Drafting.AngleLockEnabled;
            _orthoToggle.IsEnabled = !explicitDirectionLock;
            _polarToggle.IsEnabled = !explicitDirectionLock;

            foreach (var pair in _snapModeItems)
            {
                pair.Value.IsChecked =
                    (_workspace.Snap.Modes & pair.Key) != 0;
            }

            foreach (var pair in _polarItems)
            {
                pair.Value.IsChecked =
                    Math.Abs(
                        _workspace.Drafting
                            .PolarIncrementDegrees -
                        pair.Key) <= 1e-12;
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
        _planeXy.IsChecked =
            preset == CadWorkPlanePreset.XY;
        _planeYz.IsChecked =
            preset == CadWorkPlanePreset.YZ;
        _planeXz.IsChecked =
            preset == CadWorkPlanePreset.XZ;

        var canChange =
            _workspace.Tools.CanChangeDrawingPlane;
        _planeXy.IsEnabled = canChange;
        _planeYz.IsEnabled = canChange;
        _planeXz.IsEnabled = canChange;

        var label = UiText(
            "Cad.Text.WorkPlane",
            "Work Plane");
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
            _snapStatus.Text =
                UiText("Cad.Text.SnapOff", "Snap: off");
            return;
        }

        if (!_workspace.Snap.Active)
        {
            _snapStatus.Text =
                UiText("Cad.Text.SnapReady", "Snap: ready");
            return;
        }

        if (_workspace.Snap.Current is { } snap)
        {
            var name = LocalizeSnapType(snap.Type);
            var count = _workspace.Snap.Candidates.Count;
            var index =
                _workspace.Snap.CurrentCandidateIndex;
            var current = UiFormat(
                _workspace.Snap.TemporaryModes is null
                    ? "Cad.Text.SnapCurrent"
                    : "Cad.Text.SnapCurrentTemporary",
                _workspace.Snap.TemporaryModes is null
                    ? "Snap: {0}"
                    : "Snap: TEMP {0}",
                name);

            _snapStatus.Text =
                count > 1 && index >= 0
                    ? $"{current} {index + 1}/{count}"
                    : current;
            return;
        }

        _snapStatus.Text =
            UiText("Cad.Text.SnapReady", "Snap: ready");
    }

    private void RefreshPrecisionUi()
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null)
        {
            _precisionStatus.Text =
                UiText(
                    "Cad.Text.PrecisionReady",
                    "Precision: ready");
            return;
        }

        var values = new List<string>(3);

        if (_workspace.Drafting.LengthLockEnabled)
        {
            values.Add(
                $"L={_workspace.Drafting.LockedLength:0.###}");
        }

        if (_workspace.Drafting.AngleLockEnabled)
        {
            values.Add(
                $"A={_workspace.Drafting.LockedAngleDegrees:0.###}°");
        }

        if (_workspace.Precision.Factor is { } factor)
            values.Add($"F={factor:0.###}");

        _precisionStatus.Text =
            values.Count == 0
                ? UiText(
                    "Cad.Text.PrecisionFree",
                    "Precision: free")
                : UiFormat(
                    "Cad.Text.PrecisionStatus",
                    "Precision: {0}",
                    string.Join("  ", values));
    }

    private void UpdateHistoryUi()
    {
        var undoName = CadLanguageManager.HistoryName(
            _workspace.History.UndoName);
        var redoName = CadLanguageManager.HistoryName(
            _workspace.History.RedoName);

        _historyStatus.Text =
            _workspace.History.CanUndo
                ? UiFormat(
                    "Cad.Text.HistoryCurrent",
                    "History: {0}",
                    undoName)
                : UiText(
                    "Cad.Text.HistoryEmpty",
                    "History: empty");

        if (_actionItems.TryGetValue(
                "edit.undo",
                out var undoItems))
        {
            var label = UiText(
                "Cad.Text.Undo",
                "Undo");
            foreach (var item in undoItems)
            {
                item.Header =
                    _workspace.History.CanUndo
                        ? $"{label} · {undoName}"
                        : label;
            }
        }

        if (_actionItems.TryGetValue(
                "edit.redo",
                out var redoItems))
        {
            var label = UiText(
                "Cad.Text.Redo",
                "Redo");
            foreach (var item in redoItems)
            {
                item.Header =
                    _workspace.History.CanRedo
                        ? $"{label} · {redoName}"
                        : label;
            }
        }

        RefreshActionUi();
    }

    private void UpdateCoordinateStatus(
        CadResolvedPoint resolved)
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
            _workspace.Snap.EffectiveModes ==
                CadSnapType.None ||
            _workspace.Tools.ActiveTool is not
                { State: CadToolState.Drawing } apertureTool ||
            !apertureTool.CurrentStep.RequiresPointer ||
            _workspace.LastPointerPosition is not { } pointer)
        {
            _snapAperture.IsVisible = false;
            return;
        }

        var radius = _workspace.Snap.PixelTolerance;
        if (!double.IsFinite(radius) ||
            radius <= 0)
        {
            _snapAperture.IsVisible = false;
            return;
        }

        var scaling =
            TopLevel.GetTopLevel(_viewport)?
                .RenderScaling ?? 1.0;
        var diameter =
            Math.Max(4.0, radius * 2.0 / scaling);
        var x = pointer.X / scaling;
        var y = pointer.Y / scaling;

        _snapAperture.Width = diameter;
        _snapAperture.Height = diameter;
        Canvas.SetLeft(
            _snapAperture,
            x - diameter * 0.5);
        Canvas.SetTop(
            _snapAperture,
            y - diameter * 0.5);
        _snapAperture.Opacity =
            _workspace.Snap.Current is null
                ? 0.45
                : 0.82;
        _snapAperture.IsVisible = true;
    }

    private void UpdateDynamicInputHud(
        CadResolvedPoint resolved)
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

        if (precision != CadPrecisionInputKind.None &&
            _workspace.WorkPlane.IsActive)
        {
            var reference =
                tool.PrecisionReferencePoint ??
                _workspace.WorkPlane.Origin;
            var referenceLocal =
                _workspace.WorkPlane.WorldToLocal(reference);
            var pointLocal =
                _workspace.WorkPlane.WorldToLocal(
                    resolved.Point);
            var dx =
                pointLocal.X - referenceLocal.X;
            var dy =
                pointLocal.Y - referenceLocal.Y;
            var length =
                Math.Sqrt(dx * dx + dy * dy);
            var angle =
                Math.Atan2(dy, dx) *
                180.0 /
                Math.PI;

            if ((precision &
                 CadPrecisionInputKind.Length) != 0)
                values.Add($"L {length:F3}");

            if ((precision &
                 CadPrecisionInputKind.Angle) != 0)
                values.Add($"A {angle:F2}°");
        }

        if ((precision &
             CadPrecisionInputKind.Factor) != 0 &&
            _workspace.Precision.Factor is { } factor)
        {
            values.Add($"F {factor:G}");
        }

        if (resolved.Snap is { } snap)
        {
            values.Add(
                $"{UiText("Cad.Text.StatusSnap", "SNAP")} " +
                LocalizeSnapType(snap.Type));
        }
        else if (resolved.Tracking is { } tracking)
        {
            var trackingName =
                tracking.Kind switch
                {
                    CadTrackingKind.Orthogonal =>
                        UiText(
                            "Cad.Text.StatusOrtho",
                            "ORTHO"),
                    CadTrackingKind.Polar =>
                        UiText(
                            "Cad.Text.StatusPolar",
                            "POLAR"),
                    _ => tracking.Kind.ToString()
                };

            values.Add(
                $"{trackingName} " +
                $"{tracking.AngleDegrees:F1}°");
        }

        if (values.Count == 0)
        {
            values.Add(
                $"X {resolved.Point.X:F3}  " +
                $"Y {resolved.Point.Y:F3}  " +
                $"Z {resolved.Point.Z:F3}");
        }

        _dynamicValue.Text =
            string.Join("   ", values);
        _dynamicHud.IsVisible = true;

        var scaling =
            TopLevel.GetTopLevel(_viewport)?
                .RenderScaling ?? 1.0;
        var x = pointer.X / scaling;
        var y = pointer.Y / scaling;
        const double offset = 18;
        const double estimatedWidth = 240;
        const double estimatedHeight = 36;

        var maxLeft = Math.Max(
            8,
            _viewport.Bounds.Width -
            estimatedWidth -
            8);
        var maxTop = Math.Max(
            8,
            _viewport.Bounds.Height -
            estimatedHeight -
            8);

        Canvas.SetLeft(
            _dynamicHud,
            Math.Clamp(
                x + offset,
                8,
                maxLeft));
        Canvas.SetTop(
            _dynamicHud,
            Math.Clamp(
                y + offset,
                8,
                maxTop));
    }

    private static string UiText(
        string key,
        string fallback) =>
        CadLanguageManager.Text(key, fallback);

    private static string UiFormat(
        string key,
        string fallback,
        params object?[] arguments)
    {
        var template = UiText(key, fallback);
        try
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                arguments);
        }
        catch (FormatException)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                fallback,
                arguments);
        }
    }

    private static string LocalizeSnapType(
        CadSnapType type)
    {
        var key = type switch
        {
            CadSnapType.Endpoint =>
                "Cad.Text.SnapEndpoint",
            CadSnapType.Midpoint =>
                "Cad.Text.SnapMidpoint",
            CadSnapType.Center =>
                "Cad.Text.SnapCenter",
            CadSnapType.Vertex =>
                "Cad.Text.SnapVertex",
            CadSnapType.Quadrant =>
                "Cad.Text.SnapQuadrant",
            CadSnapType.Nearest =>
                "Cad.Text.SnapNearest",
            CadSnapType.Intersection =>
                "Cad.Text.SnapIntersection",
            CadSnapType.Perpendicular =>
                "Cad.Text.SnapPerpendicular",
            CadSnapType.Tangent =>
                "Cad.Text.SnapTangent",
            _ => string.Empty
        };

        return key.Length == 0
            ? type.ToString()
            : UiText(key, type.ToString());
    }

    private static string LocalizeToolName(
        CadTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return CadLanguageManager.Text(
            tool.LocalizationKey,
            tool.DisplayName);
    }

    private string LocalizeEntityType(
        CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var descriptor =
            _workspace.Entities.GetRequired(entity);
        return CadLanguageManager.Text(
            descriptor.LocalizationKey,
            descriptor.DisplayName);
    }
}
