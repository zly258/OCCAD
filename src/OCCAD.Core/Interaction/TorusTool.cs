using OcctNet;

namespace OCCAD;

public sealed class TorusTool : CadDrawingTool, ICadPointInputTool
{
    private const double MinSize = 0.1;
    private OcctPoint3d _center;
    private OcctPoint3d _majorPoint;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _axis;
    private OcctVector3d _radialAxis;
    private double _majorRadius;
    private CadTorusEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "torus";
    public override string DisplayName => "Torus";

    protected override bool CanStepBackCore => Stage > 0;

    protected override void OnActivated()
    {
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Torus.Center",
            "Torus: specify center [Esc cancel]");
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
            RestoreWorkPlaneFrame(_initialPlane, _center);
            SetStageLocalized(
                1,
                "Cad.Prompt.Torus.MajorRadius",
                "Torus: specify major radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            return true;
        }

        if (Stage == 1)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(
                0,
                "Cad.Prompt.Torus.Center",
                "Torus: specify center [Esc cancel]");
            return true;
        }

        return false;
    }

    protected override void OnCanceled() => Reset();

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (Stage == 0)
        {
            _center = point;
            _majorPoint = point;
            _xAxis = Context.WorkPlane.XAxis;
            _yAxis = Context.WorkPlane.YAxis;
            _axis = Context.WorkPlane.Normal;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(
                1,
                "Cad.Prompt.Torus.MajorRadius",
                "Torus: specify major radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            return true;
        }

        if (Stage == 1)
        {
            var majorPoint = ProjectMajorPoint(point);
            _majorRadius = CadPlaneGeometry.RadialDistance(_center, majorPoint, _xAxis, _yAxis);
            if (_majorRadius <= 1e-9)
                return false;
            _majorPoint = majorPoint;
            _radialAxis = CadTransformMath.Normalize(
                CadTransformMath.Between(_center, _majorPoint),
                nameof(_majorPoint));
            SetStageLocalized(
                2,
                "Cad.Prompt.Torus.TubeRadius",
                "Torus: specify tube radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            SetWorkPlane(_majorPoint, _radialAxis, _axis);
            Show(new CadTorusEntity(
                _center,
                _axis,
                _majorRadius,
                _majorRadius * 0.2));
            return true;
        }

        var tubePoint = ProjectTubePoint(point);
        if (_majorPoint.DistanceTo(tubePoint) <= 1e-9)
            return false;

        Update(tubePoint);
        if (_preview is null)
            return false;
        CommitPreview(_preview);
        return true;
    }

    private OcctPoint3d? ReferencePoint() => Stage switch
    {
        1 => _center,
        2 => _majorPoint,
        _ => null
    };

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            var majorPoint = ProjectMajorPoint(point);
            var major = CadPlaneGeometry.RadialDistance(_center, majorPoint, _xAxis, _yAxis);
            if (major <= 1e-9)
            {
                _preview = null;
                Context.Preview.Clear();
                return;
            }

            Show(new CadTorusEntity(
                _center,
                _axis,
                major,
                major * 0.2));
            return;
        }

        if (Stage != 2) return;

        var tubePoint = ProjectTubePoint(point);
        var minorRadius = ResolveMinorRadius(
            _majorRadius,
            _majorPoint.DistanceTo(tubePoint));
        Show(new CadTorusEntity(
            _center,
            _axis,
            _majorRadius,
            minorRadius));
    }

    private OcctPoint3d ProjectMajorPoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_center, point, _xAxis, _yAxis);

    private OcctPoint3d ProjectTubePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_majorPoint, point, _radialAxis, _axis);

    private void Show(CadTorusEntity entity)
    {
        _preview = entity;
        ShowPreview(entity);
    }

    private static double ResolveMinorRadius(
        double majorRadius,
        double requestedRadius)
    {
        if (!double.IsFinite(majorRadius) || majorRadius <= 1e-9)
            throw new ArgumentOutOfRangeException(nameof(majorRadius));
        if (!double.IsFinite(requestedRadius))
            throw new ArgumentOutOfRangeException(nameof(requestedRadius));

        var minimum = Math.Min(MinSize, majorRadius * 0.1);
        var maximum = majorRadius * 0.49;
        return Math.Clamp(requestedRadius, minimum, maximum);
    }

    private void Reset()
    {
        _preview = null;
    }
}
