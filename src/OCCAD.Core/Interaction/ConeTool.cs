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
    private double? _radiusParameter;
    private double? _heightParameter;
    private CadConeEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "cone";
    public override string DisplayName => "Cone";
    public override string PrecisionLengthLabel => Stage switch
    {
        1 => "Base Radius",
        2 => "Height",
        _ => base.PrecisionLengthLabel
    };

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Cone",
            [
                new CadOptionalDoubleToolParameterDescriptor(
                    "BaseRadius",
                    "Base Radius",
                    _radiusParameter,
                    1e-9,
                    double.MaxValue),
                new CadOptionalDoubleToolParameterDescriptor(
                    "Height",
                    "Height",
                    _heightParameter,
                    1e-9,
                    double.MaxValue)
            ]);

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
            if (!TryOptionalPositive(value, out _radiusParameter))
                return false;
        }
        else if (id.Equals("Height", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalPositive(value, out _heightParameter))
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

        if (Stage == 2)
        {
            RestoreWorkPlaneFrame(_initialPlane, _center);
            SetStageLocalized(
                1,
                "Cad.Prompt.Cone.BaseRadius",
                "Cone: specify base radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            RefreshParameterDrivenPreview();
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
                _baseNormal = Context.WorkPlane.Normal;
                Context.WorkPlane.SetOrigin(point);
                SetStageLocalized(
                    1,
                    "Cad.Prompt.Cone.BaseRadius",
                    "Cone: specify base radius [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.Length);
                RefreshParameterDrivenPreview();
                return true;

            case 1:
                {
                    var radiusPoint = ProjectBasePoint(point);
                    var radius = _radiusParameter ??
                        CadPlaneGeometry.RadialDistance(
                            _center,
                            radiusPoint,
                            _xAxis,
                            _yAxis);
                    if (radius <= 1e-9)
                        return false;

                    _radius = radius;
                    _radiusPoint = _radiusParameter is not null
                        ? _center + _xAxis * radius
                        : radiusPoint;
                    SetStageLocalized(
                        2,
                        "Cad.Prompt.Cone.Height",
                        "Cone: specify height [Backspace undo, Esc cancel]",
                        CadPrecisionInputKind.Length);
                    SetWorkPlane(_radiusPoint, _baseNormal, _xAxis);
                    LockStageAngle(0.0);
                    Show(new CadConeEntity(
                        _center,
                        _baseNormal,
                        _radius,
                        _heightParameter ?? MinSize));
                    RefreshParameterDrivenPreview();
                    return true;
                }

            case 2:
                {
                    var entity = CreateCone(point);
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
        2 => _radiusPoint,
        _ => null
    };

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            var radiusPoint = ProjectBasePoint(point);
            var radius = _radiusParameter ??
                CadPlaneGeometry.RadialDistance(
                    _center,
                    radiusPoint,
                    _xAxis,
                    _yAxis);
            if (radius > 1e-9)
                Show(new CadConeEntity(
                    _center,
                    _baseNormal,
                    radius,
                    _heightParameter ?? MinSize));
            else
            {
                _preview = null;
                Context.Preview.Clear();
            }
            return;
        }

        if (Stage != 2) return;
        if (CreateCone(point) is { } entity)
            Show(entity);
        else
        {
            _preview = null;
            Context.Preview.Clear();
        }
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
        if (Stage == 1)
        {
            var radius = _radiusParameter ?? LockedLength();
            if (radius is not { } value || value <= 1e-9)
                return false;

            point = _center + _xAxis * value;
            return point.IsFinite;
        }

        if (Stage == 2)
        {
            var height = _heightParameter ?? LockedLength();
            if (height is not { } value || value <= 1e-9)
                return false;

            point = _radiusPoint + _baseNormal * value;
            return point.IsFinite;
        }

        return false;
    }

    private double? LockedLength() =>
        Context.Workspace.Drafting.LengthLockEnabled &&
        Context.Workspace.Drafting.LockedLength > 1e-9
            ? Context.Workspace.Drafting.LockedLength
            : null;

    private OcctPoint3d ProjectBasePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_center, point, _xAxis, _yAxis);

    private CadConeEntity? CreateCone(OcctPoint3d point)
    {
        var pointerHeight =
            CadPlaneGeometry.SignedDistance(_radiusPoint, point, _baseNormal);
        var signedHeight = _heightParameter is { } parameterHeight
            ? pointerHeight < 0.0 ? -parameterHeight : parameterHeight
            : pointerHeight;
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

    private void ResetState()
    {
        _preview = null;
        _radiusParameter = null;
        _heightParameter = null;
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
