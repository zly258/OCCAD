using OcctNet;

namespace OCCAD;

public sealed class AngleDimensionTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d? _vertex;
    private OcctPoint3d? _firstRayPoint;
    private OcctPoint3d? _secondRayPoint;
    private double _textHeight = 4.0;
    private double _arrowSize = 2.5;
    private string _fontName = "Arial";
    private CadAngleDimensionEntity? _preview;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "angledimension";
    public override string DisplayName => "Angle Dimension";
    public override OcctPoint3d? PrecisionReferencePoint => _vertex;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Angle Dimension",
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

    protected override bool CanStepBackCore => _vertex is not null;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        TryResolveExactStagePoint(out _)
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnActivated()
    {
        _vertex = null;
        _firstRayPoint = null;
        _secondRayPoint = null;
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
            var point = Context.ResolvePoint(
                input.X,
                input.Y,
                PrecisionReferencePoint).Point;
            if (_vertex is { } vertex &&
                _firstRayPoint is { } first &&
                _secondRayPoint is { } second)
            {
                UpdatePreview(vertex, first, second, point);
            }
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                PrecisionReferencePoint).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
            return AcceptPoint(exactPoint);

        return CommitResolvedPoint(
            pointer,
            PrecisionReferencePoint,
            AcceptPoint);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
        {
            if (_vertex is { } vertex &&
                _firstRayPoint is { } first &&
                _secondRayPoint is { } second)
            {
                UpdatePreview(vertex, first, second, exactPoint);
            }
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
        if (_secondRayPoint is not null)
        {
            _secondRayPoint = null;
            ClearPreview();
            RestorePrompt();
            return true;
        }

        if (_firstRayPoint is not null)
        {
            _firstRayPoint = null;
            ClearPreview();
            RestorePrompt();
            return true;
        }

        if (_vertex is null)
            return false;

        _vertex = null;
        ClearPreview();
        RestoreWorkPlaneFrame(_initialPlane);
        RestorePrompt();
        return true;
    }

    protected override void OnCanceled()
    {
        _vertex = null;
        _firstRayPoint = null;
        _secondRayPoint = null;
        _preview = null;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        if (_vertex is null)
        {
            _vertex = point;
            RestoreWorkPlaneFrame(_initialPlane, point);
            RestorePrompt();
            return true;
        }

        if (_firstRayPoint is null)
        {
            if (_vertex.Value.DistanceTo(point) <= 1e-9)
                return false;
            _firstRayPoint = point;
            RestorePrompt();
            return true;
        }

        if (_secondRayPoint is null)
        {
            if (!TryDirections(
                    _vertex.Value,
                    _firstRayPoint.Value,
                    point,
                    out _,
                    out _))
                return false;

            _secondRayPoint = point;
            RestorePrompt();
            return true;
        }

        if (!TryCreateEntity(
                _vertex.Value,
                _firstRayPoint.Value,
                _secondRayPoint.Value,
                point,
                out var entity))
            return false;

        _preview = null;
        CommitPreview(entity);
        return true;
    }

    private bool TryResolveExactStagePoint(out OcctPoint3d point)
    {
        point = default;
        if (_vertex is not { } vertex)
            return false;

        if (_firstRayPoint is null || _secondRayPoint is null)
        {
            return CadExactInputGeometry.TryResolveLengthAnglePoint(
                Context.Workspace,
                vertex,
                out point);
        }

        var drafting = Context.Workspace.Drafting;
        if (!drafting.LengthLockEnabled ||
            !double.IsFinite(drafting.LockedLength) ||
            drafting.LockedLength <= 1e-9)
            return false;

        return CadExactInputGeometry.TryResolveAnglePoint(
            Context.Workspace,
            vertex,
            0.0,
            drafting.LockedLength,
            out point);
    }

    private void UpdatePreview(
        OcctPoint3d vertex,
        OcctPoint3d firstRayPoint,
        OcctPoint3d secondRayPoint,
        OcctPoint3d radiusPoint)
    {
        if (!TryCreateEntity(
                vertex,
                firstRayPoint,
                secondRayPoint,
                radiusPoint,
                out var entity))
        {
            ClearPreview();
            return;
        }

        _preview = entity;
        ShowPreview(_preview);
    }

    private bool TryCreateEntity(
        OcctPoint3d vertex,
        OcctPoint3d firstRayPoint,
        OcctPoint3d secondRayPoint,
        OcctPoint3d radiusPoint,
        out CadAngleDimensionEntity entity)
    {
        entity = null!;
        if (!TryDirections(
                vertex,
                firstRayPoint,
                secondRayPoint,
                out var firstDirection,
                out var secondDirection))
            return false;

        var frame = Context.WorkPlane.EffectivePlane;
        var delta = radiusPoint - vertex;
        var axial = delta.Dot(frame.Normal);
        var planar = delta - frame.Normal * axial;
        var radius = Math.Sqrt(planar.LengthSquared);
        if (!double.IsFinite(radius) || radius <= 1e-9)
            return false;

        try
        {
            entity = new CadAngleDimensionEntity(
                vertex,
                firstDirection,
                secondDirection,
                radius,
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

    private bool TryDirections(
        OcctPoint3d vertex,
        OcctPoint3d firstRayPoint,
        OcctPoint3d secondRayPoint,
        out OcctVector3d firstDirection,
        out OcctVector3d secondDirection)
    {
        var frame = Context.WorkPlane.EffectivePlane;
        firstDirection = ProjectDirection(vertex, firstRayPoint, frame.Normal);
        secondDirection = ProjectDirection(vertex, secondRayPoint, frame.Normal);
        if (!firstDirection.TryNormalize(out firstDirection) ||
            !secondDirection.TryNormalize(out secondDirection))
            return false;

        return Math.Abs(firstDirection.Dot(secondDirection)) < 1.0 - 1e-10;
    }

    private static OcctVector3d ProjectDirection(
        OcctPoint3d origin,
        OcctPoint3d point,
        OcctVector3d normal)
    {
        var delta = point - origin;
        var axial = delta.Dot(normal);
        return delta - normal * axial;
    }

    private void RestorePrompt()
    {
        if (_vertex is null)
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.AngleDimension.Vertex",
                "Angle Dimension: specify vertex [Esc cancel]");
            return;
        }

        if (_firstRayPoint is null)
        {
            SetStageLocalized(
                1,
                "Cad.Prompt.AngleDimension.FirstRay",
                "Angle Dimension: specify point on first ray [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return;
        }

        if (_secondRayPoint is null)
        {
            SetStageLocalized(
                2,
                "Cad.Prompt.AngleDimension.SecondRay",
                "Angle Dimension: specify point on second ray [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return;
        }

        SetStageLocalized(
            3,
            "Cad.Prompt.AngleDimension.Line",
            "Angle Dimension: specify dimension-arc radius [Backspace undo, Esc cancel]",
            CadPrecisionInputKind.Length);
    }

    private void ClearPreview()
    {
        _preview = null;
        Context.Preview.Clear();
    }

    private static bool TryPositive(string value, out double number) =>
        CadValueTextConverter.TryParseFiniteDouble(value, out number) &&
        number > 1e-9;
}
