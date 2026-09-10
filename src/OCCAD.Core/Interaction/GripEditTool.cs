using System.Drawing;
using OcctNet;

namespace OCCAD;

public sealed class GripEditTool : CadTool, ICadPointInputTool
{
    private readonly CadGripPoint _grip;
    private CadEntity? _before;
    private CadEntity? _preview;
    private OcctPoint? _dragMarker;
    private byte[]? _dragMarkerPixels;
    private bool _invalidGrip;
    private CadPrecisionInputKind _effectivePrecisionInputs;

    public GripEditTool(CadGripPoint grip)
    {
        _grip = grip;
    }

    public override string Id => "grip";
    public override string DisplayName => "Grip Edit";

    public override OcctPoint3d? PrecisionReferencePoint => ConstraintOrigin;

    private OcctPoint3d ConstraintOrigin =>
        _grip.ConstraintOrigin ?? _grip.Position;

    protected override void OnActivated()
    {
        _before = Context.Workspace.CaptureGeometry(_grip.Entity);
        _preview = _grip.Entity.Duplicate();

        _effectivePrecisionInputs = _grip.PrecisionInputs;
        if (_grip.WorkPlane?.LockedAngleDegrees is not null)
            _effectivePrecisionInputs &= ~CadPrecisionInputKind.Angle;

        SetGripPrompt(_effectivePrecisionInputs);

        if (_grip.WorkPlane is { } plane)
        {
            Context.WorkPlane.SetGripPlane(
                plane.Origin,
                plane.XAxis,
                plane.YAxis,
                plane.LockPlane);
            if (plane.LockedAngleDegrees is { } angle)
                LockStageAngle(angle);
        }
        else
        {
            Context.WorkPlane.SetGripPlane(
                _grip.Position,
                Context.WorkPlane.XAxis,
                Context.WorkPlane.YAxis);
        }

        ShowReplacementPreview([_grip.Entity], [_preview]);
        ShowDragMarker(_grip.Position);
    }

    protected override void OnDeactivated()
    {
        ClearDragMarker();
        ClearReplacementPreview();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (_preview is null) return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            var point = Context.ResolvePoint(
                input.X,
                input.Y,
                ConstraintOrigin).Point;
            if (TryMovePreview(point))
                UpdateDragMarkerFromPreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed) return false;

        if (input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Button != OcctPointerButton.Left) return false;

        _ = TryCommitPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                ConstraintOrigin).Point);
        return true;
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        TryCommitPoint(point);

    protected override bool CanCommitCurrentStageCore => true;

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (_preview is null)
            return false;

        return TryCommitPoint(
            Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                ConstraintOrigin).Point);
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_preview is not null &&
            Context.Workspace.LastPointerPosition is { } pointer)
        {
            RefreshPreviewFromLastPointer(pointer);
        }
        return true;
    }

    protected internal override void RefreshPreviewFromLastPointer(
        CadPointerPosition pointer)
    {
        if (_preview is null)
            return;

        var point = Context.ResolvePoint(
            pointer.X,
            pointer.Y,
            ConstraintOrigin).Point;
        if (TryMovePreview(point))
            UpdateDragMarkerFromPreview();
    }

    private bool TryCommitPoint(OcctPoint3d point)
    {
        if (_preview is null ||
            _before is null ||
            !point.IsFinite)
            return false;

        if (!TryMovePreview(point))
            return false;

        UpdateDragMarkerFromPreview();

        if (Context.Workspace.Entities.GeometryEquals(
                _before,
                _preview))
            return false;

        var workspace = Context.Workspace;
        var actualBefore = _grip.Entity.Duplicate();
        var wasModified = workspace.IsModified;
        var historyState = workspace.History.CurrentStateId;
        Exception? failure = null;

        using (workspace.Document.BeginChangeSet())
        {
            try
            {
                _grip.Entity.RestoreGeometrySnapshot(_preview);
                workspace.RecordGeometryChange(
                    _grip.Entity,
                    _before,
                    "Grip Edit");
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failure = exception;
                try
                {
                    _grip.Entity.RestoreGeometrySnapshot(actualBefore);
                }
                catch (Exception restoreException) when (IsRecoverable(restoreException))
                {
                    failure = new AggregateException(
                        "Grip commit and rollback both failed.",
                        exception,
                        restoreException);
                }
            }
        }

        if (failure is not null)
        {
            if (!wasModified &&
                workspace.History.CurrentStateId == historyState)
                workspace.MarkSaved();

            ShowInvalidGripPrompt();
            return true;
        }

        workspace.Tools.CompleteCurrent();
        return true;
    }

    private bool TryMovePreview(OcctPoint3d point)
    {
        if (_preview is null || !point.IsFinite)
        {
            ShowInvalidGripPrompt();
            return false;
        }

        CadEntity snapshot;
        try
        {
            snapshot = _preview.Duplicate();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            ShowInvalidGripPrompt();
            return false;
        }

        try
        {
            if (_before is not null)
                _preview.RestoreGeometrySnapshot(_before);
            _preview.MoveWorldGrip(_grip.Index, point);
            Context.Preview.Update(_preview);
            if (_invalidGrip)
            {
                _invalidGrip = false;
                SetGripPrompt(_effectivePrecisionInputs);
            }
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            try
            {
                _preview.RestoreGeometrySnapshot(snapshot);
                Context.Preview.Update(_preview);
            }
            catch (Exception restoreException) when (IsRecoverable(restoreException))
            {
                _preview = snapshot;
                try
                {
                    Context.Preview.Update(_preview);
                }
                catch (Exception previewException) when (IsRecoverable(previewException))
                {
                }
            }

            ShowInvalidGripPrompt();
            return false;
        }
    }

    private void ShowDragMarker(OcctPoint3d point)
    {
        if (!point.IsFinite ||
            Context.Workspace.Engine is not { IsInitialized: true } engine)
            return;

        ClearDragMarker();

        var size = Math.Min(35, Context.Workspace.Grips.HotMarkerSize + 2);
        _dragMarkerPixels ??= CreateCircularMarkerPixels(
            size,
            Color.FromArgb(255, 245, 178, 35));

        var createdMarker = engine.AddPointPixmap(
            point,
            size,
            size,
            _dragMarkerPixels);

        _dragMarker = createdMarker;
        engine.SetObjectSelectable(createdMarker, false);
        engine.SetDisplayPriority(createdMarker, 10);
    }

    private void UpdateDragMarker(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return;

        var engine = Context.Workspace.Engine;
        if (engine is not { IsInitialized: true })
            return;

        if (_dragMarker is not { } marker ||
            !engine.ContainsObject(marker.Id))
        {
            ShowDragMarker(point);
            return;
        }

        engine.UpdatePoints([
            new OcctPointStateUpdate(
                marker,
                point,
                true)
        ]);
    }

    private void UpdateDragMarkerFromPreview()
    {
        if (_preview is null)
            return;

        var grips = _preview.GetWorldGripPoints();
        for (var index = 0; index < grips.Count; index++)
        {
            if (grips[index].Index != _grip.Index)
                continue;

            UpdateDragMarker(grips[index].Position);
            return;
        }
    }

    private void ClearDragMarker()
    {
        var marker = _dragMarker;
        _dragMarker = null;
        if (marker is not { } existingMarker ||
            Context.Workspace.Engine is not { IsInitialized: true } engine ||
            !engine.ContainsObject(existingMarker.Id))
            return;

        try
        {
            engine.Delete(existingMarker);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }
    }

    private static byte[] CreateCircularMarkerPixels(
        int size,
        Color color)
    {
        var pixels = new byte[size * size * 4];
        var center = size / 2;
        var radius = Math.Max(2, center - 1);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                if (dx * dx + dy * dy > radius * radius)
                    continue;

                var offset = (y * size + x) * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        return pixels;
    }

    private void SetGripPrompt(
        CadPrecisionInputKind precisionInputs)
    {
        var (key, fallback) = _grip.Kind switch
        {
            CadGripKind.Vertex =>
                ("Cad.Prompt.grip.Vertex",
                 "Grip Edit: move vertex [Esc cancel]"),
            CadGripKind.Midpoint =>
                ("Cad.Prompt.grip.Midpoint",
                 "Grip Edit: move midpoint [Esc cancel]"),
            CadGripKind.Center =>
                ("Cad.Prompt.grip.Center",
                 "Grip Edit: move center [Esc cancel]"),
            CadGripKind.Radius =>
                ("Cad.Prompt.grip.Radius",
                 "Grip Edit: change radius [Esc cancel]"),
            CadGripKind.Axis =>
                ("Cad.Prompt.grip.Axis",
                 "Grip Edit: change axis [Esc cancel]"),
            CadGripKind.Height =>
                ("Cad.Prompt.grip.Height",
                 "Grip Edit: change height [Esc cancel]"),
            _ =>
                ("Cad.Prompt.grip.Move",
                 "Grip Edit: move - specify new point [Esc cancel]")
        };

        SetStageLocalized(
            0,
            key,
            fallback,
            precisionInputs);
    }

    private void ShowInvalidGripPrompt()
    {
        if (_invalidGrip) return;
        _invalidGrip = true;
        SetPromptLocalized(
            "Cad.Prompt.grip.Invalid",
            "Grip Edit: invalid position; choose another point [Esc cancel]",
            _effectivePrecisionInputs);
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
