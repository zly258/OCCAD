using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OcctNet;

namespace OCCAD.Avalonia;

internal sealed class CadCoordinateChangedEventArgs(CadResolvedPoint value) : EventArgs
{
    public CadResolvedPoint Value { get; } = value;
}

/// <summary>
/// Keeps CAD interaction policy outside MainWindow. Navigation stays in the
/// reusable OCCT viewport; this controller owns CAD selection, drafting input
/// and tool routing only. Idle application commands remain a shell concern.
/// </summary>
internal sealed class CadViewportController : IDisposable
{
    private const int CursorSize = 32;
    private const int CursorCenter = CursorSize / 2;
    private static readonly WriteableBitmap DrawingCursorBitmap = CreateDrawingCursorBitmap();
    private static readonly Cursor DrawingCursor = new(
        DrawingCursorBitmap,
        new PixelPoint(CursorCenter, CursorCenter));
    private static readonly Cursor NavigationCursor = new(StandardCursorType.SizeAll);

    private readonly CadWorkspace _workspace;
    private readonly OcctAvaloniaViewport _viewport;
    private readonly CadPointerMoveScheduler _pointerMoves;
    private readonly CadSelectionWindow _selectionWindow;
    private bool _shiftMiddleRotating;
    private bool _middleNavigating;
    private bool _selectionGesture;
    private int _selectionStartX;
    private int _selectionStartY;
    private int _selectionCurrentX;
    private int _selectionCurrentY;
    private OcctInputModifiers _selectionModifiers;
    private bool _disposed;

    public CadViewportController(
        CadWorkspace workspace,
        OcctAvaloniaViewport viewport,
        Dispatcher dispatcher)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _selectionWindow = new CadSelectionWindow(_workspace);
        _pointerMoves = new CadPointerMoveScheduler(
            dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)),
            ProcessPointer);

        _viewport.PreviewPointerInput += PreviewPointerInput;
        _viewport.PreviewKeyInput += PreviewKeyInput;
        _viewport.HoverHitChanged += HoverHitChanged;
        _viewport.PointerExited += PointerExited;
        _workspace.Tools.ToolChanged += ToolChanged;
        _workspace.Tools.ToolUpdated += ToolUpdated;
        RefreshCursor();
    }

    public event EventHandler<CadCoordinateChangedEventArgs>? CoordinateChanged;
    public event EventHandler? CoordinateCleared;
    public event EventHandler? InteractionChanged;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _selectionWindow.AttachEngine(engine);
        engine.SetAutomaticHighlight(
            _workspace.Tools.ActiveTool?.InteractionPolicy.PreselectionEnabled != false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _viewport.PreviewPointerInput -= PreviewPointerInput;
        _viewport.PreviewKeyInput -= PreviewKeyInput;
        _viewport.HoverHitChanged -= HoverHitChanged;
        _viewport.PointerExited -= PointerExited;
        _workspace.Tools.ToolChanged -= ToolChanged;
        _workspace.Tools.ToolUpdated -= ToolUpdated;
        CancelSelectionGesture();
        _selectionWindow.Dispose();
        _pointerMoves.Dispose();
        _viewport.Cursor = Cursor.Default;
    }

    private void PreviewPointerInput(object? sender, OcctPointerInputEventArgs input)
    {
        if (input.Handled || _workspace.Engine is null)
            return;

        UpdateNavigationCursor(input);
        if (TryFit(input) || TryRightButton(input) || TryShiftMiddleRotate(input))
            return;

        if (IsNavigationInput(input))
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

    private bool TryFit(OcctPointerInputEventArgs input)
    {
        if (input.Kind != OcctPointerInputKind.DoubleClicked ||
            input.Button != OcctPointerButton.Middle ||
            _workspace.Engine is not { IsInitialized: true } engine)
            return false;

        input.Handled = true;
        _pointerMoves.Clear();
        _shiftMiddleRotating = false;
        engine.FitAll();
        RefreshDrawingPointer(OcctInputModifiers.None);
        return true;
    }

    private bool TryRightButton(OcctPointerInputEventArgs input)
    {
        if (_workspace.Tools.ActiveTool is null)
            return false;

        var right = input.Button == OcctPointerButton.Right ||
                    (input.Buttons & OcctPointerButtons.Right) != 0;
        if (!right)
            return false;

        input.Handled = true;
        _pointerMoves.Flush();
        if (input.Kind == OcctPointerInputKind.Pressed)
            _workspace.Tools.HandleSecondaryAction();
        return true;
    }

    private bool TryShiftMiddleRotate(OcctPointerInputEventArgs input)
    {
        if (_workspace.Engine is not { IsInitialized: true } engine)
            return false;

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Middle &&
            (input.Modifiers & OcctInputModifiers.Shift) != 0)
        {
            input.Handled = true;
            _pointerMoves.Clear();
            _shiftMiddleRotating = true;
            RefreshCursor();
            engine.StartRotation(input.X, input.Y);
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Moved && _shiftMiddleRotating)
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
            input.Handled = true;
            _shiftMiddleRotating = false;
            RefreshCursor();
            RefreshDrawingPointer(OcctInputModifiers.None);
            return true;
        }

        return false;
    }

    private static bool IsNavigationInput(OcctPointerInputEventArgs input) =>
        input.Kind == OcctPointerInputKind.Wheel ||
        input.Button == OcctPointerButton.Middle ||
        (input.Buttons & OcctPointerButtons.Middle) != 0;

    private void UpdateNavigationCursor(OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Middle)
            _middleNavigating = true;
        else if ((input.Kind == OcctPointerInputKind.Released &&
                  input.Button == OcctPointerButton.Middle) ||
                 (input.Kind == OcctPointerInputKind.Moved &&
                  _middleNavigating &&
                  (input.Buttons & OcctPointerButtons.Middle) == 0))
            _middleNavigating = false;

        RefreshCursor();
    }

    private void PreviewKeyInput(object? sender, OcctKeyInputEventArgs input)
    {
        if (input.Handled)
            return;

        if (TryDraftingShortcut(input) || TryWorkPlaneShortcut(input))
        {
            input.Handled = true;
            RefreshDrawingPointer(input.Modifiers);
            InteractionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key == OcctKey.Tab &&
            _workspace.Tools.ActiveTool is { State: CadToolState.Drawing } &&
            _workspace.Snap.Active)
        {
            var previous = (input.Modifiers & OcctInputModifiers.Shift) != 0;
            if (previous ? _workspace.Snap.CyclePrevious() : _workspace.Snap.CycleNext())
            {
                input.Handled = true;
                RefreshDrawingPointer(input.Modifiers);
                return;
            }
        }

        if (_workspace.Tools.HandleKey(input))
            input.Handled = true;
    }

    private bool TryDraftingShortcut(OcctKeyInputEventArgs input)
    {
        if (input.Kind != OcctKeyInputKind.Pressed || input.IsRepeat ||
            (input.Modifiers & (OcctInputModifiers.Control | OcctInputModifiers.Alt | OcctInputModifiers.Meta)) != 0)
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

    private bool TryWorkPlaneShortcut(OcctKeyInputEventArgs input)
    {
        if (_workspace.Tools.ActiveTool is not { State: CadToolState.Drawing } ||
            input.Kind != OcctKeyInputKind.Pressed || input.IsRepeat ||
            (input.Modifiers & (OcctInputModifiers.Control | OcctInputModifiers.Alt | OcctInputModifiers.Meta)) != 0)
            return false;

        var preset = input.Key switch
        {
            OcctKey.S => CadWorkPlanePreset.YZ,
            OcctKey.F => CadWorkPlanePreset.XZ,
            OcctKey.T => CadWorkPlanePreset.XY,
            _ => (CadWorkPlanePreset?)null
        };
        return preset is { } value && _workspace.Tools.TryChangeDrawingPlane(value);
    }

    private void ProcessPointer(OcctPointerInputEventArgs input)
    {
        if (_workspace.Engine is null)
            return;

        if (_selectionGesture && HandleSelectionGesture(input))
            return;

        if (_workspace.Tools.HandlePointer(input))
        {
            input.Handled = true;
            PublishLastCoordinate();
            return;
        }

        var tool = _workspace.Tools.ActiveTool;
        var commandSelection =
            tool is { State: CadToolState.WaitForSelect } &&
            tool.InteractionPolicy.SelectionEnabled;

        if (tool is null &&
            input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left &&
            _workspace.Grips.TryHit(input.X, input.Y, out var grip))
        {
            _workspace.Subobjects.Clear();
            _workspace.Tools.BeginGripEdit(grip);
            input.Handled = true;
            return;
        }

        if ((tool is null || commandSelection) &&
            input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left)
        {
            BeginSelectionGesture(input);
            input.Handled = true;
            return;
        }

        if (input.Kind != OcctPointerInputKind.Moved)
            return;

        if (tool is null)
            _workspace.Grips.UpdateHot(input.X, input.Y);

        try
        {
            PublishCoordinate(_workspace.ResolvePoint(input.X, input.Y));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void BeginSelectionGesture(OcctPointerInputEventArgs input)
    {
        CancelSelectionGesture();
        _selectionGesture = true;
        _selectionStartX = _selectionCurrentX = input.X;
        _selectionStartY = _selectionCurrentY = input.Y;
        _selectionModifiers = input.Modifiers;
    }

    private bool HandleSelectionGesture(OcctPointerInputEventArgs input)
    {
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
            CancelSelectionGesture();

        return false;
    }

    private void UpdateSelectionRectangle()
    {
        var threshold = Math.Max(3, _viewport.RectangleSelectionThreshold);
        if (Math.Abs(_selectionCurrentX - _selectionStartX) < threshold &&
            Math.Abs(_selectionCurrentY - _selectionStartY) < threshold)
        {
            _selectionWindow.Clear();
            return;
        }

        var crossing = _selectionCurrentX < _selectionStartX;
        _selectionWindow.Show(
            _selectionStartX,
            _selectionStartY,
            _selectionCurrentX,
            _selectionCurrentY,
            crossing);
    }

    private void CompleteSelectionGesture()
    {
        var engine = _workspace.Engine;
        var sx = _selectionStartX;
        var sy = _selectionStartY;
        var ex = _selectionCurrentX;
        var ey = _selectionCurrentY;
        var operation = SelectionOperation(_selectionModifiers);
        var rectangle = _selectionWindow.IsVisible;
        _selectionGesture = false;

        if (engine is not { IsInitialized: true })
        {
            _selectionWindow.Clear();
            return;
        }

        if (rectangle)
        {
            _selectionWindow.Clear();
            var crossing = ex < sx;
            if (_workspace.Selection.Scope == CadSelectionScope.Subobject)
            {
                engine.SelectRectangle(sx, sy, ex, ey, false, crossing);
                var hits = engine.GetSelectedHits().ToArray();
                engine.ClearSelection();
                if (operation == CadSelectionOperation.Replace)
                    _workspace.Subobjects.Clear();

                foreach (var hit in hits.Distinct())
                {
                    var entity = _workspace.Document.FindByViewerObject(hit.Owner);
                    if (entity is null || !hit.IsSubshape)
                        continue;

                    var reference = CadSubshapeReference.Create(
                        entity,
                        hit.SubshapeType,
                        hit.SubshapeIndex);
                    CadSubshapeReferenceResolver.TryGetRepresentativePoint(
                        engine,
                        entity,
                        reference,
                        out var point);
                    _workspace.Subobjects.Apply(
                        new CadSubobjectSelection(
                            entity,
                            hit.SubshapeType,
                            hit.SubshapeIndex,
                            point),
                        operation == CadSelectionOperation.Replace
                            ? CadSelectionOperation.Add
                            : operation);
                }
                return;
            }

            var entities = engine.QueryRectangle(sx, sy, ex, ey, crossing)
                .Select(_workspace.Document.FindByViewerObject)
                .OfType<CadEntity>()
                .Where(_workspace.Document.IsEntitySelectable)
                .Distinct()
                .ToArray();
            _workspace.Selection.Apply(entities, operation);
            return;
        }

        if (_workspace.Selection.Scope == CadSelectionScope.Subobject)
        {
            if (_workspace.Preselection.Current is { IsSubshape: true } hit)
                _workspace.Subobjects.Select(hit, operation);
            else if (operation == CadSelectionOperation.Replace)
                _workspace.Subobjects.Clear();
            return;
        }

        var entityHit = _workspace.Preselection.Current?.Entity;
        _workspace.Selection.Apply(
            entityHit is not null && _workspace.Document.IsEntitySelectable(entityHit)
                ? [entityHit]
                : Array.Empty<CadEntity>(),
            operation,
            entityHit);
    }

    private void HoverHitChanged(object? sender, OcctViewportHoverHitChangedEventArgs input)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is not null && !tool.InteractionPolicy.PreselectionEnabled)
        {
            _workspace.Preselection.Clear();
            return;
        }

        var entity = input.Hit is { } hit
            ? _workspace.Document.FindByViewerObject(hit.Owner)
            : null;
        _workspace.Preselection.Update(entity, input.Hit);
    }

    private void ToolChanged(object? sender, CadToolChangedEventArgs input)
    {
        _pointerMoves.Clear();
        CancelSelectionGesture();
        _workspace.ClearPointerObservation();
        _workspace.Preselection.Clear();
        CoordinateCleared?.Invoke(this, EventArgs.Empty);
        SynchronizeHighlight();
        RefreshCursor();
    }

    private void ToolUpdated(object? sender, CadToolChangedEventArgs input)
    {
        if (input.Tool?.InteractionPolicy.PreselectionEnabled == false)
            _workspace.Preselection.Clear();
        SynchronizeHighlight();
        RefreshCursor();
    }

    private void SynchronizeHighlight()
    {
        if (_workspace.Engine is not { IsInitialized: true } engine)
            return;
        engine.SetAutomaticHighlight(
            _workspace.Tools.ActiveTool?.InteractionPolicy.PreselectionEnabled != false);
    }

    private void PointerExited(object? sender, PointerEventArgs input)
    {
        _pointerMoves.Clear();
        CancelSelectionGesture();
        _workspace.ClearPointerObservation();
        _workspace.Preselection.Clear();
        CoordinateCleared?.Invoke(this, EventArgs.Empty);
        _viewport.Cursor = Cursor.Default;
    }

    private void CancelSelectionGesture()
    {
        _selectionWindow.Clear();
        _selectionGesture = false;
    }

    private void RefreshDrawingPointer(OcctInputModifiers modifiers)
    {
        if (_workspace.LastPointerPosition is not { } pointer)
            return;
        ProcessPointer(new OcctPointerInputEventArgs(
            OcctPointerInputKind.Moved,
            OcctPointerButton.None,
            OcctPointerButtons.None,
            pointer.X,
            pointer.Y,
            0,
            modifiers));
    }

    private void RefreshCursor()
    {
        _viewport.Cursor = _middleNavigating || _shiftMiddleRotating
            ? NavigationCursor
            : _workspace.Tools.ActiveTool is { State: CadToolState.Drawing } tool &&
              tool.CurrentStep.RequiresPointer
                ? DrawingCursor
                : Cursor.Default;
    }

    private static WriteableBitmap CreateDrawingCursorBitmap()
    {
        const int bytesPerPixel = 4;
        var pixels = new byte[CursorSize * CursorSize * bytesPerPixel];

        for (var index = 2; index < CursorSize - 2; index++)
        {
            if (Math.Abs(index - CursorCenter) <= 4)
                continue;

            SetCursorPixel(pixels, index, CursorCenter);
            SetCursorPixel(pixels, index, CursorCenter + 1);
            SetCursorPixel(pixels, CursorCenter, index);
            SetCursorPixel(pixels, CursorCenter + 1, index);
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(CursorSize, CursorSize),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var framebuffer = bitmap.Lock();
        var sourceRowBytes = CursorSize * bytesPerPixel;
        for (var y = 0; y < CursorSize; y++)
        {
            Marshal.Copy(
                pixels,
                y * sourceRowBytes,
                IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes),
                sourceRowBytes);
        }
        return bitmap;
    }

    private static void SetCursorPixel(byte[] pixels, int x, int y)
    {
        var offset = (y * CursorSize + x) * 4;
        pixels[offset] = 240;
        pixels[offset + 1] = 240;
        pixels[offset + 2] = 240;
        pixels[offset + 3] = 255;
    }

    private static CadSelectionOperation SelectionOperation(OcctInputModifiers modifiers)
    {
        var control = (modifiers & OcctInputModifiers.Control) != 0;
        var shift = (modifiers & OcctInputModifiers.Shift) != 0;
        return (control, shift) switch
        {
            (false, false) => CadSelectionOperation.Replace,
            (true, false) => CadSelectionOperation.Add,
            (false, true) => CadSelectionOperation.Remove,
            _ => CadSelectionOperation.Toggle
        };
    }

    private void PublishLastCoordinate()
    {
        if (_workspace.LastResolvedPoint is { } value)
            PublishCoordinate(value);
    }

    private void PublishCoordinate(CadResolvedPoint value) =>
        CoordinateChanged?.Invoke(this, new CadCoordinateChangedEventArgs(value));
}
