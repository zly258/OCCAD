using OcctNet;

namespace OCCAD;

public sealed class ScaleTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _center;
    private double? _referenceLength;
    public override string Id => "scale";
    public override string DisplayName => "Scale";
    public override OcctPoint3d? PrecisionReferencePoint => _center;
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing;
    protected override bool CanStepBackCore => _center is not null;
    protected override bool CanFinishCore => _referenceLength is not null && Context.Workspace.Precision.Factor is not null;
    protected override void OnTransformStarted() => RestorePrompt();

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
            _referenceLength = length;
            RestorePrompt();
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
        else Context.Preview.Clear();
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
        if (_referenceLength is not null) _referenceLength = null;
        else { _center = null; Context.WorkPlane.SetToolPlaneFixed(false); }
        Context.Preview.Clear();
        RestorePrompt();
        return true;
    }

    protected override void ResetTransformState() { _center = null; _referenceLength = null; }
    private double Factor(OcctPoint3d point) => Context.Workspace.Precision.Factor ?? point.DistanceTo(_center!.Value) / _referenceLength!.Value;

    private void Preview(OcctPoint3d point)
    {
        var factor = Factor(point);
        if (double.IsFinite(factor) && factor > 1e-9)
            ShowEntityPreview(entity => entity.ScaleFromWorld(_center!.Value, factor));
        else Context.Preview.Clear();
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
