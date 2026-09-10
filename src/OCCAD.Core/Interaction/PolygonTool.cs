using OcctNet;

namespace OCCAD;

public sealed class PolygonTool : CadDrawingTool, ICadPointInputTool
{
    private readonly List<OcctPoint3d> _points = [];
    private CadEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "polygon";
    public override string DisplayName => "Polygon";

    protected override void OnActivated()
    {
        _points.Clear();
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Polygon.First",
            "Polygon: specify first point [Esc cancel]");
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
        if (input.Kind != OcctPointerInputKind.Pressed)
            return false;
        if (input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                _points.Count == 0 ? null : _points[^1]).Point);
    }

    protected override bool CanFinishCore => _points.Count >= 3;
    protected override bool CanStepBackCore => _points.Count > 0;

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(
            pointer,
            _points.Count == 0 ? null : _points[^1],
            AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override void OnCanceled() => Reset();

    protected override bool OnStepBack()
    {
        _points.RemoveAt(_points.Count - 1);
        _preview = null;
        Context.Preview.Clear();
        RestorePolygonStage();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinishCore)
            return false;

        CommitPreview(new CadPolygonEntity(_points));
        return true;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;
        if (_points.Count > 0 &&
            _points[^1].DistanceTo(point) <= 1e-9)
            return false;

        var previous =
            _points.Count == 0 ? (OcctPoint3d?)null : _points[^1];
        _points.Add(point);

        if (previous is { } from)
            AdvanceWorkPlaneAlongSegment(from, point);
        else
            Context.WorkPlane.SetOrigin(point);

        Context.Preview.Clear();
        if (_points.Count < 3)
        {
            SetStageLocalized(
                1,
                "Cad.Prompt.Polygon.Next",
                "Polygon: specify next point [Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
        }
        else
        {
            SetStageLocalized(
                1,
                "Cad.Prompt.Polygon.NextFinish",
                "Polygon: specify next point [Enter/right click finish, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
        }
        return true;
    }

    private void RestorePolygonStage()
    {
        if (_points.Count == 0)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(
                0,
                "Cad.Prompt.Polygon.First",
                "Polygon: specify first point [Esc cancel]");
            return;
        }

        RestoreWorkPlaneFrame(
            _initialPlane,
            _points[^1]);
        if (_points.Count >= 2)
            AdvanceWorkPlaneAlongSegment(
                _points[^2],
                _points[^1]);

        SetStageLocalized(
            1,
            _points.Count >= 3
                ? "Cad.Prompt.Polygon.NextFinish"
                : "Cad.Prompt.Polygon.Next",
            _points.Count >= 3
                ? "Polygon: specify next point [Enter/right click finish, Backspace undo, Esc cancel]"
                : "Polygon: specify next point [Backspace undo, Esc cancel]",
            CadPrecisionInputKind.LengthAndAngle);
    }

    private void UpdatePreview(OcctPoint3d cursor)
    {
        if (_points.Count == 0)
            return;

        var values = new List<OcctPoint3d>(_points);
        if (values[^1].DistanceTo(cursor) > 1e-9)
            values.Add(cursor);

        if (values.Count < 2)
        {
            Context.Preview.Clear();
            _preview = null;
            return;
        }

        _preview = values.Count >= 3
            ? new CadPolygonEntity(values)
            : new CadPolylineEntity(values);
        ShowPreview(_preview);
    }

    private void Reset()
    {
        _points.Clear();
        _preview = null;
    }
}
