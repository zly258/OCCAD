using OcctNet;

namespace OCCAD;

public sealed class FrustumTool : CadDrawingTool, ICadPointInputTool
{
    private const double MinSize = 0.1;
    private OcctPoint3d _center;
    private OcctPoint3d _baseRadiusPoint;
    private OcctPoint3d _topCenter;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _baseNormal;
    private OcctVector3d _axis;
    private double _baseRadius;
    private double _height;
    private CadFrustumEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "frustum";
    public override string DisplayName => "Frustum";

    protected override bool CanStepBackCore => Stage > 0;

    protected override void OnActivated()
    {
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Frustum.Center",
            "Frustum: specify base center [Esc cancel]");
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

        if (Stage == 3)
        {
            SetStageLocalized(
                2,
                "Cad.Prompt.Frustum.Height",
                "Frustum: specify height [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            SetWorkPlane(_baseRadiusPoint, _baseNormal, _xAxis);
            LockStageAngle(0.0);
            return true;
        }

        if (Stage == 2)
        {
            RestoreWorkPlaneFrame(_initialPlane, _center);
            SetStageLocalized(
                1,
                "Cad.Prompt.Frustum.BaseRadius",
                "Frustum: specify base radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            return true;
        }

        if (Stage == 1)
        {
            RestoreWorkPlaneFrame(_initialPlane);
            SetStageLocalized(
                0,
                "Cad.Prompt.Frustum.Center",
                "Frustum: specify base center [Esc cancel]");
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
                _baseRadiusPoint = point;
                _xAxis = Context.WorkPlane.XAxis;
                _yAxis = Context.WorkPlane.YAxis;
                _baseNormal = Context.WorkPlane.Normal;
                _axis = _baseNormal;
                Context.WorkPlane.SetOrigin(point);
                SetStageLocalized(
                    1,
                    "Cad.Prompt.Frustum.BaseRadius",
                    "Frustum: specify base radius [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.Length);
                return true;

            case 1:
                {
                    var radiusPoint = ProjectBasePoint(point);
                    _baseRadius = CadPlaneGeometry.RadialDistance(_center, radiusPoint, _xAxis, _yAxis);
                    if (_baseRadius <= 1e-9)
                        return false;
                    _baseRadiusPoint = radiusPoint;
                    SetStageLocalized(
                        2,
                        "Cad.Prompt.Frustum.Height",
                        "Frustum: specify height [Backspace undo, Esc cancel]",
                        CadPrecisionInputKind.Length);
                    SetWorkPlane(radiusPoint, _baseNormal, _xAxis);
                    LockStageAngle(0.0);
                    Show(new CadFrustumEntity(
                        _center,
                        _baseNormal,
                        _baseRadius,
                        Math.Max(_baseRadius * 0.5, MinSize),
                        MinSize));
                    return true;
                }

            case 2:
                if (!SetHeight(point)) return true;
                SetStageLocalized(
                    3,
                    "Cad.Prompt.Frustum.TopRadius",
                    "Frustum: specify top radius [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.Length);
                SetWorkPlane(_topCenter, _xAxis, _yAxis);
                Show(new CadFrustumEntity(
                    _center,
                    _axis,
                    _baseRadius,
                    Math.Max(_baseRadius * 0.5, MinSize),
                    _height));
                return true;

            case 3:
                {
                    var entity = CreateFrustum(ProjectTopPoint(point));
                    if (entity is null)
                        return false;
                    CommitPreview(entity);
                    return true;
                }

            default:
                return false;
        }
    }

    private OcctPoint3d? ReferencePoint() => Stage switch
    {
        1 => _center,
        2 => _baseRadiusPoint,
        3 => _topCenter,
        _ => null
    };

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            var radiusPoint = ProjectBasePoint(point);
            var radius = CadPlaneGeometry.RadialDistance(_center, radiusPoint, _xAxis, _yAxis);
            if (radius > 1e-9)
            {
                Show(new CadFrustumEntity(
                    _center,
                    _baseNormal,
                    radius,
                    Math.Max(radius * 0.5, MinSize),
                    MinSize));
            }
            else
            {
                _preview = null;
                Context.Preview.Clear();
            }
            return;
        }

        if (Stage == 2)
        {
            var signedHeight = CadPlaneGeometry.SignedDistance(_baseRadiusPoint, point, _baseNormal);
            if (Math.Abs(signedHeight) <= 1e-9)
            {
                _preview = null;
                Context.Preview.Clear();
                return;
            }
            var axis = signedHeight < 0 ? Negate(_baseNormal) : _baseNormal;
            Show(new CadFrustumEntity(
                _center,
                axis,
                _baseRadius,
                Math.Max(_baseRadius * 0.5, MinSize),
                Math.Abs(signedHeight)));
            return;
        }

        if (Stage != 3) return;
        if (CreateFrustum(ProjectTopPoint(point)) is { } entity)
            Show(entity);
        else
        {
            _preview = null;
            Context.Preview.Clear();
        }
    }

    private OcctPoint3d ProjectBasePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_center, point, _xAxis, _yAxis);

    private OcctPoint3d ProjectTopPoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_topCenter, point, _xAxis, _yAxis);

    private CadFrustumEntity? CreateFrustum(OcctPoint3d point)
    {
        var topRadius = CadPlaneGeometry.RadialDistance(_topCenter, point, _xAxis, _yAxis);
        return topRadius <= 1e-9
            ? null
            : new CadFrustumEntity(_center, _axis, _baseRadius, topRadius, _height);
    }

    private bool SetHeight(OcctPoint3d point)
    {
        var signedHeight = CadPlaneGeometry.SignedDistance(_baseRadiusPoint, point, _baseNormal);
        if (Math.Abs(signedHeight) <= 1e-9) return false;
        _height = Math.Abs(signedHeight);
        _axis = signedHeight < 0 ? Negate(_baseNormal) : _baseNormal;
        _topCenter = CadTransformMath.Add(_center, _axis, _height);
        return true;
    }

    private void Show(CadFrustumEntity entity)
    {
        _preview = entity;
        ShowPreview(entity);
    }

    private static OcctVector3d Negate(OcctVector3d value) =>
        new(-value.X, -value.Y, -value.Z);
}
