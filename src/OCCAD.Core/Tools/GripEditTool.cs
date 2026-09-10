using OcctNet;

namespace OCCAD;

public sealed class GripEditTool : CadTool, ICadPointInputTool
{
    private readonly CadGripPoint _grip;
    private CadEntity? _before;
    private CadEntity? _preview;
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
            var axes = _grip.Entity.Placement.GetWorldAxes();
            Context.WorkPlane.SetGripPlane(
                _grip.Position,
                axes.XAxis,
                axes.YAxis,
                true);
        }

        ShowReplacementPreview([_grip.Entity], [_preview]);
        Context.Workspace.Grips.ShowDragMarker(_grip.Position);
    }

    protected override void OnDeactivated()
    {
        Context.Workspace.Grips.ClearDragMarker();
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
        try
        {
            CommitReplacementPreview(() => CadTransaction.ApplyEntities(workspace, [_grip.Entity],
                "Grip Edit", entity => entity.RestoreGeometrySnapshot(_preview), geometryOnly: true));
        }
        catch (Exception failure) when (IsRecoverable(failure))
        {
            ShowReplacementPreview([_grip.Entity], [_preview]);
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

    private void UpdateDragMarkerFromPreview()
    {
        if (_preview is null)
            return;

        var grips = _preview.GetWorldGripPoints();
        for (var index = 0; index < grips.Count; index++)
        {
            if (grips[index].Index != _grip.Index)
                continue;

            Context.Workspace.Grips.UpdateDragMarker(grips[index].Position);
            return;
        }
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
