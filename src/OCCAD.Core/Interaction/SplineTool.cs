using OcctNet;

namespace OCCAD;

public sealed class SplineTool : CadDrawingTool, ICadPointInputTool
{
    private readonly List<OcctPoint3d> _fitPoints = [];
    private CadEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "spline";
    public override string DisplayName => "Spline";

    public override bool CanCommitCurrentStage =>
        _fitPoints.Count > 0 &&
        IsActive &&
        State == CadToolState.Drawing &&
        CadExactInputGeometry.TryResolveLengthAnglePoint(
            Context.Workspace,
            _fitPoints[^1],
            out _)
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnActivated()
    {
        _fitPoints.Clear();
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Spline.First",
            "Spline: specify first fit point [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Moved && _fitPoints.Count > 0)
        {
            UpdatePreview(
                Context.ResolvePoint(
                    input.X,
                    input.Y,
                    _fitPoints[^1]).Point);
            return true;
        }
        if (FinishOnDoubleClick(input)) return true;
        if (CancelOnRightClick(input)) return true;
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                _fitPoints.Count == 0 ? null : _fitPoints[^1]).Point);
    }

    protected override bool CanFinishCore => _fitPoints.Count >= 2;
    protected override bool CanStepBackCore => _fitPoints.Count > 0;

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (_fitPoints.Count > 0 &&
            CadExactInputGeometry.TryResolveLengthAnglePoint(
                Context.Workspace,
                _fitPoints[^1],
                out var exactPoint))
        {
            return AcceptPoint(exactPoint);
        }

        return CommitResolvedPoint(
            pointer,
            _fitPoints.Count == 0 ? null : _fitPoints[^1],
            AcceptPoint);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        AcceptPoint(point);

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_fitPoints.Count > 0 &&
            CadExactInputGeometry.TryResolveLengthAnglePoint(
                Context.Workspace,
                _fitPoints[^1],
                out var exactPoint))
        {
            UpdatePreview(exactPoint);
            return true;
        }

        return base.OnPrecisionInputApplied(input);
    }

    protected override void OnCanceled() => Reset();

    protected override bool OnStepBack()
    {
        if (_fitPoints.Count == 0)
            return false;

        _fitPoints.RemoveAt(_fitPoints.Count - 1);
        _preview = null;
        Context.Preview.Clear();
        RestoreSplineStage();
        ShowAcceptedSpline();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinishCore)
            return false;

        CommitPreview(new CadSplineEntity(_fitPoints));
        return true;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;
        if (_fitPoints.Count > 0 &&
            _fitPoints[^1].DistanceTo(point) <= 1e-9)
            return false;

        var previous = _fitPoints.Count == 0
            ? (OcctPoint3d?)null
            : _fitPoints[^1];
        _fitPoints.Add(point);

        if (previous is { } from)
            AdvanceWorkPlaneAlongSegment(from, point);
        else
            Context.WorkPlane.SetOrigin(point);

        Context.Preview.Clear();
        SetStageLocalized(
            1,
            _fitPoints.Count >= 2
                ? "Cad.Prompt.Spline.NextFinish"
                : "Cad.Prompt.Spline.Next",
            _fitPoints.Count >= 2
                ? "Spline: specify next fit point [Enter/right click finish, Esc cancel]"
                : "Spline: specify next fit point [Esc cancel]",
            CadPrecisionInputKind.LengthAndAngle);
        return true;
    }

    private void RestoreSplineStage()
    {
        if (_fitPoints.Count == 0)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(
                0,
                "Cad.Prompt.Spline.First",
                "Spline: specify first fit point [Esc cancel]");
            return;
        }

        RestoreWorkPlaneFrame(_initialPlane, _fitPoints[^1]);
        if (_fitPoints.Count >= 2)
            AdvanceWorkPlaneAlongSegment(
                _fitPoints[^2],
                _fitPoints[^1]);

        SetStageLocalized(
            1,
            _fitPoints.Count >= 2
                ? "Cad.Prompt.Spline.NextFinish"
                : "Cad.Prompt.Spline.Next",
            _fitPoints.Count >= 2
                ? "Spline: specify next fit point [Enter/right click finish, Backspace undo, Esc cancel]"
                : "Spline: specify next fit point [Backspace undo, Esc cancel]",
            CadPrecisionInputKind.LengthAndAngle);
    }

    private void ShowAcceptedSpline()
    {
        if (_fitPoints.Count < 2)
            return;

        _preview = _fitPoints.Count == 2
            ? new CadLineEntity(_fitPoints[0], _fitPoints[1])
            : new CadSplineEntity(_fitPoints);
        ShowPreview(_preview);
    }

    private void UpdatePreview(OcctPoint3d cursor)
    {
        var values = new List<OcctPoint3d>(_fitPoints);
        if (values.Count == 0)
            return;

        if (values[^1].DistanceTo(cursor) > 1e-9)
            values.Add(cursor);
        if (values.Count < 2)
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }

        _preview = values.Count == 2
            ? new CadLineEntity(values[0], values[1])
            : new CadSplineEntity(values);
        ShowPreview(_preview);
    }

    private void Reset()
    {
        _fitPoints.Clear();
        _preview = null;
    }
}
