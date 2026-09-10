using OcctNet;

namespace OCCAD;

public sealed class ConeTool : CadDrawingTool, ICadPointInputTool
{
    private const double MinSize = 0.1;
    private OcctPoint3d _center;
    private OcctPoint3d _radiusPoint;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _baseNormal;
    private double _radius;
    private CadConeEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "cone";
    public override string DisplayName => "Cone";

    protected override bool CanStepBackCore => Stage > 0;

    protected override void OnActivated()
    {
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Cone.Center",
            "Cone: specify base center [Esc cancel]");
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

        return AcceptPoint(Context.ResolvePoint(input.X, input.Y, ReferencePoint()).Point);
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
                "Cad.Prompt.Cone.BaseRadius",
                "Cone: specify base radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            return true;
        }

        if (Stage == 1)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(
                0,
                "Cad.Prompt.Cone.Center",
                "Cone: specify base center [Esc cancel]");
            return true;
        }

        return false;
    }

    protected override void OnCanceled() => _preview = null;

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
                _baseNormal = Context.WorkPlane.Normal;
                Context.WorkPlane.SetOrigin(point);
                SetStageLocalized(
                    1,
                    "Cad.Prompt.Cone.BaseRadius",
                    "Cone: specify base radius [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.Length);
                return true;

            case 1:
                {
                    var radiusPoint = ProjectBasePoint(point);
                    _radius = CadPlaneGeometry.RadialDistance(_center, radiusPoint, _xAxis, _yAxis);
                    if (_radius <= 1e-9) return true;
                    _radiusPoint = radiusPoint;
                    SetStageLocalized(
                        2,
                        "Cad.Prompt.Cone.Height",
                        "Cone: specify height [Backspace undo, Esc cancel]",
                        CadPrecisionInputKind.Length);
                    SetWorkPlane(radiusPoint, _baseNormal, _xAxis);
                    LockStageAngle(0.0);
                    Show(new CadConeEntity(_center, _baseNormal, _radius, MinSize));
                    return true;
                }

            case 2:
                {
                    var entity = CreateCone(point);
                    if (entity is null) return true;
                    Context.AddEntity(entity);
                    Context.Workspace.Tools.CompleteCurrent();
                    return true;
                }

            default:
                return false;
        }
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
            if (radius > 1e-9)
                Show(new CadConeEntity(_center, _baseNormal, radius, MinSize));
            return;
        }

        if (Stage != 2) return;
        if (CreateCone(point) is { } entity)
            Show(entity);
    }

    private OcctPoint3d ProjectBasePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_center, point, _xAxis, _yAxis);

    private CadConeEntity? CreateCone(OcctPoint3d point)
    {
        var signedHeight = CadPlaneGeometry.SignedDistance(_radiusPoint, point, _baseNormal);
        if (Math.Abs(signedHeight) <= 1e-9) return null;
        var axis = signedHeight < 0 ? Negate(_baseNormal) : _baseNormal;
        return new CadConeEntity(_center, axis, _radius, Math.Abs(signedHeight));
    }

    private void Show(CadConeEntity entity)
    {
        _preview = entity;
        ShowPreview(entity);
    }

    private static OcctVector3d Negate(OcctVector3d value) =>
        new(-value.X, -value.Y, -value.Z);
}
