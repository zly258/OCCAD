using OcctNet;

namespace OCCAD;

public abstract class CadTranslateToolBase : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _basePoint;
    private OcctPoint3d _initialOrigin;

    protected OcctPoint3d BasePoint =>
        _basePoint ??
        throw new InvalidOperationException("Base point is not set.");

    protected override void OnTransformStarted()
    {
        _basePoint = null;
        _initialOrigin =
            Context.WorkPlane.Origin;
        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.Base",
            $"{DisplayName}: specify base point [Esc cancel]");
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
            UpdateTranslatedPreview(
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

    protected override bool CanCommitCurrentStageCore => true;
    protected override bool CanStepBackCore =>
        _basePoint is not null;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer)
    {
        if (State != CadToolState.Drawing)
            return false;

        var point = Context.ResolvePoint(
            pointer.X,
            pointer.Y,
            _basePoint).Point;
        return AcceptPoint(point);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        Entities.Count > 0 &&
        AcceptPoint(point);

    protected abstract void Commit(OcctVector3d displacement);

    protected override bool OnStepBack()
    {
        if (_basePoint is null)
            return false;

        _basePoint = null;
        ClearTransformPreview();
        Context.WorkPlane.SetOrigin(
            _initialOrigin);
        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.Base",
            $"{DisplayName}: specify base point [Esc cancel]");
        return true;
    }

    protected override void ResetTransformState()
    {
        _basePoint = null;
        _initialOrigin = default;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (_basePoint is null)
        {
            _basePoint = point;
            Context.WorkPlane.SetOrigin(point);
            ClearTransformPreview();
            SetStageLocalized(
                1,
                $"Cad.Prompt.{Id}.Target",
                $"{DisplayName}: specify target point [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return true;
        }

        var displacement =
            CadTransformMath.Between(_basePoint.Value, point);
        if (displacement.LengthSquared <= 1e-18)
        {
            ClearTransformPreview();
            return false;
        }

        Commit(displacement);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    protected virtual void ShowTranslatedPreview(
        OcctVector3d displacement)
    {
        ShowEntityPreview(
            entity =>
                entity.TranslatePlacement(displacement));
    }

    protected void RefreshTranslatedPreview()
    {
        if (_basePoint is not { } basePoint ||
            Context.Workspace.LastPointerPosition is not { } pointer)
            return;

        var target =
            Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                basePoint).Point;
        UpdateTranslatedPreview(target);
    }

    private void UpdateTranslatedPreview(
        OcctPoint3d target)
    {
        var displacement =
            CadTransformMath.Between(BasePoint, target);
        if (displacement.LengthSquared <= 1e-18)
        {
            ClearTransformPreview();
            return;
        }

        ShowTranslatedPreview(displacement);
    }
}
