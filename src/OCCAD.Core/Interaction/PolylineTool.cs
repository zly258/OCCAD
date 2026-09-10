using OcctNet;

namespace OCCAD;

public sealed class PolylineTool : CadDrawingTool, ICadPointInputTool, ICadCommandOptionTool
{
    private readonly List<OcctPoint3d> _points = [];
    private CadPolylineEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "polyline";
    public override string DisplayName => "Polyline";

    protected override void OnActivated()
    {
        _points.Clear();
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(0, "Cad.Prompt.Polyline.First", "Polyline: specify first point [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Moved && _points.Count > 0)
        {
            UpdatePreview(Context.ResolvePoint(input.X, input.Y, _points[^1]).Point);
            return true;
        }
        if (FinishOnDoubleClick(input)) return true;
        if (CancelOnRightClick(input)) return true;
        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;
        return AcceptPoint(Context.ResolvePoint(input.X, input.Y, _points.Count == 0 ? null : _points[^1]).Point);
    }

    protected override bool CanFinishCore => _points.Count >= 2;
    protected override bool CanStepBackCore => _points.Count > 0;

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, _points.Count == 0 ? null : _points[^1], AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        AcceptPoint(point);

    public bool TryExecuteOption(string input, out bool success, out string? message)
    {
        if (!input.Equals("C", StringComparison.OrdinalIgnoreCase) &&
            !input.Equals("CLOSE", StringComparison.OrdinalIgnoreCase))
        {
            success = false;
            message = null;
            return false;
        }

        if (_points.Count < 3)
        {
            success = false;
            message = "Polyline requires at least three points before Close.";
            return true;
        }

        Context.AddEntity(new CadPolylineEntity(_points, closed: true));
        Context.Workspace.Tools.CompleteCurrent();
        success = true;
        message = "Close";
        return true;
    }

    protected override void OnCanceled() => Reset();

    protected override bool OnStepBack()
    {
        _points.RemoveAt(_points.Count - 1);
        _preview = null;
        Context.Preview.Clear();
        RestorePolylineStage();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinishCore) return false;
        Context.AddEntity(new CadPolylineEntity(_points));
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (_points.Count > 0 &&
            _points[^1].DistanceTo(point) <= 1e-9)
            return false;

        var previous = _points.Count == 0 ? (OcctPoint3d?)null : _points[^1];
        _points.Add(point);
        if (previous is { } from)
            AdvanceWorkPlaneAlongSegment(from, point);
        else
            Context.WorkPlane.SetOrigin(point);

        SetStageLocalized(
            1,
            _points.Count >= 3 ? "Cad.Prompt.Polyline.NextClose" :
                _points.Count >= 2 ? "Cad.Prompt.Polyline.NextFinish" : "Cad.Prompt.Polyline.Next",
            _points.Count >= 3
                ? "Polyline: specify next point or [Close] [Enter/Space/right click finish, Esc cancel]"
                : _points.Count >= 2
                    ? "Polyline: specify next point [Enter/Space/right click finish, Esc cancel]"
                    : "Polyline: specify next point [Esc cancel]",
            CadPrecisionInputKind.LengthAndAngle);
        UpdatePreview(point);
        return true;
    }

    private void RestorePolylineStage()
    {
        if (_points.Count == 0)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(0, "Cad.Prompt.Polyline.First", "Polyline: specify first point [Esc cancel]");
            return;
        }

        RestoreWorkPlaneFrame(_initialPlane, _points[^1]);
        if (_points.Count >= 2)
            AdvanceWorkPlaneAlongSegment(_points[^2], _points[^1]);

        SetStageLocalized(
            1,
            _points.Count >= 3 ? "Cad.Prompt.Polyline.NextClose" :
                _points.Count >= 2 ? "Cad.Prompt.Polyline.NextFinish" : "Cad.Prompt.Polyline.Next",
            _points.Count >= 3
                ? "Polyline: specify next point or [Close] [Enter/Space/right click finish, Backspace undo, Esc cancel]"
                : _points.Count >= 2
                    ? "Polyline: specify next point [Enter/Space/right click finish, Backspace undo, Esc cancel]"
                    : "Polyline: specify next point [Backspace undo, Esc cancel]",
            CadPrecisionInputKind.LengthAndAngle);
    }

    private void UpdatePreview(OcctPoint3d cursor)
    {
        if (_points.Count == 0) return;
        var values = new List<OcctPoint3d>(_points);
        if (values[^1].DistanceTo(cursor) > 1e-9) values.Add(cursor);
        if (values.Count < 2) return;
        _preview = new CadPolylineEntity(values);
        ShowPreview(_preview);
    }

    private void Reset()
    {
        _points.Clear();
        _preview = null;
    }
}
