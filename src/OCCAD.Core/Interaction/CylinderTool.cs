using OcctNet;

namespace OCCAD;

public sealed class CylinderTool : CadDrawingTool, ICadPointInputTool
{
    private const double MinSize = 0.1;
    private OcctPoint3d _center;
    private OcctPoint3d _radiusPoint;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _axis;
    private double _radius;
    private CadCylinderEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "cylinder";
    public override string DisplayName => "Cylinder";

    protected override bool CanStepBackCore => Stage > 0;

    protected override void OnActivated()
    {
        ResetState();
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Cylinder.Center",
            "Cylinder: specify base center [Esc cancel]");
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
                "Cad.Prompt.Cylinder.Radius",
                "Cylinder: specify radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            return true;
        }

        if (Stage == 1)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(
                0,
                "Cad.Prompt.Cylinder.Center",
                "Cylinder: specify base center [Esc cancel]");
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
                _center = point;
                _radiusPoint = point;
                _xAxis = Context.WorkPlane.XAxis;
                _yAxis = Context.WorkPlane.YAxis;
                _axis = Context.WorkPlane.Normal;
                Context.WorkPlane.SetOrigin(point);
                SetStageLocalized(
                    1,
                    "Cad.Prompt.Cylinder.Radius",
                    "Cylinder: specify radius [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.Length);
                break;

            case 1:
                {
                    var radiusPoint = ProjectBasePoint(point);
                    _radius = CadPlaneGeometry.RadialDistance(_center, radiusPoint, _xAxis, _yAxis);
                    if (_radius <= 1e-9) return true;
                    _radiusPoint = radiusPoint;
                    SetStageLocalized(
                        2,
                        "Cad.Prompt.Cylinder.Height",
                        "Cylinder: specify height [Backspace undo, Esc cancel]",
                        CadPrecisionInputKind.Length);
                    SetWorkPlane(radiusPoint, _axis, _xAxis);
                    LockStageAngle(0.0);
                    Show(new CadCylinderEntity(
                        _center,
                        _axis,
                        _radius,
                        MinSize));
                    break;
                }

            case 2:
                if (Math.Abs(
                        CadPlaneGeometry.SignedDistance(
                            _radiusPoint,
                            point,
                            _axis)) <= 1e-9)
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
        1 => _center,
        2 => _radiusPoint,
        _ => null
    };

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            var radiusPoint = ProjectBasePoint(point);
            var radius = CadPlaneGeometry.RadialDistance(_center, radiusPoint, _xAxis, _yAxis);
            if (radius <= 1e-9)
                return;

            Show(new CadCylinderEntity(
                _center,
                _axis,
                radius,
                MinSize));
            return;
        }

        if (Stage != 2) return;

        var signedHeight = CadPlaneGeometry.SignedDistance(_radiusPoint, point, _axis);
        var axis = signedHeight < 0.0 ? Negate(_axis) : _axis;
        Show(new CadCylinderEntity(
            _center,
            axis,
            _radius,
            Math.Max(Math.Abs(signedHeight), MinSize)));
    }

    private OcctPoint3d ProjectBasePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_center, point, _xAxis, _yAxis);

    private void Show(CadCylinderEntity entity)
    {
        _preview = entity;
        Context.Preview.Show(entity);
    }

    private static OcctVector3d Negate(OcctVector3d value) =>
        new(-value.X, -value.Y, -value.Z);

    private void ResetState()
    {
        _preview = null;
    }
}
