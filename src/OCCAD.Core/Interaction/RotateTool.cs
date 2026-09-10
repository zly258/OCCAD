using OcctNet;

namespace OCCAD;

public sealed class RotateTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _basePoint;
    private OcctVector3d _referenceAxis;
    private OcctVector3d _rotationAxis;
    private OcctPoint3d _initialPlaneOrigin;
    private OcctVector3d _initialPlaneX;
    private OcctVector3d _initialPlaneY;

    public override string Id => "rotate";
    public override string DisplayName => "Rotate";
    public override string PrecisionAngleLabel => "Rotation Angle";
    public override OcctPoint3d? PrecisionReferencePoint => _basePoint;

    protected override bool SuppressSourcesDuringPreview => true;
    protected override bool CanCommitCurrentStageCore => true;
    protected override bool CanStepBackCore => _basePoint is not null;

    public override bool CanCommitCurrentStage =>
        _basePoint is not null &&
        IsActive &&
        State == CadToolState.Drawing &&
        Entities.Count > 0 &&
        Context.Workspace.Drafting.AngleLockEnabled
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnTransformStarted()
    {
        _basePoint = null;
        CaptureInitialWorkPlane();
        _referenceAxis = Context.WorkPlane.XAxis;
        _rotationAxis = Context.WorkPlane.Normal;
        SetStageLocalized(
            0,
            "Cad.Prompt.rotate.Base",
            "Rotate: specify base point [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect ||
            Entities.Count == 0)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved &&
            _basePoint is { } basePoint)
        {
            UpdatePreview(
                Context.ResolvePoint(
                    input.X,
                    input.Y,
                    basePoint).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                _basePoint).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (State != CadToolState.Drawing)
            return false;

        if (_basePoint is not null &&
            Context.Workspace.Drafting.AngleLockEnabled)
        {
            return CommitAngle(
                Context.Workspace.Drafting.LockedAngleDegrees);
        }

        return AcceptPoint(
            Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                _basePoint).Point);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        Entities.Count > 0 &&
        AcceptPoint(point);

    protected override bool OnStepBack()
    {
        if (_basePoint is null)
            return false;

        _basePoint = null;
        ClearTransformPreview();
        RestoreInitialWorkPlane();
        _referenceAxis = Context.WorkPlane.XAxis;
        _rotationAxis = Context.WorkPlane.Normal;
        SetStageLocalized(
            0,
            "Cad.Prompt.rotate.Base",
            "Rotate: specify base point [Esc cancel]");
        return true;
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_basePoint is null)
            return input.AngleDegrees is null;

        if (Context.Workspace.Drafting.AngleLockEnabled)
        {
            ShowAnglePreview(
                Context.Workspace.Drafting.LockedAngleDegrees);
            return true;
        }

        if (Context.Workspace.LastPointerPosition is { } pointer)
            RefreshPreviewFromLastPointer(pointer);
        return true;
    }

    protected internal override void RefreshPreviewFromLastPointer(
        CadPointerPosition pointer)
    {
        if (_basePoint is not { } basePoint)
            return;

        if (Context.Workspace.Drafting.AngleLockEnabled)
        {
            ShowAnglePreview(
                Context.Workspace.Drafting.LockedAngleDegrees);
            return;
        }

        UpdatePreview(
            Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                basePoint).Point);
    }

    protected override void ResetTransformState()
    {
        _basePoint = null;
        _referenceAxis = default;
        _rotationAxis = default;
        _initialPlaneOrigin = default;
        _initialPlaneX = default;
        _initialPlaneY = default;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        if (_basePoint is null)
        {
            _basePoint = point;
            SetWorkPlane(
                point,
                _initialPlaneX,
                _initialPlaneY,
                lockPlane: false);
            _referenceAxis = Context.WorkPlane.XAxis;
            _rotationAxis = Context.WorkPlane.Normal;
            ClearTransformPreview();
            SetStageLocalized(
                1,
                "Cad.Prompt.rotate.TargetAngle",
                "Rotate: specify rotation angle [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Angle);
            return true;
        }

        if (!TryAngle(point, out var angleDegrees))
        {
            ClearTransformPreview();
            return false;
        }

        return CommitAngle(angleDegrees);
    }

    private void UpdatePreview(OcctPoint3d target)
    {
        if (_basePoint is null ||
            !TryAngle(target, out var angleDegrees))
        {
            ClearTransformPreview();
            return;
        }

        ShowAnglePreview(angleDegrees);
    }

    private void ShowAnglePreview(double angleDegrees)
    {
        if (_basePoint is null ||
            !double.IsFinite(angleDegrees) ||
            Math.Abs(NormalizeAngle(angleDegrees)) <= 1e-10)
        {
            ClearTransformPreview();
            return;
        }

        ShowEntityPreview(
            entity => entity.RotatePlacement(
                _basePoint.Value,
                _rotationAxis,
                angleDegrees));
    }

    private bool CommitAngle(double angleDegrees)
    {
        if (_basePoint is null ||
            !double.IsFinite(angleDegrees) ||
            Math.Abs(NormalizeAngle(angleDegrees)) <= 1e-10)
        {
            ClearTransformPreview();
            return false;
        }

        CommitTransform(() => Context.Workspace.RotateEntities(
            Entities,
            _basePoint.Value,
            _rotationAxis,
            angleDegrees));
        return true;
    }

    private bool TryAngle(
        OcctPoint3d point,
        out double angleDegrees)
    {
        angleDegrees = 0.0;
        if (_basePoint is not { } center ||
            !_rotationAxis.TryNormalize(out var normal) ||
            !_referenceAxis.TryNormalize(out var reference))
            return false;

        var delta = point - center;
        var planar = delta - normal * delta.Dot(normal);
        if (!planar.TryNormalize(out var direction))
            return false;

        var cross = reference.Cross(direction);
        var sin = normal.Dot(cross);
        var cos = Math.Clamp(reference.Dot(direction), -1.0, 1.0);
        angleDegrees = Math.Atan2(sin, cos) * 180.0 / Math.PI;
        return double.IsFinite(angleDegrees);
    }

    private void CaptureInitialWorkPlane()
    {
        _initialPlaneOrigin = Context.WorkPlane.Origin;
        _initialPlaneX = Context.WorkPlane.XAxis;
        _initialPlaneY = Context.WorkPlane.YAxis;
    }

    private void RestoreInitialWorkPlane() =>
        SetWorkPlane(
            _initialPlaneOrigin,
            _initialPlaneX,
            _initialPlaneY,
            lockPlane: false);

    private static double NormalizeAngle(double angleDegrees)
    {
        var value = angleDegrees % 360.0;
        if (value > 180.0) value -= 360.0;
        if (value <= -180.0) value += 360.0;
        return value;
    }
}
