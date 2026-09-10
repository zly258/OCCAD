using OcctNet;

namespace OCCAD;

/// <summary>
/// Selection-first move command. Existing selection enters the base-point stage
/// immediately; otherwise the user selects entities and confirms the selection
/// before specifying base and target points.
/// </summary>
public sealed class MoveTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _basePoint;

    public override string Id => "move";
    public override string DisplayName => "Move";
    public override OcctPoint3d? PrecisionReferencePoint => _basePoint;

    protected override bool SuppressSourcesDuringPreview => true;

    protected override void OnTransformStarted()
    {
        _basePoint = null;
        SetStageLocalized(
            0,
            "Cad.Prompt.move.Base",
            "Move: specify base point [Esc cancel]");
    }

    protected override void ResetTransformState()
    {
        _basePoint = null;
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (State != CadToolState.Drawing)
            return false;

        if (CancelOnRightClick(input))
            return true;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            if (_basePoint is { } basePoint)
                UpdatePreview(basePoint, ResolvePoint(input.X, input.Y, basePoint));
            return _basePoint is not null;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(ResolvePoint(input.X, input.Y, _basePoint));
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive || State != CadToolState.Drawing || !point.IsFinite)
            return false;

        return AcceptPoint(ProjectToDrawingPlane(point));
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        AcceptPoint(ResolvePoint(pointer.X, pointer.Y, _basePoint));

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_basePoint is not null &&
            Context.Workspace.LastPointerPosition is { } pointer)
            RefreshPreviewFromLastPointer(pointer);
        return true;
    }

    protected internal override void RefreshPreviewFromLastPointer(
        CadPointerPosition pointer)
    {
        if (_basePoint is not { } basePoint ||
            !IsActive ||
            State != CadToolState.Drawing)
            return;

        UpdatePreview(basePoint, ResolvePoint(pointer.X, pointer.Y, basePoint));
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (_basePoint is null)
        {
            _basePoint = point;
            SetStageLocalized(
                1,
                "Cad.Prompt.move.Target",
                "Move: specify target point [Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return true;
        }

        var displacement = CadTransformMath.Between(_basePoint.Value, point);
        if (!displacement.IsFinite || displacement.LengthSquared <= 1e-18)
            return false;

        var targets = Entities.ToArray();
        var workspace = Context.Workspace;
        CommitTransform(complete =>
            CadTransaction.ApplyEntities(
                workspace,
                targets,
                "Move",
                entity => entity.TranslatePlacement(displacement),
                geometryOnly: true,
                complete: complete));
        return true;
    }

    private void UpdatePreview(OcctPoint3d basePoint, OcctPoint3d target)
    {
        var displacement = CadTransformMath.Between(basePoint, target);
        if (!displacement.IsFinite || displacement.LengthSquared <= 1e-18)
        {
            ClearTransformPreview();
            return;
        }

        ShowEntityPreview(entity => entity.TranslatePlacement(displacement));
    }

    private OcctPoint3d ResolvePoint(int x, int y, OcctPoint3d? reference)
    {
        var resolved = Context.ResolvePoint(x, y, reference).Point;
        return ProjectToDrawingPlane(resolved);
    }

    private OcctPoint3d ProjectToDrawingPlane(OcctPoint3d point)
    {
        if (!Context.WorkPlane.IsActive)
            return point;

        var plane = Context.WorkPlane.EffectivePlane;
        return CadPlaneGeometry.ProjectToPlane(
            plane.Origin,
            point,
            plane.XAxis,
            plane.YAxis);
    }
}
