using OcctNet;

namespace OCCAD;

public sealed class HelixTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d _origin;
    private OcctVector3d _axis;
    private OcctVector3d _xAxis;
    private double _radius;
    private double _pitch = 1.0;
    private double _turns = 3.0;
    private double? _radiusParameter;
    private double? _startAngleDegrees;
    private bool _pitchExplicit;
    private WorkPlaneFrame _initial;

    public override string Id => "helix";
    public override string DisplayName => "Helix";
    public override CadToolInputKind InputKind =>
        Stage == 2 ? CadToolInputKind.Confirmation : CadToolInputKind.Point;
    public override string PrecisionLengthLabel => Stage == 1
        ? "Radius"
        : base.PrecisionLengthLabel;
    public override string PrecisionAngleLabel => Stage == 1
        ? "Start Angle"
        : base.PrecisionAngleLabel;

    protected override bool CanStepBackCore => Stage > 0;
    protected override bool CanFinishCore => Stage == 2;

    public override bool CanCommitCurrentStage
    {
        get
        {
            if (!IsActive || State != CadToolState.Drawing)
                return false;
            if (Stage == 2)
                return false;
            if (Stage == 1 && TryResolveExactRadiusPoint(out _))
                return true;
            return base.CanCommitCurrentStage;
        }
    }

    public override CadToolPanelDescriptor ParameterPanel =>
        new("Helix", BuildParameters());

    protected override void OnActivated()
    {
        ResetState();
        _initial = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Helix.Center",
            "Helix: specify axis origin [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && Stage == 1)
        {
            Update(Context.ResolvePoint(input.X, input.Y, _origin).Point);
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left ||
            Stage >= 2)
            return false;

        return Accept(
            Context.ResolvePoint(
                input.X,
                input.Y,
                Stage == 1 ? _origin : null).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (Stage == 2)
            return false;
        if (Stage == 1 && TryResolveExactRadiusPoint(out var exactPoint))
            return Accept(exactPoint);

        return CommitResolvedPoint(
            pointer,
            Stage == 1 ? _origin : null,
            Accept);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        Stage < 2 &&
        Accept(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Radius", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalPositive(value, out _radiusParameter))
                return false;
        }
        else if (id.Equals("StartAngle", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalFinite(value, out _startAngleDegrees))
                return false;
        }
        else
        {
            if (!CadValueTextConverter.TryParseFiniteDouble(value, out var number))
                return false;

            if (id.Equals("Pitch", StringComparison.OrdinalIgnoreCase))
            {
                if (Math.Abs(number) <= 1e-9 || Math.Abs(number) > 1000000.0)
                    return false;
                _pitch = number;
                _pitchExplicit = true;
            }
            else if (id.Equals("Turns", StringComparison.OrdinalIgnoreCase))
            {
                if (number <= 0.0 || number > 10000.0)
                    return false;
                _turns = number;
            }
            else
            {
                return false;
            }
        }

        RefreshParameterDrivenPreview();
        NotifyUpdated();
        return true;
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (Stage == 1 && TryResolveExactRadiusPoint(out var exactPoint))
        {
            Update(exactPoint);
            return true;
        }

        return base.OnPrecisionInputApplied(input);
    }

    protected override bool OnFinish()
    {
        if (!CanFinishCore) return false;
        CommitPreview(
            new CadHelixEntity(
                _origin,
                _axis,
                _xAxis,
                _radius,
                _pitch,
                _turns));
        return true;
    }

    protected override bool OnStepBack()
    {
        Context.Preview.Clear();
        if (Stage == 2)
        {
            RestoreWorkPlaneFrame(_initial, _origin);
            SetStageLocalized(
                1,
                "Cad.Prompt.Helix.Radius",
                "Helix: specify radius and start direction [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            RefreshParameterDrivenPreview();
            return true;
        }

        RestoreWorkPlaneFrame(_initial);
        SetStageLocalized(
            0,
            "Cad.Prompt.Helix.Center",
            "Helix: specify axis origin [Esc cancel]");
        return true;
    }

    protected override void OnCanceled() => ResetState();

    private IReadOnlyList<CadToolParameterDescriptor> BuildParameters()
    {
        var parameters = new List<CadToolParameterDescriptor>(4);
        if (Stage <= 1)
        {
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "Radius",
                "Radius",
                _radiusParameter,
                1e-9,
                1e12));
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "StartAngle",
                "Start Angle",
                _startAngleDegrees,
                -360000.0,
                360000.0));
        }

        parameters.Add(new CadDoubleToolParameterDescriptor(
            "Pitch",
            "Pitch",
            _pitch,
            -1000000,
            1000000));
        parameters.Add(new CadDoubleToolParameterDescriptor(
            "Turns",
            "Turns",
            _turns,
            0.01,
            10000));
        return parameters;
    }

    private bool Accept(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (Stage == 0)
        {
            _origin = point;
            _axis = Context.WorkPlane.Normal;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(
                1,
                "Cad.Prompt.Helix.Radius",
                "Helix: specify radius and start direction [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            RefreshParameterDrivenPreview();
            return true;
        }

        if (Stage != 1)
            return false;

        if (!TryResolveRadiusAndAxis(point, out var radius, out var xAxis))
            return false;

        _xAxis = xAxis;
        _radius = radius;
        if (!_pitchExplicit)
            _pitch = _radius;

        SetStageLocalized(
            2,
            "Cad.Prompt.Helix.Parameters",
            "Helix: set pitch and turns [Enter accept, Esc cancel]");
        Show();
        return true;
    }

    private void Update(OcctPoint3d point)
    {
        if (Stage != 1)
            return;

        if (!TryResolveRadiusAndAxis(point, out var radius, out var xAxis))
        {
            Context.Preview.Clear();
            return;
        }

        var pitch = _pitchExplicit ? _pitch : radius;
        ShowPreview(
            new CadHelixEntity(
                _origin,
                _axis,
                xAxis,
                radius,
                pitch,
                _turns));
    }

    private void RefreshParameterDrivenPreview()
    {
        if (Stage == 2)
        {
            Show();
            return;
        }

        if (Stage == 1 && TryResolveExactRadiusPoint(out var exactPoint))
        {
            Update(exactPoint);
            return;
        }

        if (Stage == 1 && Context.Workspace.LastPointerPosition is { } pointer)
        {
            Update(Context.ResolvePoint(pointer.X, pointer.Y, _origin).Point);
        }
    }

    private bool TryResolveRadiusAndAxis(
        OcctPoint3d point,
        out double radius,
        out OcctVector3d xAxis)
    {
        radius = 0.0;
        xAxis = default;

        var direction = point - _origin;
        direction -= _axis * direction.Dot(_axis);
        var pointerRadius = direction.Length;
        radius = _radiusParameter ?? LockedLength() ?? pointerRadius;
        if (!double.IsFinite(radius) || radius <= 1e-9)
            return false;

        var angle = _startAngleDegrees ?? LockedAngle();
        if (angle is { } angleValue)
        {
            var candidate = AxisFromAngle(angleValue);
            if (!candidate.TryNormalize(out var normalized))
                return false;
            xAxis = normalized;
            return true;
        }

        return direction.TryNormalize(out xAxis);
    }

    private bool TryResolveExactRadiusPoint(out OcctPoint3d point)
    {
        point = default;
        var radius = _radiusParameter ?? LockedLength();
        var angle = _startAngleDegrees ?? LockedAngle();
        if (radius is not { } radiusValue || radiusValue <= 1e-9 ||
            angle is not { } angleValue || !double.IsFinite(angleValue))
            return false;

        point = _origin + AxisFromAngle(angleValue) * radiusValue;
        return point.IsFinite;
    }

    private OcctVector3d AxisFromAngle(double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new OcctVector3d(
            _initial.XAxis.X * Math.Cos(radians) + _initial.YAxis.X * Math.Sin(radians),
            _initial.XAxis.Y * Math.Cos(radians) + _initial.YAxis.Y * Math.Sin(radians),
            _initial.XAxis.Z * Math.Cos(radians) + _initial.YAxis.Z * Math.Sin(radians)).Normalized();
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

    private void Show() =>
        ShowPreview(
            new CadHelixEntity(
                _origin,
                _axis,
                _xAxis,
                _radius,
                _pitch,
                _turns));

    private void ResetState()
    {
        _radius = 0.0;
        _pitch = 1.0;
        _turns = 3.0;
        _radiusParameter = null;
        _startAngleDegrees = null;
        _pitchExplicit = false;
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
