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
    private double? _majorRadiusParameter;
    private double? _tubeRadiusParameter;
    private CadTorusEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "torus";
    public override string DisplayName => "Torus";
    public override string PrecisionLengthLabel => Stage == 2
        ? "Tube Radius"
        : "Major Radius";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Torus",
            [
                new CadOptionalDoubleToolParameterDescriptor(
                    "MajorRadius",
                    "Major Radius",
                    _majorRadiusParameter,
                    1e-9,
                    double.MaxValue),
                new CadOptionalDoubleToolParameterDescriptor(
                    "TubeRadius",
                    "Tube Radius",
                    _tubeRadiusParameter,
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
        Reset();
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Torus.Center",
            "Torus: specify center [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (input.Kind == OcctPointerInputKind.Moved && Stage > 0)
        {
            Update(
                Context.ResolvePoint(
                    input.X,
                    input.Y,
                    ReferencePoint()).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                ReferencePoint()).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
            return AcceptPoint(exactPoint);

        return CommitResolvedPoint(pointer, ReferencePoint(), AcceptPoint);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("MajorRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalPositive(value, out var major) ||
                !CompatibleRadii(major, _tubeRadiusParameter))
                return false;
            _majorRadiusParameter = major;
        }
        else if (id.Equals("TubeRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalPositive(value, out var tube) ||
                !CompatibleRadii(_majorRadiusParameter ?? Positive(_majorRadius), tube))
                return false;
            _tubeRadiusParameter = tube;
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
                "Cad.Prompt.Torus.MajorRadius",
                "Torus: specify major radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            RefreshParameterDrivenPreview();
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
        if (!point.IsFinite)
            return false;

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
            RefreshParameterDrivenPreview();
            return true;
        }

        if (Stage == 1)
        {
            var majorPoint = ProjectMajorPoint(point);
            var major = _majorRadiusParameter ??
                CadPlaneGeometry.RadialDistance(
                    _center,
                    majorPoint,
                    _xAxis,
                    _yAxis);
            if (major <= 1e-9 ||
                !CompatibleRadii(major, _tubeRadiusParameter))
                return false;

            _majorRadius = major;
            _majorPoint = _majorRadiusParameter is not null
                ? _center + _xAxis * major
                : majorPoint;
            _radialAxis = CadTransformMath.Normalize(
                CadTransformMath.Between(_center, _majorPoint),
                nameof(_majorPoint));
            SetStageLocalized(
                2,
                "Cad.Prompt.Torus.TubeRadius",
                "Torus: specify tube radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            SetWorkPlane(_majorPoint, _radialAxis, _axis);
            Show(
                new CadTorusEntity(
                    _center,
                    _axis,
                    _majorRadius,
                    _tubeRadiusParameter ?? _majorRadius * 0.2));
            RefreshParameterDrivenPreview();
            return true;
        }

        var tubePoint = ProjectTubePoint(point);
        if (_majorPoint.DistanceTo(tubePoint) <= 1e-9 &&
            _tubeRadiusParameter is null)
            return false;

        Update(tubePoint);
        if (_preview is null)
            return false;

        CommitPreview(_preview);
        return true;
    }

    private OcctPoint3d? ReferencePoint() =>
        Stage switch
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
            var major = _majorRadiusParameter ??
                CadPlaneGeometry.RadialDistance(
                    _center,
                    majorPoint,
                    _xAxis,
                    _yAxis);
            if (major <= 1e-9 ||
                !CompatibleRadii(major, _tubeRadiusParameter))
            {
                _preview = null;
                Context.Preview.Clear();
                return;
            }

            Show(
                new CadTorusEntity(
                    _center,
                    _axis,
                    major,
                    _tubeRadiusParameter ?? major * 0.2));
            return;
        }

        if (Stage != 2)
            return;

        var tubePoint = ProjectTubePoint(point);
        var requested = _tubeRadiusParameter ??
            _majorPoint.DistanceTo(tubePoint);
        if (!CompatibleRadii(_majorRadius, requested))
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }

        var minorRadius = _tubeRadiusParameter ??
            ResolveMinorRadius(_majorRadius, requested);
        Show(
            new CadTorusEntity(
                _center,
                _axis,
                _majorRadius,
                minorRadius));
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
        var radius = Stage switch
        {
            1 => _majorRadiusParameter ?? LockedLength(),
            2 => _tubeRadiusParameter ?? LockedLength(),
            _ => null
        };
        if (radius is not { } value || value <= 1e-9)
            return false;

        if (Stage == 2 && !CompatibleRadii(_majorRadius, value))
            return false;

        point = Stage switch
        {
            1 => _center + _xAxis * value,
            2 => _majorPoint + _radialAxis * value,
            _ => default
        };
        return point.IsFinite;
    }

    private double? LockedLength() =>
        Context.Workspace.Drafting.LengthLockEnabled &&
        Context.Workspace.Drafting.LockedLength > 1e-9
            ? Context.Workspace.Drafting.LockedLength
            : null;

    private OcctPoint3d ProjectMajorPoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(
            _center,
            point,
            _xAxis,
            _yAxis);

    private OcctPoint3d ProjectTubePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(
            _majorPoint,
            point,
            _radialAxis,
            _axis);

    private void Show(CadTorusEntity entity)
    {
        _preview = entity;
        ShowPreview(entity);
    }

    private static bool CompatibleRadii(double? major, double? tube) =>
        major is null ||
        tube is null ||
        tube.Value < major.Value * 0.5;

    private static double? Positive(double value) =>
        double.IsFinite(value) && value > 1e-9 ? value : null;

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
        _majorRadius = 0.0;
        _majorRadiusParameter = null;
        _tubeRadiusParameter = null;
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
