using OcctNet;

namespace OCCAD;

public sealed class LineTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d? _start;
    private CadLineEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "line";
    public override string DisplayName => "Line";
    protected override bool CanStepBackCore =>
        _start is not null;

    protected override void OnActivated()
    {
        _start = null;
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(0, "Cad.Prompt.Line.First", "Line: specify first point [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && _start is { } start)
        {
            UpdatePreview(start, Context.ResolvePoint(input.X, input.Y, start).Point);
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;
        return AcceptPoint(Context.ResolvePoint(input.X, input.Y, _start).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, _start, AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        AcceptPoint(point);

    protected override bool OnStepBack()
    {
        if (_start is null)
            return false;

        _start = null;
        _preview = null;
        Context.Preview.Clear();
        RestoreWorkPlaneFrame(_initialPlane);
        SetStageLocalized(
            0,
            "Cad.Prompt.Line.First",
            "Line: specify first point [Esc cancel]");
        return true;
    }

    protected override void OnCanceled() => Reset();

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (_start is null)
        {
            _start = point;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(1, "Cad.Prompt.Line.Next", "Line: specify next point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle);
            return true;
        }
        if (_start.Value.DistanceTo(point) <= 1e-9)
            return false;
        var entity = new CadLineEntity(_start.Value, point);
        _start = null;
        _preview = null;
        CommitPreview(entity);
        return true;
    }

    private void UpdatePreview(OcctPoint3d start, OcctPoint3d end)
    {
        if (start.DistanceTo(end) <= 1e-9)
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }
        _preview = new CadLineEntity(start, end);
        ShowPreview(_preview);
    }

    private void Reset()
    {
        _start = null;
        _preview = null;
    }
}
