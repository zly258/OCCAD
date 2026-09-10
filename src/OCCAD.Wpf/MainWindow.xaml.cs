using System.Drawing;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using OCCAD;
using WpfTextBox = System.Windows.Controls.TextBox;
using OcctNet;

namespace OCCAD.Wpf;

public partial class MainWindow : System.Windows.Window
{
    private const string CadDocumentFilter =
        "OCCT CAD Document (*.ocad)|*.ocad|JSON (*.json)|*.json|All files (*.*)|*.*";

    private readonly CadWorkspace _workspace = new();
    private string? _documentPath;
    private readonly CadViewportInteractionController _viewportInteraction;
    private readonly CadPropertyInspectorController _propertyInspector;
    private bool _refreshingTree;
    private bool _refreshingLayerUi;
    private bool _refreshingSnapUi;
    private bool _refreshingPrecisionUi;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureViewport();
        _viewportInteraction = new CadViewportInteractionController(
            _workspace,
            Viewport,
            Dispatcher);
        _propertyInspector =
            new CadPropertyInspectorController(
                _workspace,
                PropertyInspector);
        WireEvents();
        RefreshTree();
        UpdateHistoryUi();
        RefreshLayerUi();
        RefreshSnapUi();
        RefreshPrecisionUi();
        UpdateDockLayout();
        UpdateWindowTitle();
        RefreshLanguageUi();
        SizeChanged += (_, _) => UpdateDockLayout();
    }

    private void ConfigureViewport()
    {
        Viewport.InteractionFeatures = OcctViewportInteractionFeatures.Default;
        Viewport.RectangleSelectionBehavior = OcctRectangleSelectionBehavior.Directional;
        Viewport.Cursor = System.Windows.Input.Cursors.Arrow;
        Viewport.InitialOptions = new OcctViewportInitializationOptions
        {
            BackgroundColor = Color.Black,
            ViewOrientation = OcctViewOrientation.Isometric,
            Projection = OcctProjectionType.Orthographic,
            TriedronVisible = true,
            ViewCubeVisible = false
        };
    }

    private void LanguageClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string language }) return;
        CadLanguageManager.Apply(language);
        RefreshLanguageUi();
        UpdateToolUi(_workspace.Tools.ActiveTool);
        UpdateSelectionStatus();
        UpdateHistoryUi();
        RefreshLayerUi();
        RefreshSnapUi();
        RefreshPrecisionUi();
        RefreshTree();
        UpdateWindowTitle();
    }

    private void WireEvents()
    {
        Viewport.EngineRecreated += (_, args) =>
        {
            _workspace.AttachEngine(args.Engine);
            ConfigureEngine(args.Engine);
            ToolStatus.Text = UiFormat(
                "Cad.Text.ReadyOcct",
                "Ready - OCCT {0}",
                OcctEngine.OcctVersion);
            RefreshTree();
            RefreshActionUi();
        };
        _viewportInteraction.CoordinateChanged += (_, args) =>
            UpdateCoordinateStatus(args.Value);
        _viewportInteraction.InteractionSettingsChanged += (_, _) =>
            Dispatcher.InvokeAsync(() =>
            {
                RefreshSnapUi();
                RefreshPrecisionUi();
            });
        Viewport.ErrorOccurred += (_, args) =>
            Dispatcher.InvokeAsync(() => ToolStatus.Text = args.Exception.Message);

        _workspace.Selection.Changed += (_, args) =>
            Dispatcher.InvokeAsync(() => ApplySelection(args));
        _workspace.Subobjects.Changed += (_, _) =>
            Dispatcher.InvokeAsync(() =>
            {
                UpdateSelectionStatus();
                RefreshActionUi();
            });
        _workspace.Preselection.Changed += (_, _) =>
            Dispatcher.InvokeAsync(UpdateSelectionStatus);
        _workspace.Document.ChangeSetCommitted += (_, args) =>
            Dispatcher.InvokeAsync(() => ApplyDocumentChangeSet(args));
        _workspace.Tools.ToolChanged += (_, args) =>
            Dispatcher.InvokeAsync(() => UpdateToolUi(_workspace.Tools.ActiveTool));
        _workspace.Tools.ToolUpdated += (_, args) =>
            Dispatcher.InvokeAsync(() => UpdateToolUi(_workspace.Tools.ActiveTool));
        _workspace.Actions.ActionFailed += (_, args) =>
            Dispatcher.InvokeAsync(() => ToolStatus.Text = args.Exception.Message);
        _workspace.Snap.CurrentChanged += (_, _) => Dispatcher.InvokeAsync(UpdateSnapStatus);
        _workspace.WorkPlane.Changed += (_, _) => Dispatcher.InvokeAsync(WorkPlaneChanged);
        _workspace.History.Changed += (_, _) => Dispatcher.InvokeAsync(UpdateHistoryUi);
        _workspace.Layers.Changed += (_, args) =>
            Dispatcher.InvokeAsync(() => LayerManagerChanged(args));
        _workspace.ModifiedChanged += (_, _) =>
            Dispatcher.InvokeAsync(UpdateWindowTitle);

        Closing += MainWindowClosing;
    }

    private static void ConfigureEngine(OcctEngine engine)
    {
        using (engine.BeginDisplayBatch())
        {
            engine.SetGradientBackground(Color.Black, Color.Black);
            engine.SetSelectionTolerance(5);
            engine.SetAntialiasing(true);
            engine.SetFaceBoundariesVisible(true, applyExisting: true);
            engine.SetDefaultMaterial(OcctMaterial.Plastified);
        }
    }

    private void MainWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (e.Key == Key.N)
            {
                NewDocument();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenDocument();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                SaveDocument(
                    saveAs: (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
                e.Handled = true;
                return;
            }

            if (TryExecuteActionShortcut(e))
                return;
        }

        if ((e.Key == Key.Enter || e.Key == Key.Space) &&
            Keyboard.FocusedElement is not WpfTextBox &&
            _workspace.Tools.ActiveTool is null)
        {
            if (_workspace.Actions.ExecuteLast())
            {
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter &&
            Keyboard.FocusedElement is not WpfTextBox &&
            _workspace.Tools.ActiveTool is { } activeTool)
        {
            _viewportInteraction.FlushPointerMoves();

            if (activeTool.CanFinish)
            {
                _workspace.Tools.FinishCurrent();
                e.Handled = true;
                return;
            }

            if (activeTool.CanCommitCurrentStage &&
                _workspace.Tools.CommitCurrentStage())
            {
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape &&
            Keyboard.FocusedElement is not WpfTextBox &&
            _workspace.Tools.ActiveTool is not null)
        {
            _workspace.Tools.CancelCurrent();
            e.Handled = true;
            return;
        }

        if (TryExecuteActionShortcut(e))
            return;
    }

    private bool TryExecuteActionShortcut(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is WpfTextBox)
            return false;

        var parts = new List<string>(4);
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Control) != 0)
            parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Shift) != 0)
            parts.Add("Shift");
        if ((modifiers & ModifierKeys.Alt) != 0)
            parts.Add("Alt");
        if ((modifiers & ModifierKeys.Windows) != 0)
            parts.Add("Meta");

        parts.Add(e.Key.ToString());
        var shortcut = string.Join("+", parts);
        if (!_workspace.Actions.ExecuteShortcut(shortcut))
            return false;

        e.Handled = true;
        return true;
    }

    private void UpdateCoordinateStatus(CadResolvedPoint resolved)
    {
        CoordinateStatus.Text =
            $"X {resolved.Point.X:F3}  Y {resolved.Point.Y:F3}  Z {resolved.Point.Z:F3}";
        UpdateDynamicInputHud(resolved);
        UpdateSnapAperture();
    }

    private void UpdateSnapAperture()
    {
        if (!_workspace.Snap.Enabled ||
            !_workspace.Snap.Active ||
            _workspace.Snap.EffectiveModes == CadSnapType.None ||
            _workspace.Tools.ActiveTool is not { State: CadToolState.Drawing } ||
            _workspace.LastPointerPosition is not { } pointer)
        {
            SnapAperture.Visibility =
                System.Windows.Visibility.Collapsed;
            return;
        }

        var radius = _workspace.Snap.PixelTolerance;
        if (!double.IsFinite(radius) || radius <= 0.0)
        {
            SnapAperture.Visibility =
                System.Windows.Visibility.Collapsed;
            return;
        }

        var diameter = Math.Max(4.0, radius * 2.0);
        SnapAperture.Width = diameter;
        SnapAperture.Height = diameter;
        Canvas.SetLeft(
            SnapAperture,
            pointer.X - diameter * 0.5);
        Canvas.SetTop(
            SnapAperture,
            pointer.Y - diameter * 0.5);
        SnapAperture.Opacity =
            _workspace.Snap.Current is null ? 0.45 : 0.8;
        SnapAperture.Visibility =
            System.Windows.Visibility.Visible;
    }

    private void UpdateDynamicInputHud(CadResolvedPoint resolved)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null ||
            tool.State != CadToolState.Drawing ||
            _workspace.LastPointerPosition is not { } pointer)
        {
            DynamicInputHud.Visibility = System.Windows.Visibility.Collapsed;
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
                _workspace.WorkPlane.WorldToLocal(resolved.Point);
            var dx = pointLocal.X - referenceLocal.X;
            var dy = pointLocal.Y - referenceLocal.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var angle =
                Math.Atan2(dy, dx) *
                180.0 /
                Math.PI;

            if ((precision & CadPrecisionInputKind.Length) != 0)
                values.Add($"L {length:F3}");
            if ((precision & CadPrecisionInputKind.Angle) != 0)
                values.Add($"A {angle:F2}°");
        }

        if ((precision & CadPrecisionInputKind.Factor) != 0 &&
            _workspace.Precision.Factor is { } factor)
            values.Add($"F {factor:G}");

        if (resolved.Snap is { } snap)
            values.Add($"{CadLanguageManager.Text("Cad.Text.StatusSnap", "SNAP")} {LocalizeSnapType(snap.Type)}");
        else if (resolved.Tracking is { } tracking)
        {
            var trackingName = tracking.Kind switch
            {
                CadTrackingKind.Orthogonal => CadLanguageManager.Text("Cad.Text.StatusOrtho", "ORTHO"),
                CadTrackingKind.Polar => CadLanguageManager.Text("Cad.Text.StatusPolar", "POLAR"),
                _ => tracking.Kind.ToString()
            };
            values.Add($"{trackingName} {tracking.AngleDegrees:F1}°");
        }

        if (values.Count == 0)
            values.Add(
                $"X {resolved.Point.X:F3}  Y {resolved.Point.Y:F3}  Z {resolved.Point.Z:F3}");

        DynamicValueText.Text = string.Join("   ", values);
        DynamicInputHud.Visibility = System.Windows.Visibility.Visible;

        const double offset = 18.0;
        const double estimatedWidth = 220.0;
        const double estimatedHeight = 34.0;
        var maxLeft = Math.Max(8.0, Viewport.ActualWidth - estimatedWidth - 8.0);
        var maxTop = Math.Max(8.0, Viewport.ActualHeight - estimatedHeight - 8.0);
        Canvas.SetLeft(
            DynamicInputHud,
            Math.Clamp(pointer.X + offset, 8.0, maxLeft));
        Canvas.SetTop(
            DynamicInputHud,
            Math.Clamp(pointer.Y + offset, 8.0, maxTop));
    }

    private void UpdateToolUi(CadTool? tool)
    {
        _toolPanel?.SetTool(tool);
        RefreshWorkPlaneStatusUi();

        if (tool is null)
        {
            Viewport.InteractionFeatures = OcctViewportInteractionFeatures.Default;
            ToolStatus.Text = UiText("Cad.Text.Ready", "Ready");
            DynamicInputHud.Visibility = System.Windows.Visibility.Collapsed;
            SnapAperture.Visibility = System.Windows.Visibility.Collapsed;
            RefreshPrecisionUi();
            RefreshActionUi();
            return;
        }

        Viewport.InteractionFeatures =
            tool.State == CadToolState.WaitForSelect
                ? OcctViewportInteractionFeatures.Default
                : OcctViewportInteractionFeatures.Default &
                  ~(OcctViewportInteractionFeatures.Selection |
                    OcctViewportInteractionFeatures.HoverDetection);

        RefreshOperationStatus(tool);

        if (_workspace.LastResolvedPoint is { } resolved)
            UpdateDynamicInputHud(resolved);
        else
            DynamicInputHud.Visibility = System.Windows.Visibility.Collapsed;

        RefreshPrecisionUi();
        RefreshActionUi();
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

    private static string LocalizeToolPrompt(CadToolPrompt prompt) =>
        CadLanguageManager.ToolPrompt(prompt);

    private static string LocalizeToolName(CadTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return CadLanguageManager.Text(
            tool.LocalizationKey,
            tool.DisplayName);
    }

    private string LocalizeEntityType(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var descriptor =
            _workspace.Entities.GetRequired(entity);
        return CadLanguageManager.Text(
            descriptor.LocalizationKey,
            descriptor.DisplayName);
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
                change.EntityChangeKind == CadEntityChangeKind.Metadata &&
                string.Equals(
                    change.PropertyName,
                    nameof(CadEntity.Name),
                    StringComparison.Ordinal));

        if (refreshTree)
            RefreshTree();

        _propertyInspector.ApplyDocumentChangeSet(args);
        RefreshActionUi();
    }

    private void ApplySelection(CadSelectionChangedEventArgs args)
    {
        if (!_propertyInspector.IsInspectingLayer ||
            args.Entities.Count > 0)
            _propertyInspector.InspectEntities(args.Entities);
        _workspace.Grips.Show(
            _workspace.Tools.Mode == CadInteractionMode.Normal
                ? args.Entities
                : []);
        UpdateSelectionStatus();
        SelectTreeEntity(args.Primary);
        RefreshActionUi();
    }

    private void UpdateSelectionStatus()
    {
        var selectionLabel = CadLanguageManager.Text("Cad.Text.Selection", "Selection");
        var hoverLabel = CadLanguageManager.Text("Cad.Text.Hover", "Hover");
        var count = _workspace.Selection.Selected.Count;
        var subobjectCount = _workspace.Subobjects.Selected.Count;
        var selectionText = subobjectCount > 0
            ? string.Format(
                CultureInfo.CurrentCulture,
                CadLanguageManager.Text("Cad.Text.SelectionWithSubobjects", "{0}: {1} ({2} subobjects)"),
                selectionLabel,
                count,
                subobjectCount)
            : $"{selectionLabel}: {count}";
        EntityTree.ToolTip = _workspace.Preselection.Current is { } hover
            ? $"{selectionText} | {hoverLabel}: {hover.Entity.Name}"
            : selectionText;
    }

    private void RefreshTree()
    {
        _refreshingTree = true;
        try
        {
            var selected = _workspace.Selection.Primary;
            EntityTree.Items.Clear();

            foreach (var group in _workspace.Document.Entities
                         .GroupBy(static entity => entity.EntityType)
                         .OrderBy(
                             group => LocalizeEntityType(group.First()),
                             StringComparer.CurrentCultureIgnoreCase))
            {
                var entities = group
                    .OrderBy(
                        static entity => entity.Name,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
                var groupItem = new TreeViewItem
                {
                    Header =
                        $"{LocalizeEntityType(entities[0])} [{entities.Length}]",
                    IsExpanded = true
                };

                foreach (var entity in entities)
                {
                    groupItem.Items.Add(new TreeViewItem
                    {
                        Header = $"{entity.Name}  [{entity.Layer}]",
                        Tag = entity,
                        IsSelected = ReferenceEquals(entity, selected)
                    });
                }

                EntityTree.Items.Add(groupItem);
            }
        }
        finally
        {
            _refreshingTree = false;
        }

        ApplyModelFilter();
    }

    private void EntityTreeSelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (_refreshingTree) return;
        if (_workspace.Tools.Mode == CadInteractionMode.Drawing &&
            _workspace.Tools.ActiveTool?.State != CadToolState.WaitForSelect)
            _workspace.Tools.CancelCurrent();

        if (EntityTree.SelectedItem is TreeViewItem { Tag: CadEntity entity } item)
        {
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

                var layer = _workspace.Layers.GetRequired(entity.Layer);
                ToolStatus.Text = layer.Visible
                    ? UiFormat("Cad.Text.LayerLockedMessage", "Layer {0} is locked.", layer.Name)
                    : UiFormat("Cad.Text.LayerHiddenMessage", "Layer {0} is hidden.", layer.Name);
                return;
            }

            _workspace.Selection.Select(entity);
        }
        else
        {
            _workspace.Selection.Clear();
        }
    }

    private void SelectTreeEntity(CadEntity? entity)
    {
        _refreshingTree = true;
        try
        {
            foreach (var group in EntityTree.Items.OfType<TreeViewItem>())
            {
                foreach (var item in group.Items.OfType<TreeViewItem>())
                {
                    var selected =
                        entity is not null &&
                        ReferenceEquals(item.Tag, entity);
                    item.IsSelected = selected;
                    if (selected)
                    {
                        group.IsExpanded = true;
                        item.BringIntoView();
                    }
                }
            }
        }
        finally
        {
            _refreshingTree = false;
        }
    }

    private void RefreshSnapUi()
    {
        _refreshingSnapUi = true;
        try
        {
            SnapToggle.IsChecked = _workspace.Snap.Enabled;
            EndpointSnapToggle.IsChecked = (_workspace.Snap.Modes & CadSnapType.Endpoint) != 0;
            MidpointSnapToggle.IsChecked = (_workspace.Snap.Modes & CadSnapType.Midpoint) != 0;
            CenterSnapToggle.IsChecked = (_workspace.Snap.Modes & CadSnapType.Center) != 0;
            VertexSnapToggle.IsChecked = (_workspace.Snap.Modes & CadSnapType.Vertex) != 0;
            QuadrantSnapToggle.IsChecked = (_workspace.Snap.Modes & CadSnapType.Quadrant) != 0;
        }
        finally
        {
            _refreshingSnapUi = false;
        }

        UpdateSnapStatus();
    }

    private void UpdateSnapStatus()
    {
        UpdateSnapAperture();

        if (!_workspace.Snap.Enabled)
        {
            ObjectSnapMenu.ToolTip =
                UiText("Cad.Text.SnapOff", "Snap: off");
            return;
        }

        if (!_workspace.Snap.Active)
        {
            ObjectSnapMenu.ToolTip =
                UiText("Cad.Text.SnapReady", "Snap: ready");
            return;
        }

        var temporary = _workspace.Snap.TemporaryModes is not null;
        if (_workspace.Snap.Current is { } snap)
        {
            var count = _workspace.Snap.Candidates.Count;
            var index = _workspace.Snap.CurrentCandidateIndex;
            var snapName = LocalizeSnapType(snap.Type);
            var template = temporary
                ? "Cad.Text.SnapCurrentTemporary"
                : "Cad.Text.SnapCurrent";
            var currentFallback = temporary
                ? "Snap: TEMP {0}"
                : "Snap: {0}";
            var current = UiFormat(
                template,
                currentFallback,
                snapName);
            ObjectSnapMenu.ToolTip =
                count > 1 && index >= 0
                    ? $"{current} {index + 1}/{count} [Tab]"
                    : current;
            return;
        }

        var modes = _workspace.Snap.EffectiveModes;
        var precision = new List<string>(9);
        if ((modes & CadSnapType.Endpoint) != 0) precision.Add("E");
        if ((modes & CadSnapType.Midpoint) != 0) precision.Add("M");
        if ((modes & CadSnapType.Center) != 0) precision.Add("C");
        if ((modes & CadSnapType.Vertex) != 0) precision.Add("V");
        if ((modes & CadSnapType.Quadrant) != 0) precision.Add("Q");
        if ((modes & CadSnapType.Nearest) != 0) precision.Add("N");
        if ((modes & CadSnapType.Intersection) != 0) precision.Add("I");
        if ((modes & CadSnapType.Perpendicular) != 0) precision.Add("P");
        if ((modes & CadSnapType.Tangent) != 0) precision.Add("T");

        var key = temporary
            ? "Cad.Text.SnapModesTemporary"
            : "Cad.Text.SnapModes";
        var fallback = temporary
            ? "Snap: TEMP{0}"
            : "Snap: on{0}";
        var detail = precision.Count == 0
            ? string.Empty
            : $" [{string.Join("/", precision)}]";
        ObjectSnapMenu.ToolTip = UiFormat(
            key,
            fallback,
            detail);
    }

    private void WorkPlaneChanged()
    {
        _workspace.Tracking.Clear();
        RefreshPrecisionUi();
        RefreshSnapUi();
        _toolPanel?.SetTool(_workspace.Tools.ActiveTool);
        RefreshWorkPlaneStatusUi();
        _viewportInteraction.RefreshCurrentDrawingPointer();
    }

    private void RefreshPrecisionUi()
    {
        _refreshingPrecisionUi = true;
        try
        {
            var explicitDirectionLock =
                _workspace.WorkPlane.AxisLockEnabled ||
                _workspace.WorkPlane.AngleLockEnabled;

            OrthoTrackingToggle.IsChecked = _workspace.WorkPlane.OrthogonalTrackingEnabled;
            PolarTrackingToggle.IsChecked = _workspace.WorkPlane.PolarTrackingEnabled;
            OrthoTrackingToggle.IsEnabled = !explicitDirectionLock;
            PolarTrackingToggle.IsEnabled = !explicitDirectionLock;

            PolarTrackingToggle.Content = string.Format(
                CultureInfo.CurrentCulture,
                CadLanguageManager.Text("Cad.Text.StatusPolarValue", "{0} {1:G}°"),
                CadLanguageManager.Text("Cad.Text.StatusPolar", "POLAR"),
                _workspace.WorkPlane.PolarIncrementDegrees);

            var polar = _workspace.WorkPlane.PolarIncrementDegrees;
            Polar15Menu.IsChecked = Math.Abs(polar - 15.0) <= 1e-12;
            Polar30Menu.IsChecked = Math.Abs(polar - 30.0) <= 1e-12;
            Polar45Menu.IsChecked = Math.Abs(polar - 45.0) <= 1e-12;
            Polar90Menu.IsChecked = Math.Abs(polar - 90.0) <= 1e-12;
        }
        finally
        {
            _refreshingPrecisionUi = false;
        }
    }

    private void UpdateHistoryUi()
    {
        var undoLabel = UiText(
            "Cad.Text.Undo",
            "Undo");
        var redoLabel = UiText(
            "Cad.Text.Redo",
            "Redo");
        UndoMenuItem.Header =
            _workspace.History.UndoName is { } undo
                ? $"{undoLabel} · {CadLanguageManager.HistoryName(undo)}"
                : undoLabel;
        RedoMenuItem.Header =
            _workspace.History.RedoName is { } redo
                ? $"{redoLabel} · {CadLanguageManager.HistoryName(redo)}"
                : redoLabel;
        UndoMenuItem.ToolTip = _workspace.History.CanUndo
            ? UiFormat(
                "Cad.Text.HistoryCurrent",
                "History: {0}",
                CadLanguageManager.HistoryName(_workspace.History.UndoName))
            : UiText(
                "Cad.Text.HistoryEmpty",
                "History: empty");

        RefreshActionUi();
    }

    private void LayerManagerChanged(CadLayerManagerChangedEventArgs args)
    {
        RefreshLayerUi();
        if (args.Kind != CadLayerManagerChangeKind.CurrentChanged)
            RefreshTree();

        _propertyInspector.ApplyLayerManagerChange(args);
    }

    private void RefreshLayerUi()
    {
        _refreshingLayerUi = true;
        try
        {
            var filter = LayerSearchBox.Text.Trim();
            var layers = _workspace.Layers.Layers
                .Where(layer =>
                    filter.Length == 0 ||
                    layer.Name.Contains(
                        filter,
                        StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(static layer => layer.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            LayerList.ItemsSource = null;
            LayerList.ItemsSource = layers;
            LayerList.SelectedItem = layers.Contains(_workspace.Layers.Current)
                ? _workspace.Layers.Current
                : null;
            LayerStatus.Text = UiFormat(
                "Cad.Text.LayerCurrent",
                "Layer: {0}",
                _workspace.Layers.Current.Name);
        }
        finally
        {
            _refreshingLayerUi = false;
        }
    }

    private void ActionClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement
            {
                Tag: string actionId
            })
            return;

        var action = _workspace.Actions.GetRequired(actionId);
        if (!action.CanExecute())
        {
            ToolStatus.Text =
                action is CadToolAction { RequiresSelection: true }
                    ? UiFormat(
                        "Cad.Text.ToolRequiresSelection",
                        "Select objects before {0}.",
                        action.DisplayName)
                    : UiFormat(
                        "Cad.Text.ActionUnavailable",
                        "{0} is not available in the current state.",
                        action.DisplayName);
            RefreshActionUi();
            return;
        }

        _workspace.Actions.Execute(actionId);
        RefreshActionUi();
    }

    private void NewClick(object sender, System.Windows.RoutedEventArgs e) =>
        NewDocument();

    private void OpenClick(object sender, System.Windows.RoutedEventArgs e) =>
        OpenDocument();

    private void SaveClick(object sender, System.Windows.RoutedEventArgs e) =>
        SaveDocument(saveAs: false);

    private void SaveAsClick(object sender, System.Windows.RoutedEventArgs e) =>
        SaveDocument(saveAs: true);

    private void ExitClick(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void LayerSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_refreshingLayerUi)
            RefreshLayerUi();
    }

    private void RefreshLayerClick(object sender, System.Windows.RoutedEventArgs e) =>
        RefreshLayerUi();

    private void LayerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingLayerUi || LayerList.SelectedItem is not CadLayer layer) return;
        _workspace.SetCurrentLayer(layer);
    }

    private void NewLayerClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var layer = _workspace.AddNextLayer();
        ToolStatus.Text = UiFormat(
            "Cad.Text.LayerCreated",
            "Layer created: {0}",
            layer.Name);
    }

    private void RenameLayerClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var layer = _workspace.Layers.Current;
        if (layer.IsDefault)
        {
            ToolStatus.Text = UiText(
                "Cad.Text.DefaultLayerRenameDenied",
                "Default layer 0 cannot be renamed.");
            return;
        }

        var dialog = new LayerNameDialog(this, layer.Name);
        if (dialog.ShowDialog() != true) return;

        try
        {
            _workspace.RenameLayer(layer, dialog.LayerName);
            ToolStatus.Text = UiFormat(
                "Cad.Text.LayerRenamed",
                "Layer renamed: {0}",
                layer.Name);
        }
        catch (InvalidOperationException exception)
        {
            ToolStatus.Text = exception.Message;
        }
    }

    private void RemoveLayerClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var layer = _workspace.Layers.Current;
        try
        {
            _workspace.RemoveLayer(layer);
            ToolStatus.Text = UiFormat(
                "Cad.Text.LayerRemoved",
                "Layer removed: {0}",
                layer.Name);
        }
        catch (InvalidOperationException exception)
        {
            ToolStatus.Text = exception.Message;
        }
    }

    private void AssignLayerClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_workspace.Selection.Selected.Count == 0)
        {
            ToolStatus.Text = UiText(
                "Cad.Text.AssignLayerRequiresSelection",
                "Select objects before assigning a layer.");
            return;
        }

        _workspace.AssignEntitiesToLayer(
            _workspace.Selection.Selected.ToArray(),
            _workspace.Layers.Current.Name);
        _propertyInspector.Refresh();
        RefreshTree();
    }

    private void LayerPropertiesClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _propertyInspector.InspectLayer(
            _workspace.Layers.Current);
        ShowPropertyDock();
    }

    private void SnapChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_refreshingSnapUi) return;
        _workspace.Snap.Enabled = SnapToggle.IsChecked == true;
        if (!_workspace.Snap.Enabled) _workspace.Snap.Clear();
        UpdateSnapStatus();
    }

    private void SnapModesChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_refreshingSnapUi || sender is not System.Windows.Controls.MenuItem toggle) return;

        var mode =
            ReferenceEquals(toggle, EndpointSnapToggle) ? CadSnapType.Endpoint :
            ReferenceEquals(toggle, MidpointSnapToggle) ? CadSnapType.Midpoint :
            ReferenceEquals(toggle, CenterSnapToggle) ? CadSnapType.Center :
            ReferenceEquals(toggle, VertexSnapToggle) ? CadSnapType.Vertex :
            ReferenceEquals(toggle, QuadrantSnapToggle) ? CadSnapType.Quadrant :
            CadSnapType.None;
        if (mode == CadSnapType.None) return;

        if (toggle.IsChecked == true)
            _workspace.Snap.Modes |= mode;
        else
            _workspace.Snap.Modes &= ~mode;

        _workspace.Snap.Clear();
        UpdateSnapStatus();
    }

    private void SnapTemporaryModeClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement
            {
                Tag: string value
            })
            return;

        if (string.Equals(
                value,
                "Persistent",
                StringComparison.OrdinalIgnoreCase))
        {
            _workspace.Snap.TemporaryModes = null;
            UpdateSnapStatus();
            return;
        }

        if (!_workspace.Snap.Active ||
            _workspace.Tools.ActiveTool is not { State: CadToolState.Drawing })
        {
            ToolStatus.Text = CadLanguageManager.Text(
                "Cad.Text.SnapTemporaryRequiresTool",
                "Start a drawing/edit stage before using a temporary snap override.");
            return;
        }

        if (!Enum.TryParse<CadSnapType>(
                value,
                ignoreCase: true,
                out var mode))
            return;

        _workspace.Snap.TemporaryModes = mode;
        UpdateSnapStatus();
    }

    private void PolarIncrementClick(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string value } ||
            !double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var increment))
            return;

        _workspace.WorkPlane.PolarIncrementDegrees = increment;
        _workspace.Tracking.Clear();
        RefreshPrecisionUi();
    }

    private void TrackingChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_refreshingPrecisionUi) return;

        _workspace.WorkPlane.OrthogonalTrackingEnabled =
            OrthoTrackingToggle.IsChecked == true;
        _workspace.WorkPlane.PolarTrackingEnabled =
            PolarTrackingToggle.IsChecked == true;
        _workspace.Tracking.Clear();
        RefreshPrecisionUi();
    }

    private void ToggleModelDockClick(object sender, System.Windows.RoutedEventArgs e)
    {
        ModelDockPane.Visibility =
            ModelDockPane.Visibility == System.Windows.Visibility.Visible
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
        UpdateDockLayout();
    }

    private void ToggleLayerDockClick(object sender, System.Windows.RoutedEventArgs e)
    {
        LayerDockPane.Visibility =
            LayerDockPane.Visibility == System.Windows.Visibility.Visible
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
        UpdateDockLayout();
    }

    private void TogglePropertyDockClick(object sender, System.Windows.RoutedEventArgs e)
    {
        PropertyDockPane.Visibility =
            PropertyDockPane.Visibility == System.Windows.Visibility.Visible
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
        UpdateDockLayout();
    }

    private void ShowPropertyDock()
    {
        PropertyDockPane.Visibility = System.Windows.Visibility.Visible;
        UpdateDockLayout();
    }

    private void UpdateDockLayout()
    {
        var availableWidth = ActualWidth > 0 ? ActualWidth : Width;
        var modelWidth = Math.Clamp(availableWidth * 0.18, 180, 240);
        var rightWidth = Math.Clamp(availableWidth * 0.24, 240, 320);
        var modelVisible =
            ModelDockPane.Visibility == System.Windows.Visibility.Visible;
        ModelDockColumn.Width = modelVisible
            ? new System.Windows.GridLength(modelWidth)
            : new System.Windows.GridLength(0);
        ModelSplitterColumn.Width = modelVisible
            ? new System.Windows.GridLength(4)
            : new System.Windows.GridLength(0);
        ModelDockSplitter.Visibility = modelVisible
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        var layerVisible =
            LayerDockPane.Visibility == System.Windows.Visibility.Visible;
        var propertyVisible =
            PropertyDockPane.Visibility == System.Windows.Visibility.Visible;
        var rightVisible = layerVisible || propertyVisible;

        RightDockColumn.Width = rightVisible
            ? new System.Windows.GridLength(rightWidth)
            : new System.Windows.GridLength(0);
        RightSplitterColumn.Width = rightVisible
            ? new System.Windows.GridLength(4)
            : new System.Windows.GridLength(0);
        RightDockSplitter.Visibility = rightVisible
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        LayerDockRow.Height = layerVisible
            ? new System.Windows.GridLength(propertyVisible ? 0.46 : 1.0, System.Windows.GridUnitType.Star)
            : new System.Windows.GridLength(0);
        PropertyDockRow.Height = propertyVisible
            ? new System.Windows.GridLength(layerVisible ? 0.54 : 1.0, System.Windows.GridUnitType.Star)
            : new System.Windows.GridLength(0);
        RightDockRowSplitter.Height = layerVisible && propertyVisible
            ? new System.Windows.GridLength(4)
            : new System.Windows.GridLength(0);
        LayerPropertySplitter.Visibility = layerVisible && propertyVisible
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    private void ModelSearchTextChanged(object sender, TextChangedEventArgs e) =>
        ApplyModelFilter();

    private void ApplyModelFilter()
    {
        var text = ModelSearchBox.Text.Trim();
        foreach (var group in EntityTree.Items.OfType<TreeViewItem>())
        {
            var groupMatch =
                text.Length == 0 ||
                (group.Header?.ToString()?.Contains(
                    text,
                    StringComparison.CurrentCultureIgnoreCase) ?? false);
            var anyChildVisible = false;

            foreach (var item in group.Items.OfType<TreeViewItem>())
            {
                var itemMatch =
                    text.Length == 0 ||
                    groupMatch ||
                    (item.Header?.ToString()?.Contains(
                        text,
                        StringComparison.CurrentCultureIgnoreCase) ?? false);
                item.Visibility = itemMatch
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
                anyChildVisible |= itemMatch;
            }

            group.Visibility =
                groupMatch || anyChildVisible
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            if (text.Length > 0 && anyChildVisible)
                group.IsExpanded = true;
        }
    }

    private void ClearClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_workspace.Actions.Execute("file.clear"))
            return;
        ResetDocumentUi(fit: false);
    }

    private void NewDocument()
    {
        if (!ConfirmSaveChanges())
            return;

        if (!_workspace.Actions.Execute("file.new"))
            return;

        _documentPath = null;
        ResetDocumentUi(fit: false);
        UpdateWindowTitle();
    }

    private void OpenDocument()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = CadDocumentFilter,
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        if (!ConfirmSaveChanges())
            return;

        try
        {
            CadDocumentSerializer.Load(_workspace, dialog.FileName);
            _documentPath = dialog.FileName;
            ResetDocumentUi(fit: true);
            UpdateWindowTitle();
            ToolStatus.Text = UiFormat(
                "Cad.Text.Opened",
                "Opened {0}",
                System.IO.Path.GetFileName(dialog.FileName));
        }
        catch (Exception exception)
        {
            ToolStatus.Text = exception.Message;
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                UiText(
                    "Cad.Text.OpenDocumentTitle",
                    "Open CAD Document"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private bool SaveDocument(bool saveAs)
    {
        var path = _documentPath;
        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = CadDocumentFilter,
                DefaultExt = ".ocad",
                AddExtension = true,
                FileName = string.IsNullOrWhiteSpace(path)
                    ? "Drawing.ocad"
                    : System.IO.Path.GetFileName(path)
            };
            if (dialog.ShowDialog(this) != true)
                return false;
            path = dialog.FileName;
        }

        try
        {
            CadDocumentSerializer.Save(_workspace, path);
            _documentPath = path;
            _workspace.MarkSaved();
            UpdateWindowTitle();
            ToolStatus.Text = UiFormat(
                "Cad.Text.Saved",
                "Saved {0}",
                System.IO.Path.GetFileName(path));
            return true;
        }
        catch (Exception exception)
        {
            ToolStatus.Text = exception.Message;
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                UiText(
                    "Cad.Text.SaveDocumentTitle",
                    "Save CAD Document"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    private void ResetDocumentUi(bool fit)
    {
        _propertyInspector.InspectEntities([]);
        EntityTree.ToolTip =
            UiText(
                "Cad.Text.SelectionInitial",
                "Selection: 0");
        RefreshTree();
        RefreshLayerUi();
        RefreshSnapUi();
        RefreshPrecisionUi();
        if (fit)
            _workspace.Actions.Execute("view.fit");
    }

    private bool ConfirmSaveChanges()
    {
        if (!_workspace.IsModified)
            return true;

        var documentName = string.IsNullOrWhiteSpace(_documentPath)
            ? "Drawing"
            : System.IO.Path.GetFileName(_documentPath);
        var message = string.Format(
            CultureInfo.CurrentCulture,
            CadLanguageManager.Text(
                "Cad.Text.UnsavedMessage",
                "Save changes to {0}?"),
            documentName);
        var result = System.Windows.MessageBox.Show(
            this,
            message,
            CadLanguageManager.Text(
                "Cad.Text.UnsavedTitle",
                "Unsaved Changes"),
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Warning);

        return result switch
        {
            System.Windows.MessageBoxResult.Yes =>
                SaveDocument(saveAs: false),
            System.Windows.MessageBoxResult.No => true,
            _ => false
        };
    }

    private void MainWindowClosing(
        object? sender,
        System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmSaveChanges())
        {
            e.Cancel = true;
            return;
        }

        CadLanguageManager.Changed -= CadLanguageChanged;
        _commandLine?.Dispose();
        _propertyInspector.Dispose();
        _viewportInteraction.Dispose();
        _workspace.Dispose();
    }

    private void UpdateWindowTitle()
    {
        var name = string.IsNullOrWhiteSpace(_documentPath)
            ? UiText(
                "Cad.Text.Drawing",
                "Drawing")
            : System.IO.Path.GetFileName(_documentPath);
        var modified = _workspace.IsModified ? " *" : string.Empty;
        Title = $"OCCAD - {name}{modified}";
    }
}
