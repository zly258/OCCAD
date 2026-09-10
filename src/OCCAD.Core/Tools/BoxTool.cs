using OcctNet;

namespace OCCAD;

public sealed class BoxTool : CadDrawingTool, ICadPointInputTool
{
    private const double MinSize = 0.1;

    private OcctPoint3d _start;
    private OcctPoint3d _second;
    private OcctPoint3d _baseOrigin;
    private OcctVector3d _baseX;
    private OcctVector3d _baseY;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _zAxis;
    private double _length;
    private double _width;
    private double _signX = 1.0;
    private double _signY = 1.0;
    private double? _lengthParameter;
    private double? _widthParameter;
    private double? _heightParameter;
    private double? _angleDegrees;
    private CadBoxEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "box";
    public override string DisplayName => "Box";

    public override string PrecisionLengthLabel => Stage == 2
        ? "Height"
        : "Base Diagonal";

    public override CadToolParameterSchema ParameterSchema =>
        new("Box", BuildParameters());

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
        CaptureBasePlane();
        SetStageLocalized(
            0,
            "Cad.Prompt.Box.First",
            "Box: specify first corner [Esc cancel]");
    }

    protected internal override void OnWorkPlaneChanged()
    {
        if (Stage != 0)
            return;

        CaptureBasePlane();
        RefreshParameterDrivenPreview();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (input.Kind == OcctPointerInputKind.Moved && Stage > 0)
        {
            Update(Context.ResolvePoint(input.X, input.Y, ReferencePoint()).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(input.X, input.Y, ReferencePoint()).Point);
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
        if (id.Equals("Length", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalPositive(value, out _lengthParameter))
                return false;
        }
        else if (id.Equals("Width", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalPositive(value, out _widthParameter))
                return false;
        }
        else if (id.Equals("Height", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 2 || !TryOptionalPositive(value, out _heightParameter))
                return false;
        }
        else if (id.Equals("Angle", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage > 1 || !TryOptionalFinite(value, out _angleDegrees))
                return false;
            UpdateAxes();
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

        RefreshParameterDrivenPreview();
        return true;
    }

    protected override bool OnStepBack()
    {
        Context.Preview.Clear();
        _preview = null;

        if (Stage == 2)
        {
            SetWorkPlane(
                _start,
                _xAxis,
                _yAxis,
                lockPlane: true);
            SetStageLocalized(
                1,
                "Cad.Prompt.Box.Opposite",
                "Box: specify opposite base corner [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            RefreshParameterDrivenPreview();
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

    private IReadOnlyList<CadToolParameterDescriptor> BuildParameters()
    {
        var parameters = new List<CadToolParameterDescriptor>(4);
        if (Stage <= 1)
        {
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "Length",
                "Length",
                _lengthParameter,
                1e-9,
                double.MaxValue));
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "Width",
                "Width",
                _widthParameter,
                1e-9,
                double.MaxValue));
            parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
                "Angle",
                "Angle",
                _angleDegrees,
                -360000.0,
                360000.0));
        }

        parameters.Add(new CadOptionalDoubleToolParameterDescriptor(
            "Height",
            "Height",
            _heightParameter,
            1e-9,
            double.MaxValue));
        return parameters;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        switch (Stage)
        {
            case 0:
                _start = point;
                _second = point;
                _zAxis = Context.WorkPlane.Normal;
                SetWorkPlane(
                    point,
                    _xAxis,
                    _yAxis,
                    lockPlane: true);
                SetStageLocalized(
                    1,
                    "Cad.Prompt.Box.Opposite",
                    "Box: specify opposite base corner [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.LengthAndAngle);
                RefreshParameterDrivenPreview();
                return true;

            case 1:
            {
                var basePoint = ProjectBasePoint(point);
                if (!SetBase(basePoint))
                    return false;

                SetStageLocalized(
                    2,
                    "Cad.Prompt.Box.Height",
                    "Box: specify height [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.Length);
                SetWorkPlane(
                    _second,
                    _zAxis,
                    _xAxis,
                    lockPlane: true);
                LockStageAngle(0.0);
                Show(new CadBoxEntity(
                    _baseOrigin,
                    _xAxis,
                    _yAxis,
                    _zAxis,
                    _length,
                    _width,
                    _heightParameter ?? MinSize));
                RefreshParameterDrivenPreview();
                return true;
            }

            case 2:
                Update(point);
                if (_preview is null)
                    return false;
                CommitPreview(_preview);
                return true;

            default:
                return false;
        }
    }

    private OcctPoint3d? ReferencePoint() => Stage switch
    {
        1 => _start,
        2 => _second,
        _ => null
    };

    private bool TryResolveExactStagePoint(out OcctPoint3d point)
    {
        point = default;

        if (Stage == 1 &&
            _lengthParameter is { } length && length > 1e-9 &&
            _widthParameter is { } width && width > 1e-9)
        {
            point = _start + _xAxis * length + _yAxis * width;
            return point.IsFinite;
        }

        if (Stage == 1 &&
            CadExactInputGeometry.TryResolveLengthAnglePoint(
                Context.Workspace,
                _start,
                out point))
        {
            return point.IsFinite;
        }

        if (Stage == 2)
        {
            var height = _heightParameter ?? LockedLength();
            if (height is not { } value || value <= 1e-9)
                return false;

            point = _second + _zAxis * value;
            return point.IsFinite;
        }

        return false;
    }

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            if (!TryResolveBase(
                    ProjectBasePoint(point),
                    out var origin,
                    out var length,
                    out var width,
                    out _,
                    out _))
            {
                _preview = null;
                Context.Preview.Clear();
                return;
            }

            Show(new CadBoxEntity(
                origin,
                _xAxis,
                _yAxis,
                _zAxis,
                length,
                width,
                _heightParameter ?? MinSize));
            return;
        }

        if (Stage != 2)
            return;

        var pointerHeight =
            CadPlaneGeometry.SignedDistance(_second, point, _zAxis);
        var signedHeight = _heightParameter is { } parameterHeight
            ? pointerHeight < 0.0 ? -parameterHeight : parameterHeight
            : pointerHeight;
        if (Math.Abs(signedHeight) <= 1e-9)
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }

        var size = Math.Max(Math.Abs(signedHeight), MinSize);
        var heightOrigin = signedHeight < 0.0
            ? CadTransformMath.Add(_baseOrigin, _zAxis, signedHeight)
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

    private void RefreshParameterDrivenPreview()
    {
        if (Stage > 0 && TryResolveExactStagePoint(out var exactPoint))
        {
            Update(exactPoint);
            return;
        }

        if (Stage > 0 &&
            Context.Workspace.LastPointerPosition is { } pointer)
        {
            Update(Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                ReferencePoint()).Point);
        }
    }

    private OcctPoint3d ProjectBasePoint(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(
            _start,
            point,
            _xAxis,
            _yAxis);

    private bool SetBase(OcctPoint3d point)
    {
        if (!TryResolveBase(
                point,
                out var origin,
                out var length,
                out var width,
                out var signX,
                out var signY))
            return false;

        _baseOrigin = origin;
        _length = length;
        _width = width;
        _signX = signX;
        _signY = signY;
        _second =
            _start +
            _xAxis * (_signX * _length) +
            _yAxis * (_signY * _width);
        return _second.IsFinite;
    }

    private bool TryResolveBase(
        OcctPoint3d point,
        out OcctPoint3d origin,
        out double length,
        out double width,
        out double signX,
        out double signY)
    {
        origin = _start;
        length = 0.0;
        width = 0.0;
        signX = 1.0;
        signY = 1.0;

        var delta = CadTransformMath.Between(_start, point);
        var dx = CadTransformMath.Dot(delta, _xAxis);
        var dy = CadTransformMath.Dot(delta, _yAxis);
        if (!double.IsFinite(dx) || !double.IsFinite(dy))
            return false;

        if (Math.Abs(dx) > 1e-9)
            signX = dx < 0.0 ? -1.0 : 1.0;
        if (Math.Abs(dy) > 1e-9)
            signY = dy < 0.0 ? -1.0 : 1.0;

        length = _lengthParameter ?? Math.Abs(dx);
        width = _widthParameter ?? Math.Abs(dy);
        if (!double.IsFinite(length) ||
            !double.IsFinite(width) ||
            length <= 1e-9 ||
            width <= 1e-9)
            return false;

        if (signX < 0.0)
            origin = CadTransformMath.Add(origin, _xAxis, -length);
        if (signY < 0.0)
            origin = CadTransformMath.Add(origin, _yAxis, -width);
        return origin.IsFinite;
    }

    private void CaptureBasePlane()
    {
        _initialPlane = CaptureWorkPlaneFrame();
        _baseX = _initialPlane.XAxis;
        _baseY = _initialPlane.YAxis;
        _zAxis = _initialPlane.XAxis.Cross(_initialPlane.YAxis).Normalized();
        UpdateAxes();
    }

    private void UpdateAxes()
    {
        var angle = _angleDegrees ?? 0.0;
        var radians = angle * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        var x = new OcctVector3d(
            _baseX.X * cos + _baseY.X * sin,
            _baseX.Y * cos + _baseY.Y * sin,
            _baseX.Z * cos + _baseY.Z * sin);
        var y = new OcctVector3d(
            -_baseX.X * sin + _baseY.X * cos,
            -_baseX.Y * sin + _baseY.Y * cos,
            -_baseX.Z * sin + _baseY.Z * cos);

        _xAxis = x.TryNormalize(out var normalizedX)
            ? normalizedX
            : _baseX;
        _yAxis = y.TryNormalize(out var normalizedY)
            ? normalizedY
            : _baseY;
    }

    private double? LockedLength() =>
        Context.Workspace.Drafting.LengthLockEnabled &&
        Context.Workspace.Drafting.LockedLength > 1e-9
            ? Context.Workspace.Drafting.LockedLength
            : null;

    private void Show(CadBoxEntity entity)
    {
        _preview = entity;
        ShowPreview(entity);
    }

    private void ResetState()
    {
        _preview = null;
        _length = 0.0;
        _width = 0.0;
        _signX = 1.0;
        _signY = 1.0;
        _lengthParameter = null;
        _widthParameter = null;
        _heightParameter = null;
        _angleDegrees = null;
    }

    private static bool TryOptionalPositive(
        string text,
        out double? value)
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

    private static bool TryOptionalFinite(
        string text,
        out double? value)
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
