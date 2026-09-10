using OcctNet;

namespace OCCAD;

public sealed class RotateTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _center;
    private OcctVector3d? _reference;
    private OcctVector3d _normal;
    public override string Id => "rotate";
    public override string DisplayName => "Rotate";
    public override OcctPoint3d? PrecisionReferencePoint => _center;
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing;
    protected override bool CanStepBackCore => _center is not null;
    protected override bool CanFinishCore => _reference is not null && Context.WorkPlane.AngleLockEnabled;

    protected override void OnTransformStarted()
    {
        _normal = Context.WorkPlane.Normal;
        RestorePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (State != CadToolState.Drawing) return false;
        if (input.Kind == OcctPointerInputKind.Moved && _reference is not null)
        {
            Preview(Context.ResolvePoint(input.X, input.Y, _center).Point);
            return true;
        }
        return input.Kind == OcctPointerInputKind.Pressed && input.Button == OcctPointerButton.Left &&
            TryAcceptPoint(Context.ResolvePoint(input.X, input.Y, _center).Point);
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive || State != CadToolState.Drawing || !point.IsFinite) return false;
        if (_center is null)
        {
            _center = point;
            Context.WorkPlane.SetOrigin(point);
            Context.WorkPlane.SetToolPlaneFixed(true);
            RestorePrompt();
            return true;
        }
        if (_reference is null)
        {
            if (!Direction(point, out var direction)) return false;
            _reference = direction;
            RestorePrompt();
            return true;
        }
        if (!Angle(point, out var angle)) return false;
        Commit(angle);
        return true;
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y, _center).Point);

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_reference is null) return false;
        if (input.AngleDegrees is { } angle)
            ShowEntityPreview(entity => entity.Rotate(_center!.Value, _normal, angle));
        else Context.Preview.Clear();
        NotifyUpdated();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinish) return false;
        Commit(Context.WorkPlane.LockedAngleDegrees);
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_reference is not null) _reference = null;
        else { _center = null; Context.WorkPlane.SetToolPlaneFixed(false); }
        Context.Preview.Clear();
        RestorePrompt();
        return true;
    }

    protected override void ResetTransformState() { _center = null; _reference = null; }

    private bool Direction(OcctPoint3d point, out OcctVector3d direction)
    {
        var delta = point - _center!.Value;
        return (delta - _normal * delta.Dot(_normal)).TryNormalize(out direction);
    }

    private bool Angle(OcctPoint3d point, out double angle)
    {
        angle = Context.WorkPlane.LockedAngleDegrees;
        if (Context.WorkPlane.AngleLockEnabled) return true;
        if (!Direction(point, out var target)) return false;
        var reference = _reference!.Value;
        angle = Math.Atan2(_normal.Dot(reference.Cross(target)), reference.Dot(target)) * 180.0 / Math.PI;
        return true;
    }

    private void Preview(OcctPoint3d point)
    {
        if (Angle(point, out var angle))
            ShowEntityPreview(entity => entity.Rotate(_center!.Value, _normal, angle));
        else Context.Preview.Clear();
    }

    private void Commit(double angle)
    {
        Context.Workspace.RotateEntities(Entities, _center!.Value, _normal, angle);
        Context.Workspace.Tools.CompleteCurrent();
    }

    private void RestorePrompt()
    {
        if (_center is null) SetStageLocalized(0, "Cad.Prompt.rotate.Base", "Rotate: specify base point [Esc cancel]");
        else if (_reference is null) SetStageLocalized(1, "Cad.Prompt.rotate.Reference", "Rotate: specify reference direction [Backspace undo, Esc cancel]");
        else SetStageLocalized(2, "Cad.Prompt.rotate.TargetAngle", "Rotate: specify target direction or angle [Backspace undo, Esc cancel]", CadPrecisionInputKind.Angle);
    }
}
