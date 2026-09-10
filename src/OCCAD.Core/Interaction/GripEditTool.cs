using OcctNet;

namespace OCCAD;

public sealed class GripEditTool : CadTool
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
            SetWorkPlane(plane.Origin, plane.XAxis, plane.YAxis, plane.LockPlane);
            if (plane.LockedAngleDegrees is { } angle)
                LockStageAngle(angle);
        }
        else
        {
            Context.WorkPlane.SetOrigin(_grip.Position);
        }

        Context.Preview.Show(_preview);
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (_preview is null) return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            TryMovePreview(Context.ResolvePoint(input.X, input.Y, ConstraintOrigin).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed) return false;

        if (input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Button != OcctPointerButton.Left) return false;

        return AcceptPoint(Context.ResolvePoint(input.X, input.Y, ConstraintOrigin).Point);
    }

    protected override bool CanCommitCurrentStageCore => true;

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (_preview is null)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(pointer.X, pointer.Y, ConstraintOrigin).Point);
    }

    protected override void OnCanceled()
    {
        if (_before is null) return;
        try
        {
            _grip.Entity.RestoreGeometry(_before);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (_preview is null || _before is null || !point.IsFinite)
            return true;

        if (!TryMovePreview(point))
            return true;

        if (Context.Workspace.Entities.GeometryEquals(_before, _preview))
            return true;

        var actualBefore = _grip.Entity.Duplicate();
        try
        {
            _grip.Entity.RestoreGeometry(_preview);
            Context.Workspace.RecordGeometryChange(
                _grip.Entity,
                _before,
                "Grip Edit");
            Context.Workspace.Tools.CompleteCurrent();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            try
            {
                _grip.Entity.RestoreGeometry(actualBefore);
            }
            catch (Exception restoreException) when (IsRecoverable(restoreException))
            {
            }

            ShowInvalidGripPrompt();
        }

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
                _preview.RestoreGeometry(_before);
            _preview.MoveGrip(_grip.Index, point);
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
                _preview.RestoreGeometry(snapshot);
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

    private void SetGripPrompt(CadPrecisionInputKind precisionInputs)
    {
        if (_grip.Kind is CadGripKind.Center or CadGripKind.Control || _grip.Index == 0)
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.grip.Move",
                "Grip Edit: move - specify new point [Esc cancel]",
                precisionInputs);
            return;
        }

        SetStageLocalized(
            0,
            "Cad.Prompt.grip.Index",
            "Grip Edit: grip {0} - specify new point [Esc cancel]",
            precisionInputs,
            _grip.Index);
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
