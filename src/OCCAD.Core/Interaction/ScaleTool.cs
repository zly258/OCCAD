using OcctNet;

namespace OCCAD;

public sealed class ScaleTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _center;
    private double? _referenceLength;
    private OcctVector3d? _referenceDirection;
    private CadPlaneFrame _initialPlane;
    public override string Id => "scale";
    public override string DisplayName => "Scale";
    protected override bool SuppressSourcesDuringPreview => true;
    public override OcctPoint3d? PrecisionReferencePoint => _center;
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing;
    protected override bool CanStepBackCore => _center is not null;
    protected override bool CanFinishCore => _referenceLength is not null && Context.Workspace.Precision.Factor is not null;
    protected override void OnTransformStarted()
    {
        _initialPlane =
            Context.WorkPlane.EffectivePlane;
        RestorePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (State != CadToolState.Drawing) return false;
        if (input.Kind == OcctPointerInputKind.Moved && _referenceLength is not null)
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
        var length = point.DistanceTo(_center.Value);
        if (_referenceLength is null)
        {
            if (length <= 1e-9) return false;
            var vector =
                CadTransformMath.Between(
                    _center.Value,
                    point);
            if (!vector.TryNormalize(
                    out var direction))
                return false;

            _referenceLength = length;
            _referenceDirection = direction;

            var normal =
                _initialPlane.Normal;
            var planarDirection =
                new OcctVector3d(
                    direction.X -
                        normal.X *
                        CadTransformMath.Dot(
                            direction,
                            normal),
                    direction.Y -
                        normal.Y *
                        CadTransformMath.Dot(
                            direction,
                            normal),
                    direction.Z -
                        normal.Z *
                        CadTransformMath.Dot(
                            direction,
                            normal));
            if (!planarDirection.TryNormalize(
                    out var xAxis))
                xAxis = _initialPlane.XAxis;

            var yAxis =
                normal.Cross(xAxis).Normalized();
            SetWorkPlane(
                _center.Value,
                xAxis,
                yAxis,
                lockPlane: true);
            RestorePrompt();
            LockStageAngle(0.0);
            return true;
        }
        var factor = Factor(point);
        if (!double.IsFinite(factor) || factor <= 1e-9) return false;
        Commit(factor);
        return true;
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y, _center).Point);

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_referenceLength is null) return false;
        if (input.Factor is { } factor)
            ShowEntityPreview(entity => entity.ScaleFromWorld(_center!.Value, factor));
        else ClearTransformPreview();
        NotifyUpdated();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinish) return false;
        Commit(Context.Workspace.Precision.Factor!.Value);
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_referenceLength is not null)
        {
            _referenceLength = null;
            _referenceDirection = null;
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
        _referenceLength = null;
        _referenceDirection = null;
        _initialPlane = default;
    }
    private double Factor(OcctPoint3d point) => Context.Workspace.Precision.Factor ?? point.DistanceTo(_center!.Value) / _referenceLength!.Value;

    private void Preview(OcctPoint3d point)
    {
        var factor = Factor(point);
        if (double.IsFinite(factor) && factor > 1e-9)
            ShowEntityPreview(entity => entity.ScaleFromWorld(_center!.Value, factor));
        else ClearTransformPreview();
    }

    private void Commit(double factor)
    {
        Context.Workspace.ScaleEntities(Entities, _center!.Value, factor);
        Context.Workspace.Tools.CompleteCurrent();
    }

    private void RestorePrompt()
    {
        if (_center is null) SetStageLocalized(0, "Cad.Prompt.scale.Base", "Scale: specify base point [Esc cancel]");
        else if (_referenceLength is null) SetStageLocalized(1, "Cad.Prompt.scale.Reference", "Scale: specify reference distance [Backspace undo, Esc cancel]");
        else SetStageLocalized(2, "Cad.Prompt.scale.Target", "Scale: specify target distance or factor [Backspace undo, Esc cancel]", CadPrecisionInputKind.Factor);
    }
}
