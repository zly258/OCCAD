using OcctNet;

namespace OCCAD;

public sealed class RotateTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _center;
    private OcctVector3d? _reference;
    private OcctVector3d _normal;
    private CadPlaneFrame _initialPlane;
    public override string Id => "rotate";
    public override string DisplayName => "Rotate";
    protected override bool SuppressSourcesDuringPreview => true;
    public override OcctPoint3d? PrecisionReferencePoint => _center;
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing;
    protected override bool CanStepBackCore => _center is not null;
    protected override bool CanFinishCore => _reference is not null && Context.Workspace.Drafting.AngleLockEnabled;

    protected override void OnTransformStarted()
    {
        _initialPlane =
            Context.WorkPlane.EffectivePlane;
        _normal = _initialPlane.Normal;
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
            var yAxis =
                _normal.Cross(direction).Normalized();
            SetWorkPlane(
                _center.Value,
                direction,
                yAxis,
                lockPlane: true);
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
            ShowEntityPreview(entity => entity.RotatePlacement(_center!.Value, _normal, angle));
        else ClearTransformPreview();
        NotifyUpdated();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinish) return false;
        Commit(Context.Workspace.Drafting.LockedAngleDegrees);
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_reference is not null)
        {
            _reference = null;
            SetWorkPlane(
                _center!.Value,
                _initialPlane.XAxis,
                _initialPlane.YAxis,
                lockPlane: false);
        }
        else
        {
            _center = null;
            SetWorkPlane(
                _initialPlane.Origin,
                _initialPlane.XAxis,
                _initialPlane.YAxis,
                lockPlane: false);
        }
        ClearTransformPreview();
        RestorePrompt();
        return true;
    }

    protected override void ResetTransformState()
    {
        _center = null;
        _reference = null;
        _initialPlane = default;
    }

    private bool Direction(OcctPoint3d point, out OcctVector3d direction)
    {
        var delta = point - _center!.Value;
        return (delta - _normal * delta.Dot(_normal)).TryNormalize(out direction);
    }

    private bool Angle(OcctPoint3d point, out double angle)
    {
        angle = Context.Workspace.Drafting.LockedAngleDegrees;
        if (Context.Workspace.Drafting.AngleLockEnabled) return true;
        if (!Direction(point, out var target)) return false;
        var reference = _reference!.Value;
        angle = Math.Atan2(_normal.Dot(reference.Cross(target)), reference.Dot(target)) * 180.0 / Math.PI;
        return true;
    }

    private void Preview(OcctPoint3d point)
    {
        if (Angle(point, out var angle))
            ShowEntityPreview(entity => entity.RotatePlacement(_center!.Value, _normal, angle));
        else ClearTransformPreview();
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
