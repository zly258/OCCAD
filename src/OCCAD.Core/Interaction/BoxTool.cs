using OcctNet;

namespace OCCAD;

public sealed class BoxTool : CadDrawingTool, ICadPointInputTool
{
    private const double MinSize = 0.1;
    private OcctPoint3d _start;
    private OcctPoint3d _second;
    private OcctPoint3d _baseOrigin;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _zAxis;
    private double _length;
    private double _width;
    private CadBoxEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "box";
    public override string DisplayName => "Box";

    protected override bool CanStepBackCore => Stage > 0;

    protected override void OnActivated()
    {
        ResetState();
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Box.First",
            "Box: specify first corner [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;

        if (input.Kind == OcctPointerInputKind.Moved && Stage > 0)
        {
            Update(Context.ResolvePoint(input.X, input.Y, ReferencePoint()).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(input.X, input.Y, ReferencePoint()).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, ReferencePoint(), AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnStepBack()
    {
        Context.Preview.Clear();
        _preview = null;

        if (Stage == 2)
        {
            RestoreWorkPlaneFrame(_initialPlane, _start);
            SetStageLocalized(
                1,
                "Cad.Prompt.Box.Opposite",
                "Box: specify opposite base corner [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return true;
        }

        if (Stage == 1)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(
                0,
                "Cad.Prompt.Box.First",
                "Box: specify first corner [Esc cancel]");
            return true;
        }

        return false;
    }

    protected override void OnCanceled() => ResetState();

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        switch (Stage)
        {
            case 0:
                _start = point;
                _second = point;
                _xAxis = Context.WorkPlane.XAxis;
                _yAxis = Context.WorkPlane.YAxis;
                _zAxis = Context.WorkPlane.Normal;
                Context.WorkPlane.SetOrigin(point);
                SetStageLocalized(
                    1,
                    "Cad.Prompt.Box.Opposite",
                    "Box: specify opposite base corner [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.LengthAndAngle);
                break;

            case 1:
                {
                    var basePoint = ProjectBasePoint(point);
                    if (!SetBase(basePoint)) return true;
                    _second = basePoint;
                    SetStageLocalized(
                        2,
                        "Cad.Prompt.Box.Height",
                        "Box: specify height [Backspace undo, Esc cancel]",
                        CadPrecisionInputKind.Length);
                    SetWorkPlane(basePoint, _zAxis, _xAxis);
                    LockStageAngle(0.0);
                    Show(new CadBoxEntity(
                        _baseOrigin,
                        _xAxis,
                        _yAxis,
                        _zAxis,
                        _length,
                        _width,
                        MinSize));
                    break;
                }

            case 2:
                if (Math.Abs(
                        CadPlaneGeometry.SignedDistance(
                            _second,
                            point,
                            _zAxis)) <= 1e-9)
                    return true;

                Update(point);
                if (_preview is not null)
                    Context.AddEntity(_preview.Duplicate());
                Context.Workspace.Tools.CompleteCurrent();
                break;
        }

        return true;
    }

    private OcctPoint3d? ReferencePoint() => Stage switch
    {
        1 => _start,
        2 => _second,
        _ => null
    };

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            if (!TryBasePreview(ProjectBasePoint(point), out var center, out var length, out var width))
                return;

            var origin = center
                - _xAxis * (length * 0.5)
                - _yAxis * (width * 0.5);
            Show(new CadBoxEntity(
                origin,
                _xAxis,
                _yAxis,
                _zAxis,
                length,
                width,
                MinSize));
            return;
        }

        if (Stage != 2) return;

        var height = CadPlaneGeometry.SignedDistance(_second, point, _zAxis);
        var size = Math.Max(Math.Abs(height), MinSize);
        var heightOrigin = height < 0.0
            ? CadTransformMath.Add(_baseOrigin, _zAxis, height)
            : _baseOrigin;
        Show(new CadBoxEntity(
            heightOrigin,
            _xAxis,
            _yAxis,
            _zAxis,
            _length,
            _width,
            size));
    }

    private OcctPoint3d ProjectBasePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_start, point, _xAxis, _yAxis);

    private bool TryBasePreview(
        OcctPoint3d point,
        out OcctPoint3d center,
        out double length,
        out double width)
    {
        var delta = CadTransformMath.Between(_start, point);
        var dx = CadTransformMath.Dot(delta, _xAxis);
        var dy = CadTransformMath.Dot(delta, _yAxis);
        length = Math.Abs(dx);
        width = Math.Abs(dy);
        center = _start + _xAxis * (dx * 0.5) + _yAxis * (dy * 0.5);
        return length > 1e-9 && width > 1e-9;
    }

    private bool SetBase(OcctPoint3d point)
    {
        var delta = CadTransformMath.Between(_start, point);
        var dx = CadTransformMath.Dot(delta, _xAxis);
        var dy = CadTransformMath.Dot(delta, _yAxis);
        if (Math.Abs(dx) <= 1e-9 || Math.Abs(dy) <= 1e-9) return false;

        _length = Math.Abs(dx);
        _width = Math.Abs(dy);
        _baseOrigin = _start;
        if (dx < 0.0) _baseOrigin = CadTransformMath.Add(_baseOrigin, _xAxis, dx);
        if (dy < 0.0) _baseOrigin = CadTransformMath.Add(_baseOrigin, _yAxis, dy);
        return true;
    }

    private void Show(CadBoxEntity entity)
    {
        _preview = entity;
        ShowPreview(entity);
    }

    private void ResetState()
    {
        _preview = null;
    }
}
