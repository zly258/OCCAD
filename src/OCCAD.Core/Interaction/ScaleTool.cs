using OcctNet;

namespace OCCAD;

public sealed class ScaleTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private const double MinimumFactor = 1e-9;

    private OcctPoint3d? _basePoint;
    private OcctPoint3d _initialOrigin;

    public override string Id => "scale";
    public override string DisplayName => "Scale";
    public override string PrecisionFactorLabel => "Scale Factor";
    public override OcctPoint3d? PrecisionReferencePoint => _basePoint;

    protected override bool SuppressSourcesDuringPreview => true;
    protected override bool CanCommitCurrentStageCore => true;
    protected override bool CanStepBackCore => _basePoint is not null;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        _basePoint is not null &&
        Context.Workspace.Precision.Factor is > MinimumFactor
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnTransformStarted()
    {
        _basePoint = null;
        _initialOrigin = Context.WorkPlane.Origin;
        SetStageLocalized(
            0,
            "Cad.Prompt.scale.Base",
            "Scale: specify base point [Esc cancel]");
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
            var point = Context.ResolvePoint(
                input.X,
                input.Y,
                basePoint).Point;
            UpdatePreview(point);
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
            Context.Workspace.Precision.Factor is { } factor &&
            factor > MinimumFactor)
        {
            return CommitFactor(factor);
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
        Context.WorkPlane.SetOrigin(_initialOrigin);
        Context.Workspace.Precision.ResetFactor();
        SetStageLocalized(
            0,
            "Cad.Prompt.scale.Base",
            "Scale: specify base point [Esc cancel]");
        return true;
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_basePoint is null)
            return input.Factor is null;

        if (Context.Workspace.Precision.Factor is { } factor)
        {
            if (!IsValidFactor(factor))
                return false;

            ShowScalePreview(factor);
            return true;
        }

        if (Context.Workspace.LastPointerPosition is { } pointer)
            RefreshPreviewFromLastPointer(pointer);
        return true;
    }

    protected internal override void RefreshPreviewFromLastPointer(
        CadPointerPosition pointer)
    {
        if (_basePoint is null)
            return;

        if (Context.Workspace.Precision.Factor is { } factor &&
            IsValidFactor(factor))
        {
            ShowScalePreview(factor);
            return;
        }

        var point = Context.ResolvePoint(
            pointer.X,
            pointer.Y,
            _basePoint).Point;
        UpdatePreview(point);
    }

    protected override void ResetTransformState()
    {
        _basePoint = null;
        _initialOrigin = default;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        if (_basePoint is null)
        {
            _basePoint = point;
            Context.WorkPlane.SetOrigin(point);
            ClearTransformPreview();
            SetStageLocalized(
                1,
                "Cad.Prompt.scale.Target",
                "Scale: specify factor [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Factor);
            return true;
        }

        if (Context.Workspace.Precision.Factor is { } lockedFactor)
            return CommitFactor(lockedFactor);

        if (!TryFactorFromPoint(point, out var factor))
        {
            ClearTransformPreview();
            return false;
        }

        return CommitFactor(factor);
    }

    private void UpdatePreview(OcctPoint3d point)
    {
        if (Context.Workspace.Precision.Factor is { } lockedFactor)
        {
            if (IsValidFactor(lockedFactor))
                ShowScalePreview(lockedFactor);
            else
                ClearTransformPreview();
            return;
        }

        if (!TryFactorFromPoint(point, out var factor))
        {
            ClearTransformPreview();
            return;
        }

        ShowScalePreview(factor);
    }

    private void ShowScalePreview(double factor)
    {
        if (!IsValidFactor(factor) || _basePoint is null)
        {
            ClearTransformPreview();
            return;
        }

        if (Math.Abs(factor - 1.0) <= 1e-12)
        {
            ClearTransformPreview();
            return;
        }

        ShowEntityPreview(
            entity => entity.ScaleFromWorld(
                _basePoint.Value,
                factor));
    }

    private bool CommitFactor(double factor)
    {
        if (_basePoint is null ||
            !IsValidFactor(factor) ||
            Math.Abs(factor - 1.0) <= 1e-12)
        {
            ClearTransformPreview();
            return false;
        }

        Context.Workspace.ScaleEntities(
            Entities,
            _basePoint.Value,
            factor);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private bool TryFactorFromPoint(
        OcctPoint3d point,
        out double factor)
    {
        factor = 0.0;
        if (_basePoint is not { } basePoint)
            return false;

        var frame = Context.WorkPlane.EffectivePlane;
        var delta = point - basePoint;
        var x = delta.Dot(frame.XAxis);
        var y = delta.Dot(frame.YAxis);
        factor = Math.Sqrt(x * x + y * y);
        return IsValidFactor(factor);
    }

    private static bool IsValidFactor(double factor) =>
        double.IsFinite(factor) && factor > MinimumFactor;
}
