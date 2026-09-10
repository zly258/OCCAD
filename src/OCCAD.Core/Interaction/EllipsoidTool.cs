using OcctNet;

namespace OCCAD;

public sealed class EllipsoidTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d _center;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _zAxis;
    private double _rx;
    private double _ry;
    private double? _rxParameter;
    private double? _ryParameter;
    private double? _rzParameter;
    private double? _angleDegrees;
    private CadEllipsoidEntity? _preview;
    private WorkPlaneFrame _initial;

    public override string Id => "ellipsoid";
    public override string DisplayName => "Ellipsoid";
    public override string PrecisionLengthLabel => Stage switch
    {
        1 => "X Semi-axis",
        2 => "Y Semi-axis",
        3 => "Z Semi-axis",
        _ => base.PrecisionLengthLabel
    };
    public override string PrecisionAngleLabel => Stage == 1
        ? "Orientation"
        : base.PrecisionAngleLabel;

    public override CadToolPanelDescriptor ParameterPanel =>
        new("Ellipsoid", BuildParameters());

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
        _initial = CaptureWorkPlaneFrame();
        _zAxis = _initial.XAxis.Cross(_initial.YAxis).Normalized();
        SetStageLocalized(
            0,
            "Cad.Prompt.Ellipsoid.Center",
            "Ellipsoid: specify center [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && Stage > 0)
        {
            Update(Context.ResolvePoint(input.X, input.Y, Reference()).Point);
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;
        return Accept(Context.ResolvePoint(input.X, input.Y, Reference()).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
            return Accept(exactPoint);

        return CommitResolvedPoint(pointer, Reference(), Accept);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && Accept(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("XRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalPositive(value, out _rxParameter))
                return false;
        }
        else if (id.Equals("YRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 2 || !TryOptionalPositive(value, out _ryParameter))
                return false;
        }
        else if (id.Equals("ZRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalPositive(value, out _rzParameter))
                return false;
        }
        else if (id.Equals("Angle", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalFinite(value, out _angleDegrees))
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
            RestoreWorkPlaneFrame(_initial, _center);
            SetStageLocalized(
                2,
                "Cad.Prompt.Ellipsoid.YRadius",
                "Ellipsoid: specify Y semi-axis [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            RefreshParameterDrivenPreview();
            return true;
        }
        if (Stage == 2)
        {
            RestoreWorkPlaneFrame(_initial, _center);
            SetStageLocalized(
                1,
                "Cad.Prompt.Ellipsoid.XRadius",
                "Ellipsoid: specify X semi-axis and orientation [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            RefreshParameterDrivenPreview();
            return true;
        }
        if (Stage == 1)
        {
            RestoreWorkPlaneFrame(_initial);
            SetStageLocalized(
                0,
                "Cad.Prompt.Ellipsoid.Center",
                "Ellipsoid: specify center [Esc cancel]");
            return true;
        }
        return false;
    }

    protected override void OnCanceled() => ResetState();

    private IReadOnlyList<CadToolParameterDescriptor> BuildParameters()
    {
        var parameters = new List<CadToolParameterDescriptor>(4);
        if (Stage <= 1)
        {
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "XRadius",
                "X Semi-axis",
                _rxParameter,
                1e-9,
                double.MaxValue));
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "Angle",
                "Orientation",
                _angleDegrees,
                -360000.0,
                360000.0));
        }

        if (Stage <= 2)
        {
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "YRadius",
                "Y Semi-axis",
                _ryParameter,
                1e-9,
                double.MaxValue));
        }

        parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
            "ZRadius",
            "Z Semi-axis",
            _rzParameter,
            1e-9,
            double.MaxValue));
        return parameters;
    }

    private OcctPoint3d? Reference() => Stage > 0 ? _center : null;

    private bool Accept(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (Stage == 0)
        {
            _center = point;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(
                1,
                "Cad.Prompt.Ellipsoid.XRadius",
                "Ellipsoid: specify X semi-axis and orientation [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            RefreshParameterDrivenPreview();
            return true;
        }

        if (Stage == 1)
        {
            if (!TrySetPrimaryAxis(point))
                return false;

            Show(
                _rx,
                _ryParameter ?? _rx,
                _rzParameter ?? _ryParameter ?? _rx);
            SetStageLocalized(
                2,
                "Cad.Prompt.Ellipsoid.YRadius",
                "Ellipsoid: specify Y semi-axis [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            RefreshParameterDrivenPreview();
            return true;
        }

        if (Stage == 2)
        {
            _ry = _ryParameter ?? Math.Abs((point - _center).Dot(_yAxis));
            if (_ry <= 1e-9) return false;
            Show(_rx, _ry, _rzParameter ?? _ry);
            Context.WorkPlane.SetToolPlaneFixed(false);
            Context.WorkPlane.SetToolPlane(_center, _zAxis, _xAxis);
            SetStageLocalized(
                3,
                "Cad.Prompt.Ellipsoid.ZRadius",
                "Ellipsoid: specify Z semi-axis [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            RefreshParameterDrivenPreview();
            return true;
        }

        var rz = _rzParameter ?? Math.Abs((point - _center).Dot(_zAxis));
        if (rz <= 1e-9) return false;
        Show(_rx, _ry, rz);
        if (_preview is null) return false;
        CommitPreview(_preview);
        return true;
    }

    private bool TrySetPrimaryAxis(OcctPoint3d point)
    {
        var direction = Planar(point - _center);
        var radius = _rxParameter ?? direction.Length;
        if (!double.IsFinite(radius) || radius <= 1e-9)
            return false;

        if (_angleDegrees is { } parameterAngle)
        {
            _xAxis = AxisFromAngle(parameterAngle);
        }
        else if (Context.Workspace.Drafting.AngleLockEnabled &&
                 double.IsFinite(Context.Workspace.Drafting.LockedAngleDegrees))
        {
            _xAxis = AxisFromAngle(Context.Workspace.Drafting.LockedAngleDegrees);
        }
        else if (!direction.TryNormalize(out _xAxis))
        {
            return false;
        }

        _rx = radius;
        _zAxis = _initial.XAxis.Cross(_initial.YAxis).Normalized();
        _yAxis = _zAxis.Cross(_xAxis).Normalized();
        return true;
    }

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            if (TrySetPrimaryAxis(point))
            {
                Show(
                    _rx,
                    _ryParameter ?? _rx,
                    _rzParameter ?? _ryParameter ?? _rx);
            }
            else
            {
                Context.Preview.Clear();
            }
            return;
        }
        if (Stage == 2)
        {
            var radius = _ryParameter ?? Math.Abs((point - _center).Dot(_yAxis));
            if (radius > 1e-9)
                Show(_rx, radius, _rzParameter ?? radius);
            else
                Context.Preview.Clear();
            return;
        }
        if (Stage == 3)
        {
            var radius = _rzParameter ?? Math.Abs((point - _center).Dot(_zAxis));
            if (radius > 1e-9)
                Show(_rx, _ry, radius);
            else
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
                Reference()).Point);
        }
    }

    private bool TryResolveExactStagePoint(out OcctPoint3d point)
    {
        point = default;
        if (Stage == 1)
        {
            var radius = _rxParameter ?? LockedLength();
            var angle = _angleDegrees ?? LockedAngle();
            if (radius is not { } radiusValue || radiusValue <= 1e-9 ||
                angle is not { } angleValue || !double.IsFinite(angleValue))
                return false;

            point = _center + AxisFromAngle(angleValue) * radiusValue;
            return point.IsFinite;
        }

        if (Stage == 2)
        {
            var radius = _ryParameter ?? LockedLength();
            if (radius is not { } value || value <= 1e-9)
                return false;

            point = _center + _yAxis * value;
            return point.IsFinite;
        }

        if (Stage == 3)
        {
            var radius = _rzParameter ?? LockedLength();
            if (radius is not { } value || value <= 1e-9)
                return false;

            point = _center + _zAxis * value;
            return point.IsFinite;
        }

        return false;
    }

    private double? LockedLength() =>
        Context.Workspace.Drafting.LengthLockEnabled &&
        Context.Workspace.Drafting.LockedLength > 1e-9
            ? Context.Workspace.Drafting.LockedLength
            : null;

    private double? LockedAngle() =>
        Context.Workspace.Drafting.AngleLockEnabled &&
        double.IsFinite(Context.Workspace.Drafting.LockedAngleDegrees)
            ? Context.Workspace.Drafting.LockedAngleDegrees
            : null;

    private OcctVector3d AxisFromAngle(double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new OcctVector3d(
            _initial.XAxis.X * cos + _initial.YAxis.X * sin,
            _initial.XAxis.Y * cos + _initial.YAxis.Y * sin,
            _initial.XAxis.Z * cos + _initial.YAxis.Z * sin).Normalized();
    }

    private OcctVector3d Planar(OcctVector3d direction)
    {
        var normal = _initial.XAxis.Cross(_initial.YAxis).Normalized();
        return direction - normal * direction.Dot(normal);
    }

    private void Show(double x, double y, double z)
    {
        _preview = new CadEllipsoidEntity(
            _center,
            _xAxis,
            _yAxis,
            _zAxis,
            x,
            y,
            z);
        ShowPreview(_preview);
    }

    private void ResetState()
    {
        _preview = null;
        _rxParameter = null;
        _ryParameter = null;
        _rzParameter = null;
        _angleDegrees = null;
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

    private static bool TryOptionalFinite(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!CadValueTextConverter.TryParseFiniteDouble(text, out var parsed))
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }
}
