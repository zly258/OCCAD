using OcctNet;

namespace OCCAD;

public sealed class LengthDimensionTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d? _start;
    private OcctPoint3d? _end;
    private double _textHeight = 4.0;
    private double _arrowSize = 2.5;
    private string _fontName = "Arial";
    private CadLengthDimensionEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "lengthdimension";
    public override string DisplayName => "Length Dimension";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Length Dimension",
            [
                new CadDoubleToolParameterDescriptor(
                    "TextHeight",
                    "Text Height",
                    _textHeight,
                    1e-9,
                    double.MaxValue),
                new CadDoubleToolParameterDescriptor(
                    "ArrowSize",
                    "Arrow Size",
                    _arrowSize,
                    1e-9,
                    double.MaxValue),
                new CadStringToolParameterDescriptor(
                    "Font",
                    "Font",
                    _fontName)
            ]);

    protected override bool CanStepBackCore => _start is not null;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        TryResolveExactStagePoint(out _)
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnActivated()
    {
        _start = null;
        _end = null;
        _preview = null;
        _initialPlane = CaptureWorkPlaneFrame();
        RestorePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            var reference = _end is not null
                ? Context.WorkPlane.Origin
                : _start;
            var point = Context.ResolvePoint(input.X, input.Y, reference).Point;
            if (_end is { } end && _start is { } start)
                UpdatePreview(start, end, point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        var referencePoint = _end is not null
            ? Context.WorkPlane.Origin
            : _start;
        return AcceptPoint(
            Context.ResolvePoint(input.X, input.Y, referencePoint).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
            return AcceptPoint(exactPoint);

        var reference = _end is not null
            ? Context.WorkPlane.Origin
            : _start;
        return CommitResolvedPoint(pointer, reference, AcceptPoint);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
        {
            if (_start is { } start && _end is { } end)
                UpdatePreview(start, end, exactPoint);
            return true;
        }

        return base.OnPrecisionInputApplied(input);
    }

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryPositive(value, out _textHeight))
                return false;
        }
        else if (id.Equals("ArrowSize", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryPositive(value, out _arrowSize))
                return false;
        }
        else if (id.Equals("Font", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            _fontName = value.Trim();
        }
        else
        {
            return false;
        }

        RefreshPreviewFromLastPointer();
        NotifyUpdated();
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_end is not null)
        {
            _end = null;
            _preview = null;
            Context.Preview.Clear();
            if (_start is { } start)
                RestoreWorkPlaneFrame(_initialPlane, start);
            RestorePrompt();
            return true;
        }

        if (_start is null)
            return false;

        _start = null;
        _preview = null;
        Context.Preview.Clear();
        RestoreWorkPlaneFrame(_initialPlane);
        RestorePrompt();
        return true;
    }

    protected override void OnCanceled()
    {
        _start = null;
        _end = null;
        _preview = null;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        if (_start is null)
        {
            _start = point;
            RestoreWorkPlaneFrame(_initialPlane, point);
            RestorePrompt();
            return true;
        }

        if (_end is null)
        {
            if (_start.Value.DistanceTo(point) <= 1e-9)
                return false;

            _end = point;
            if (!ConfigureDimensionLineStage(_start.Value, point))
            {
                _end = null;
                return false;
            }
            return true;
        }

        if (!TryCreateEntity(_start.Value, _end.Value, point, out var entity))
            return false;

        _preview = null;
        CommitPreview(entity);
        return true;
    }

    private bool ConfigureDimensionLineStage(
        OcctPoint3d start,
        OcctPoint3d end)
    {
        var segment = end - start;
        if (!segment.TryNormalize(out var axis))
            return false;

        var normal = Context.WorkPlane.Normal;
        var offsetDirection = normal.Cross(axis);
        if (!offsetDirection.TryNormalize(out var yAxis))
            return false;

        var middle = start + segment * 0.5;
        SetWorkPlane(middle, axis, yAxis, lockPlane: true);
        RestorePrompt();
        LockStageAngle(90.0);
        return true;
    }

    private bool TryResolveExactStagePoint(out OcctPoint3d point)
    {
        point = default;
        if (_start is not { } start)
            return false;

        if (_end is null)
        {
            return CadExactInputGeometry.TryResolveLengthAnglePoint(
                Context.Workspace,
                start,
                out point);
        }

        return CadExactInputGeometry.TryResolveLengthAnglePoint(
            Context.Workspace,
            Context.WorkPlane.Origin,
            out point);
    }

    private void UpdatePreview(
        OcctPoint3d start,
        OcctPoint3d end,
        OcctPoint3d offsetPoint)
    {
        if (!TryCreateEntity(start, end, offsetPoint, out var entity))
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }

        _preview = entity;
        ShowPreview(_preview);
    }

    private bool TryCreateEntity(
        OcctPoint3d start,
        OcctPoint3d end,
        OcctPoint3d offsetPoint,
        out CadLengthDimensionEntity entity)
    {
        entity = null!;
        var frame = Context.WorkPlane.EffectivePlane;
        var segment = end - start;
        if (!segment.TryNormalize(out var axis))
            return false;

        var offsetDirection = frame.Normal.Cross(axis);
        if (!offsetDirection.TryNormalize(out var normalizedOffset))
            return false;

        var middle = start + segment * 0.5;
        var offset = (offsetPoint - middle).Dot(normalizedOffset);
        if (!double.IsFinite(offset))
            return false;

        try
        {
            entity = new CadLengthDimensionEntity(
                start,
                end,
                frame.Normal,
                offset,
                _textHeight,
                _arrowSize,
                _fontName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void RestorePrompt()
    {
        if (_start is null)
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.LengthDimension.First",
                "Dimension: specify first extension point [Esc cancel]");
            return;
        }

        if (_end is null)
        {
            SetStageLocalized(
                1,
                "Cad.Prompt.LengthDimension.Second",
                "Dimension: specify second extension point [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return;
        }

        SetStageLocalized(
            2,
            "Cad.Prompt.LengthDimension.Position",
            "Dimension: specify dimension-line offset [Backspace undo, Esc cancel]",
            CadPrecisionInputKind.Length);
    }

    private static bool TryPositive(string value, out double number) =>
        CadValueTextConverter.TryParseFiniteDouble(value, out number) &&
        number > 1e-9;
}
