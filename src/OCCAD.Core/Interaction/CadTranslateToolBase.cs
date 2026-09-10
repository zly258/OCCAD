using OcctNet;

namespace OCCAD;

public abstract class CadTranslateToolBase : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _basePoint;

    protected OcctPoint3d BasePoint =>
        _basePoint ??
        throw new InvalidOperationException("Base point is not set.");

    protected override void OnTransformStarted()
    {
        _basePoint = null;
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
            ShowTranslatedPreview(
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

    protected override void ResetTransformState() =>
        _basePoint = null;

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (_basePoint is null)
        {
            _basePoint = point;
            Context.WorkPlane.SetOrigin(point);
            Context.Preview.Show(
                Entities.Select(static entity => entity.Duplicate()));
            SetStageLocalized(
                1,
                $"Cad.Prompt.{Id}.Target",
                $"{DisplayName}: specify target point [Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return true;
        }

        var displacement =
            CadTransformMath.Between(_basePoint.Value, point);
        if (displacement.LengthSquared > 1e-18)
            Commit(displacement);

        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void ShowTranslatedPreview(OcctPoint3d target)
    {
        var displacement =
            CadTransformMath.Between(BasePoint, target);
        ShowEntityPreview(
            entity => entity.TranslatePlacement(displacement));
    }
}
