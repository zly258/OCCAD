using OcctNet;

namespace OCCAD;

public sealed class PointTool : CadDrawingTool, ICadPointInputTool
{
    public override string Id => "point";
    public override string DisplayName => "Point";

    protected override void OnActivated() =>
        SetStageLocalized(
            0,
            "Cad.Prompt.Point.Location",
            "Point: specify location [Esc cancel]");

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(Context.ResolvePoint(input.X, input.Y).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, null, AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) => AcceptPoint(point);

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        Context.AddEntity(new CadPointEntity(point));
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }
}
