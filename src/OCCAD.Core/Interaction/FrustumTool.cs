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
    private double? _baseRadiusParameter;
    private double? _heightParameter;
    private double? _topRadiusParameter;
    private CadFrustumEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "frustum";
    public override string DisplayName => "Frustum";
    public override string PrecisionLengthLabel => Stage switch
    {
        1 => "Base Radius",
        2 => "Height",
        3 => "Top Radius",
        _ => base.PrecisionLengthLabel
    };

    public override CadToolPanelDescriptor ParameterPanel =>
        new("Frustum", BuildParameters());

    protected override bool CanStepBackCore => Stage > 0;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        TryResolveExactStagePoint(out _)
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnActivated()
    {
        ResetState();
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

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
            return AcceptPoint(exactPoint);

        return CommitResolvedPoint(pointer, ReferencePoint(), AcceptPoint);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("BaseRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalPositive(value, out _baseRadiusParameter))
                return false;
        }
        else if (id.Equals("Height", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 2 || !TryOptionalPositive(value, out _heightParameter))
                return false;
        }
        else if (id.Equals("TopRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalPositive(value, out _topRadiusParameter))
                return false;
        }
        else
        {
            return false;
        }

        RefreshParameterDrivenPreview();
        NotifyUpdated();
        return true;
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
        {
            Update(exactPoint);
            return true;
        }

        return base.OnPrecisionInputApplied(input);
    }

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
            RefreshParameterDrivenPreview();
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
            RefreshParameterDrivenPreview();
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

    protected override void OnCanceled() => ResetState();

    private IReadOnlyList<CadToolParameterDescriptor> BuildParameters()
    {
        var parameters = new List<CadToolParameterDescriptor>(3);
        if (Stage <= 1)
        {
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "BaseRadius",
                "Base Radius",
                _baseRadiusParameter,
                1e-9,
                double.MaxValue));
        }
        if (Stage <= 2)
        {
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "Height",
                "Height",
                _heightParameter,
                1e-9,
                double.MaxValue));
        }
        parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
            "TopRadius",
            "Top Radius",
            _topRadiusParameter,
            1e-9,
            double.MaxValue));
        return parameters;
    }

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
                RefreshParameterDrivenPreview();
                return true;

            case 1:
            {
                var radiusPoint = ProjectBasePoint(point);
                var radius = _baseRadiusParameter ??
                    CadPlaneGeometry.RadialDistance(
                        _center,
                        radiusPoint,
                        _xAxis,
                        _yAxis);
                if (radius <= 1e-9) return false;

                _baseRadius = radius;
                _baseRadiusPoint = _baseRadiusParameter is not null
                    ? _center + _xAxis * radius
                    : radiusPoint;
                SetStageLocalized(
                    2,
                    "Cad.Prompt.Frustum.Height",
                    "Frustum: specify height [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.Length);
                SetWorkPlane(_baseRadiusPoint, _baseNormal, _xAxis);
                LockStageAngle(0.0);
                Show(new CadFrustumEntity(
                    _center,
                    _baseNormal,
                    _baseRadius,
                    _topRadiusParameter ?? Math.Max(_baseRadius * 0.5, MinSize),
                    _heightParameter ?? MinSize));
                RefreshParameterDrivenPreview();
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
                    _topRadiusParameter ?? Math.Max(_baseRadius * 0.5, MinSize),
                    _height));
                RefreshParameterDrivenPreview();
                return true;

            case 3:
            {
                var entity = CreateFrustum(ProjectTopPoint(point));
                if (entity is null) return false;
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
            var radius = _baseRadiusParameter ??
                CadPlaneGeometry.RadialDistance(
                    _center,
                    radiusPoint,
                    _xAxis,
                    _yAxis);
            if (radius > 1e-9)
            {
                Show(new CadFrustumEntity(
                    _center,
                    _baseNormal,
                    radius,
                    _topRadiusParameter ?? Math.Max(radius * 0.5, MinSize),
                    _heightParameter ?? MinSize));
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
            var pointerHeight =
                CadPlaneGeometry.SignedDistance(
                    _baseRadiusPoint,
                    point,
                    _baseNormal);
            var signedHeight = _heightParameter is { } parameterHeight
                ? pointerHeight < 0.0 ? -parameterHeight : parameterHeight
                : pointerHeight;
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
                _topRadiusParameter ?? Math.Max(_baseRadius * 0.5, MinSize),
                Math.Abs(signedHeight)));
            return;
        }

        if (Stage == 3 && CreateFrustum(ProjectTopPoint(point)) is { } entity)
            Show(entity);
    }

    private void RefreshParameterDrivenPreview()
    {
        if (TryResolveExactStagePoint(out var exactPoint))
        {
            Update(exactPoint);
            return;
        }

        if (Stage > 0 && Context.Workspace.LastPointerPosition is { } pointer)
        {
            Update(Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                ReferencePoint()).Point);
        }
    }

    private bool TryResolveExactStagePoint(out OcctPoint3d point)
    {
        point = default;
        var length = Stage switch
        {
            1 => _baseRadiusParameter ?? LockedLength(),
            2 => _heightParameter ?? LockedLength(),
            3 => _topRadiusParameter ?? LockedLength(),
            _ => null
        };
        if (length is not { } value || value <= 1e-9)
            return false;

        point = Stage switch
        {
            1 => _center + _xAxis * value,
            2 => _baseRadiusPoint + _baseNormal * value,
            3 => _topCenter + _xAxis * value,
            _ => default
        };
        return point.IsFinite;
    }

    private double? LockedLength() =>
        Context.Workspace.Drafting.LengthLockEnabled &&
        Context.Workspace.Drafting.LockedLength > 1e-9
            ? Context.Workspace.Drafting.LockedLength
            : null;

    private OcctPoint3d ProjectBasePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_center, point, _xAxis, _yAxis);

    private OcctPoint3d ProjectTopPoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_topCenter, point, _xAxis, _yAxis);

    private CadFrustumEntity? CreateFrustum(OcctPoint3d point)
    {
        var requestedRadius =
            CadPlaneGeometry.RadialDistance(
                _topCenter,
                point,
                _xAxis,
                _yAxis);
        var topRadius = _topRadiusParameter ?? requestedRadius;
        return topRadius <= 1e-9
            ? null
            : new CadFrustumEntity(
                _center,
                _axis,
                _baseRadius,
                topRadius,
                _height);
    }

    private bool SetHeight(OcctPoint3d point)
    {
        var pointerHeight =
            CadPlaneGeometry.SignedDistance(
                _baseRadiusPoint,
                point,
                _baseNormal);
        var signedHeight = _heightParameter is { } parameterHeight
            ? pointerHeight < 0.0 ? -parameterHeight : parameterHeight
            : pointerHeight;
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

    private void ResetState()
    {
        _preview = null;
        _baseRadiusParameter = null;
        _heightParameter = null;
        _topRadiusParameter = null;
    }

    private static bool TryOptionalPositive(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!CadValueTextConverter.TryParseFiniteDouble(text, out var parsed) ||
            parsed <= 0.0)
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }
}
