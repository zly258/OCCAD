using System.Drawing;
using Avalonia.Input;
using Avalonia.Threading;
using OCCAD;
using OcctNet;

namespace OCCAD.Avalonia;

internal sealed class CadCoordinateChangedEventArgs(CadResolvedPoint value)
    : EventArgs
{
    public CadResolvedPoint Value { get; } = value;
}

internal sealed class CadViewportInteractionController : IDisposable
{
    private static readonly Cursor NavigationCursor =
        new(StandardCursorType.SizeAll);

    private readonly CadWorkspace _workspace;
    private readonly OcctAvaloniaViewport _viewport;
    private readonly CadPointerMoveScheduler _pointerMoves;
    private readonly List<OcctShape> _subobjectMarkers = [];
    private bool _shiftMiddleRotating;
    private bool _middleNavigating;
    private bool _selectionGesture;
    private bool _selectionRectangleVisible;
    private int _selectionStartX;
    private int _selectionStartY;
    private int _selectionCurrentX;
    private int _selectionCurrentY;
    private OcctInputModifiers _selectionModifiers;
    private bool _disposed;

    public CadViewportInteractionController(
        CadWorkspace workspace,
        OcctAvaloniaViewport viewport,
        Dispatcher dispatcher)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        ArgumentNullException.ThrowIfNull(dispatcher);

        _pointerMoves = new CadPointerMoveScheduler(dispatcher, ProcessPointer);
        _viewport.PreviewPointerInput += PreviewPointerInput;
        _viewport.PreviewKeyInput += PreviewKeyInput;
        _viewport.ObjectSelectionChanged += ObjectSelectionChanged;
        _viewport.HoverHitChanged += HoverHitChanged;
        _viewport.PointerExited += ViewportPointerExited;
        _viewport.EngineRecreated += ViewportEngineRecreated;
        _workspace.Tools.ToolChanged += ToolChanged;
        _workspace.Tools.ToolUpdated += ToolUpdated;
        _workspace.Subobjects.Changed += SubobjectsChanged;
        RefreshCursor();
    }

    public event EventHandler<CadCoordinateChangedEventArgs>? CoordinateChanged;
    public event EventHandler? CoordinateCleared;
    public event EventHandler? InteractionSettingsChanged;

    public void FlushPointerMoves() => _pointerMoves.Flush();

    public void RefreshCurrentDrawingPointer() =>
        RefreshDrawingPointer(OcctInputModifiers.None);

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException(
                "The OCCT engine is not initialized.");

        engine.SetAutomaticHighlight(
            _workspace.Tools.ActiveTool is null);

        RebuildSubobjectMarkers(
            _workspace.Subobjects.Selected);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _viewport.PreviewPointerInput -= PreviewPointerInput;
        _viewport.PreviewKeyInput -= PreviewKeyInput;
        _viewport.ObjectSelectionChanged -= ObjectSelectionChanged;
        _viewport.HoverHitChanged -= HoverHitChanged;
        _viewport.PointerExited -= ViewportPointerExited;
        _viewport.EngineRecreated -= ViewportEngineRecreated;
        _workspace.Tools.ToolChanged -= ToolChanged;
        _workspace.Tools.ToolUpdated -= ToolUpdated;
        _workspace.Subobjects.Changed -= SubobjectsChanged;
        ClearSubobjectMarkers();
        _pointerMoves.Dispose();
        _viewport.Cursor = Cursor.Default;
    }

    private void PreviewPointerInput(
        object? sender,
        OcctPointerInputEventArgs input)
    {
        if (_workspace.Engine is null) return;

        UpdateNavigationCursorState(input);
        RefreshCursor();

        if (TryHandleMiddleDoubleClickFit(input) ||
            TryHandleCadRightButton(input) ||
            TryHandleShiftMiddleRotation(input))
            return;

        if (IsViewportNavigationInput(input))
        {
            _pointerMoves.Clear();
            return;
        }

        if (input.Kind == OcctPointerInputKind.Moved &&
            input.Buttons == OcctPointerButtons.None &&
            _workspace.Tools.ActiveTool is { State: CadToolState.Drawing })
        {
            input.Handled = true;
            _pointerMoves.Post(input);
            return;
        }

        _pointerMoves.Flush();
        ProcessPointer(input);
    }

    private bool TryHandleMiddleDoubleClickFit(
        OcctPointerInputEventArgs input)
    {
        if (input.Kind != OcctPointerInputKind.DoubleClicked ||
            input.Button != OcctPointerButton.Middle ||
            _workspace.Engine is not { IsInitialized: true } engine)
            return false;

        _pointerMoves.Clear();
        _shiftMiddleRotating = false;
        input.Handled = true;
        engine.FitAll();
        RefreshCurrentDrawingPointer();
        return true;
    }

    private bool TryHandleCadRightButton(
        OcctPointerInputEventArgs input)
    {
        var isRightInput =
            input.Button == OcctPointerButton.Right ||
            (input.Buttons & OcctPointerButtons.Right) != 0;
        if (!isRightInput)
            return false;

        _pointerMoves.Flush();
        input.Handled = true;

        if (input.Kind == OcctPointerInputKind.Pressed)
            _workspace.Tools.HandleSecondaryAction();

        return true;
    }

    private bool TryHandleShiftMiddleRotation(
        OcctPointerInputEventArgs input)
    {
        var engine = _workspace.Engine;
        if (engine is null || !engine.IsInitialized)
            return false;

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Middle &&
            (input.Modifiers & OcctInputModifiers.Shift) != 0)
        {
            _pointerMoves.Clear();
            _shiftMiddleRotating = true;
            RefreshCursor();
            input.Handled = true;
            engine.StartRotation(input.X, input.Y);
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Moved &&
            _shiftMiddleRotating)
        {
            if ((input.Buttons & OcctPointerButtons.Middle) == 0)
            {
                _shiftMiddleRotating = false;
                RefreshCursor();
                return false;
            }

            input.Handled = true;
            engine.Rotation(input.X, input.Y);
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Released &&
            input.Button == OcctPointerButton.Middle &&
            _shiftMiddleRotating)
        {
            _shiftMiddleRotating = false;
            RefreshCursor();
            input.Handled = true;
            RefreshCurrentDrawingPointer();
            return true;
        }

        return false;
    }

    private void UpdateNavigationCursorState(
        OcctPointerInputEventArgs input)
    {
        var changed = false;

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Middle)
        {
            changed = !_middleNavigating;
            _middleNavigating = true;
        }
        else if ((input.Kind == OcctPointerInputKind.Released &&
                  input.Button == OcctPointerButton.Middle) ||
                 (input.Kind == OcctPointerInputKind.Moved &&
                  _middleNavigating &&
                  (input.Buttons & OcctPointerButtons.Middle) == 0))
        {
            changed = _middleNavigating;
            _middleNavigating = false;
        }

        if (changed)
            RefreshCursor();
    }

    private void RefreshCursor()
    {
        _viewport.Cursor =
            _middleNavigating || _shiftMiddleRotating
                ? NavigationCursor
                : _workspace.Tools.ActiveTool is
                    { State: CadToolState.Drawing } tool &&
                  tool.CurrentStep.RequiresPointer
                    ? CadDrawingCursor.Instance
                    : Cursor.Default;
    }

    private static bool IsViewportNavigationInput(
        OcctPointerInputEventArgs input) =>
        input.Kind == OcctPointerInputKind.Wheel ||
        input.Button == OcctPointerButton.Middle ||
        (input.Buttons & OcctPointerButtons.Middle) != 0;

    private void PreviewKeyInput(
        object? sender,
        OcctKeyInputEventArgs input)
    {
        if (TryHandleInteractionShortcut(input))
        {
            input.Handled = true;
            RefreshCurrentDrawingPointer();
            InteractionSettingsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (TryHandleDrawingPlaneShortcut(input))
        {
            input.Handled = true;
            RefreshCurrentDrawingPointer();
            return;
        }

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key == OcctKey.Tab &&
            _workspace.Tools.ActiveTool is
                { State: CadToolState.Drawing } &&
            _workspace.Snap.Active)
        {
            _pointerMoves.Flush();
            var backwards =
                (input.Modifiers & OcctInputModifiers.Shift) != 0;
            var cycled = backwards
                ? _workspace.Snap.CyclePrevious()
                : _workspace.Snap.CycleNext();

            if (cycled)
            {
                input.Handled = true;
                RefreshDrawingPointer(input.Modifiers);
                return;
            }
        }

        if (_workspace.Tools.HandleKey(input))
            input.Handled = true;
    }

    private bool TryHandleInteractionShortcut(
        OcctKeyInputEventArgs input)
    {
        if (input.Kind != OcctKeyInputKind.Pressed ||
            input.IsRepeat ||
            (input.Modifiers &
             (OcctInputModifiers.Control |
              OcctInputModifiers.Alt |
              OcctInputModifiers.Meta)) != 0)
            return false;

        switch (input.Key)
        {
            case OcctKey.F3:
                _workspace.Snap.Enabled = !_workspace.Snap.Enabled;
                if (!_workspace.Snap.Enabled)
                    _workspace.Snap.Clear();
                return true;

            case OcctKey.F8:
                _workspace.Drafting.OrthogonalTrackingEnabled =
                    !_workspace.Drafting.OrthogonalTrackingEnabled;
                _workspace.Tracking.Clear();
                return true;

            case OcctKey.F10:
                _workspace.Drafting.PolarTrackingEnabled =
                    !_workspace.Drafting.PolarTrackingEnabled;
                _workspace.Tracking.Clear();
                return true;

            default:
                return false;
        }
    }

    private bool TryHandleDrawingPlaneShortcut(
        OcctKeyInputEventArgs input)
    {
        if (input.Kind != OcctKeyInputKind.Pressed ||
            input.IsRepeat ||
            (input.Modifiers &
             (OcctInputModifiers.Control |
              OcctInputModifiers.Alt |
              OcctInputModifiers.Meta)) != 0)
            return false;

        var preset = input.Key switch
        {
            OcctKey.S => CadWorkPlanePreset.YZ,
            OcctKey.F => CadWorkPlanePreset.XZ,
            OcctKey.T => CadWorkPlanePreset.XY,
            _ => (CadWorkPlanePreset?)null
        };

        return preset is { } value &&
               _workspace.Tools.TryChangeDrawingPlane(value);
    }

    private void RefreshDrawingPointer(
        OcctInputModifiers modifiers)
    {
        if (_workspace.LastPointerPosition is not { } pointer)
            return;

        var refresh = new OcctPointerInputEventArgs(
            OcctPointerInputKind.Moved,
            OcctPointerButton.None,
            OcctPointerButtons.None,
            pointer.X,
            pointer.Y,
            0,
            modifiers);
        ProcessPointer(refresh);
    }

    private void ObjectSelectionChanged(
        object? sender,
        OcctAvaloniaSelectionEventArgs input)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null ||
            tool.InteractionPolicy.SelectionEnabled)
        {
            _workspace.Selection.UpdateFromViewer(
                input.SelectedObjects,
                input.SelectedObject);
        }
    }

    private void HoverHitChanged(
        object? sender,
        OcctViewportHoverHitChangedEventArgs input)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is not null &&
            !tool.InteractionPolicy.PreselectionEnabled)
        {
            _workspace.Preselection.Clear();
            return;
        }

        var hit = input.Hit;
        var entity = hit is { } value
            ? _workspace.Document.FindByViewerObject(value.Owner)
            : null;
        _workspace.Preselection.Update(entity, hit);
    }

    private void ToolChanged(
        object? sender,
        CadToolChangedEventArgs input)
    {
        ClearTransientInput();
        _workspace.ClearPointerObservation();

        if (_workspace.Engine is { IsInitialized: true } engine)
        {
            // OCCT automatic detection highlight is intentionally disabled
            // while a Tool owns the pointer. Otherwise the final committed
            // entity can be highlighted immediately at the same cursor
            // position and look like a stale, oversized preview.
            engine.SetAutomaticHighlight(input.Tool is null);
            engine.Redraw();
        }

        CoordinateCleared?.Invoke(
            this,
            EventArgs.Empty);
        RefreshCursor();
    }

    private void ToolUpdated(
        object? sender,
        CadToolChangedEventArgs input)
    {
        if (input.Tool is null ||
            !input.Tool.InteractionPolicy.PreselectionEnabled)
        {
            _workspace.Preselection.Clear();
        }

        RefreshCursor();
    }

    private void ViewportEngineRecreated(
        object? sender,
        OcctEngineLifecycleEventArgs input)
    {
        _pointerMoves.Clear();
        _selectionGesture = false;
        _selectionRectangleVisible = false;

        // Presentation handles are scoped to one native engine generation.
        // Drop old handles before Workspace.AttachEngine can stabilize
        // subobject selections and raise selection events against the new
        // engine.
        _subobjectMarkers.Clear();
    }

    private void ViewportPointerExited(
        object? sender,
        PointerEventArgs input)
    {
        ClearTransientInput();
        _workspace.ClearPointerObservation();
        CoordinateCleared?.Invoke(
            this,
            EventArgs.Empty);
        _viewport.Cursor = Cursor.Default;
    }

    private void SubobjectsChanged(
        object? sender,
        CadSubobjectSelectionChangedEventArgs input) =>
        RebuildSubobjectMarkers(input.Items);

    private void RebuildSubobjectMarkers(
        IReadOnlyList<CadSubobjectSelection> items)
    {
        var engine = _workspace.Engine;
        if (engine is null || !engine.IsInitialized) return;

        using var batch = engine.BeginDisplayBatch();
        DeleteSubobjectMarkers(engine);
        _subobjectMarkers.Clear();

        foreach (var item in items)
        {
            if (!item.Point.IsFinite) continue;

            var marker = engine.MakeVertex(item.Point);
            _subobjectMarkers.Add(marker);
            engine.SetObjectSelectable(marker, false);
            engine.SetObjectColor(marker, Color.OrangeRed);
            engine.SetObjectDisplayMode(
                marker,
                OcctDisplayMode.Wireframe);
        }
    }

    private void ClearSubobjectMarkers()
    {
        if (_workspace.Engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            DeleteSubobjectMarkers(engine);
        }

        _subobjectMarkers.Clear();
    }

    private void DeleteSubobjectMarkers(OcctEngine engine)
    {
        var existing = _subobjectMarkers
            .Where(marker => engine.ContainsObject(marker.Id))
            .Cast<IOcctObject>()
            .ToArray();
        if (existing.Length > 0)
            engine.Delete(existing);
    }

    private void ClearTransientInput()
    {
        _pointerMoves.Clear();
        _shiftMiddleRotating = false;
        _middleNavigating = false;
        CancelSelectionGesture();
        _workspace.Preselection.Clear();
        _workspace.Snap.Clear();
        _workspace.Tracking.Clear();
        _workspace.Grips.ClearHot();
    }

    private void ProcessPointer(
        OcctPointerInputEventArgs input)
    {
        if (_workspace.Engine is null) return;

        if (_selectionGesture &&
            HandleSelectionGesture(input))
            return;

        if (_workspace.Tools.HandlePointer(input))
        {
            input.Handled = true;
            PublishLastCoordinate();
            return;
        }

        if (_workspace.Tools.ActiveTool is null &&
            input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left &&
            _workspace.Grips.TryHit(input.X, input.Y, out var grip))
        {
            _workspace.Subobjects.Clear();
            _workspace.Tools.BeginGripEdit(grip);
            input.Handled = true;
            return;
        }

        if (_workspace.Tools.ActiveTool is null &&
            input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left)
        {
            if (_workspace.Preselection.Current is
                { IsSubshape: true } subobject)
            {
                _workspace.Subobjects.Select(
                    subobject,
                    SelectionOperation(input.Modifiers));
                input.Handled = true;
                return;
            }

            BeginSelectionGesture(input);
            input.Handled = true;
            return;
        }

        if (input.Kind != OcctPointerInputKind.Moved)
            return;

        if (_workspace.Tools.Mode == CadInteractionMode.Normal)
            _workspace.Grips.UpdateHot(input.X, input.Y);

        try
        {
            PublishCoordinate(
                _workspace.ResolvePoint(input.X, input.Y));
        }
        catch (InvalidOperationException)
        {
            // View ray parallel to active work plane.
        }
    }

    private void BeginSelectionGesture(
        OcctPointerInputEventArgs input)
    {
        CancelSelectionGesture();
        _selectionGesture = true;
        _selectionStartX = input.X;
        _selectionStartY = input.Y;
        _selectionCurrentX = input.X;
        _selectionCurrentY = input.Y;
        _selectionModifiers = input.Modifiers;
    }

    private bool HandleSelectionGesture(
        OcctPointerInputEventArgs input)
    {
        if (!_selectionGesture)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            if ((input.Buttons & OcctPointerButtons.Left) == 0)
            {
                CancelSelectionGesture();
                return false;
            }

            input.Handled = true;
            _selectionCurrentX = input.X;
            _selectionCurrentY = input.Y;
            UpdateSelectionRectangle();
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Released &&
            input.Button == OcctPointerButton.Left)
        {
            input.Handled = true;
            _selectionCurrentX = input.X;
            _selectionCurrentY = input.Y;
            CompleteSelectionGesture();
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button != OcctPointerButton.Left)
        {
            CancelSelectionGesture();
        }

        return false;
    }

    private void UpdateSelectionRectangle()
    {
        if (_workspace.Engine is not { IsInitialized: true } engine)
            return;

        var threshold = Math.Max(
            0,
            _viewport.RectangleSelectionThreshold);
        var dx = Math.Abs(
            _selectionCurrentX - _selectionStartX);
        var dy = Math.Abs(
            _selectionCurrentY - _selectionStartY);
        if (dx < threshold &&
            dy < threshold)
        {
            HideSelectionRectangle(engine);
            return;
        }

        var crossing =
            _selectionCurrentX < _selectionStartX;
        var lineColor = crossing
            ? Color.ForestGreen
            : Color.CornflowerBlue;
        var fillColor = crossing
            ? Color.FromArgb(54, 70, 150, 70)
            : Color.FromArgb(44, 80, 120, 210);

        engine.ShowSelectionRectangle(
            Math.Min(_selectionStartX, _selectionCurrentX),
            Math.Min(_selectionStartY, _selectionCurrentY),
            Math.Max(_selectionStartX, _selectionCurrentX),
            Math.Max(_selectionStartY, _selectionCurrentY),
            lineColor,
            fillColor,
            0.72,
            1.0);
        _selectionRectangleVisible = true;
    }

    private void CompleteSelectionGesture()
    {
        var engine = _workspace.Engine;
        var startX = _selectionStartX;
        var startY = _selectionStartY;
        var endX = _selectionCurrentX;
        var endY = _selectionCurrentY;
        var modifiers = _selectionModifiers;
        var rectangleVisible = _selectionRectangleVisible;

        _selectionGesture = false;

        if (engine is not { IsInitialized: true })
        {
            _selectionRectangleVisible = false;
            return;
        }

        if (rectangleVisible)
        {
            // Hide the transient rectangle before clearing the visibility
            // state. HideSelectionRectangle() intentionally checks this flag.
            HideSelectionRectangle(engine);
            var allowOverlap = endX < startX;
            var objects = engine.QueryRectangle(
                startX,
                startY,
                endX,
                endY,
                allowOverlap);
            var entities = objects
                .Select(_workspace.Document.FindByViewerObject)
                .OfType<CadEntity>()
                .Distinct()
                .ToArray();
            ApplyEntitySelection(
                entities,
                SelectionOperation(modifiers));
            return;
        }

        var entity =
            _workspace.Preselection.Current?.Entity;
        ApplyEntitySelection(
            entity is null
                ? Array.Empty<CadEntity>()
                : [entity],
            SelectionOperation(modifiers),
            entity);
    }

    private void ApplyEntitySelection(
        IReadOnlyList<CadEntity> entities,
        CadSelectionOperation operation,
        CadEntity? primary = null)
    {
        _workspace.Selection.Apply(
            entities,
            operation,
            primary);
    }

    private void CancelSelectionGesture()
    {
        if (_workspace.Engine is { IsInitialized: true } engine)
            HideSelectionRectangle(engine);

        _selectionGesture = false;
        _selectionRectangleVisible = false;
    }

    private void HideSelectionRectangle(
        OcctEngine engine)
    {
        if (!_selectionRectangleVisible)
            return;

        try
        {
            engine.HideSelectionRectangle();
            engine.Redraw();
        }
        catch (Exception exception)
            when (IsRecoverableSelectionFailure(exception))
        {
        }
        finally
        {
            _selectionRectangleVisible = false;
        }
    }

    private static CadSelectionOperation SelectionOperation(
        OcctInputModifiers modifiers)
    {
        var control =
            (modifiers & OcctInputModifiers.Control) != 0;
        var shift =
            (modifiers & OcctInputModifiers.Shift) != 0;

        return (control, shift) switch
        {
            (false, false) => CadSelectionOperation.Replace,
            (true, false) => CadSelectionOperation.Add,
            (false, true) => CadSelectionOperation.Remove,
            (true, true) => CadSelectionOperation.Toggle
        };
    }

    private static bool IsRecoverableSelectionFailure(
        Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private void PublishLastCoordinate()
    {
        if (_workspace.LastResolvedPoint is { } resolved)
            PublishCoordinate(resolved);
    }

    private void PublishCoordinate(CadResolvedPoint value) =>
        CoordinateChanged?.Invoke(
            this,
            new CadCoordinateChangedEventArgs(value));
}
