using OcctNet;

namespace OCCAD;

public sealed class CircularDimensionTool : CadDrawingTool, ICadPointInputTool
{
    private const string RadiusKind = "Radius";
    private const string DiameterKind = "Diameter";

    private OcctPoint3d? _center;
    private double? _radius;
    private string _kind = RadiusKind;
    private double _textHeight = 4.0;
    private double _arrowSize = 2.5;
    private string _fontName = "Arial";
    private WorkPlaneFrame _initialPlane;

    public override string Id => "circulardimension";
    public override string DisplayName =>
        IsDiameter ? "Diameter Dimension" : "Radius Dimension";
    public override string PrecisionLengthLabel => Stage switch
    {
        1 => "Radius",
        2 => "Leader Distance",
        _ => base.PrecisionLengthLabel
    };
    public override string PrecisionAngleLabel => Stage switch
    {
        1 => "Direction",
        2 => "Leader Angle",
        _ => base.PrecisionAngleLabel
    };

    public override CadToolPanelDescriptor ParameterPanel
    {
        get
        {
            var parameters = new List<CadToolParameterDescriptor>();
            if (Stage == 0)
            {
                parameters.Add(
                    new CadChoiceToolParameterDescriptor(
                        "Kind",
                        "Kind",
                        _kind,
                        [RadiusKind, DiameterKind]));
            }

            parameters.Add(
                new CadDoubleToolParameterDescriptor(
                    "TextHeight",
                    "Text Height",
                    _textHeight,
                    1e-9,
                    double.MaxValue));
            parameters.Add(
                new CadDoubleToolParameterDescriptor(
                    "ArrowSize",
                    "Arrow Size",
                    _arrowSize,
                    1e-9,
                    double.MaxValue));
            parameters.Add(
                new CadStringToolParameterDescriptor(
                    "Font",
                    "Font",
                    _fontName));

            return new CadToolPanelDescriptor(DisplayName, parameters);
        }
    }

    protected override bool CanStepBackCore => _center is not null;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        TryResolveExactStagePoint(out _)
            ? true
            : base.CanCommitCurrentStage;

    private bool IsDiameter =>
        _kind.Equals(DiameterKind, StringComparison.OrdinalIgnoreCase);

    protected override void OnActivated()
    {
        _center = null;
        _radius = null;
        _initialPlane = CaptureWorkPlaneFrame();
        RestorePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (input.Kind == OcctPointerInputKind.Moved &&
            _center is { } center)
        {
            var point = Context.ResolvePoint(
                input.X,
                input.Y,
                center).Point;
            UpdatePreview(center, point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                _center).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactStagePoint(out var exactPoint))
            return AcceptPoint(exactPoint);

        return CommitResolvedPoint(pointer, _center, AcceptPoint);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        AcceptPoint(point);

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (_center is { } center &&
            TryResolveExactStagePoint(out var exactPoint))
        {
            UpdatePreview(center, exactPoint);
            return true;
        }

        return base.OnPrecisionInputApplied(input);
    }

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Kind", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage != 0)
                return false;

            var normalized = value.Trim();
            if (!normalized.Equals(RadiusKind, StringComparison.OrdinalIgnoreCase) &&
                !normalized.Equals(DiameterKind, StringComparison.OrdinalIgnoreCase))
                return false;

            _kind = normalized.Equals(DiameterKind, StringComparison.OrdinalIgnoreCase)
                ? DiameterKind
                : RadiusKind;
            RestorePrompt();
        }
        else if (id.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))
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
        if (_radius is not null)
        {
            _radius = null;
            Context.Preview.Clear();
            RestorePrompt();
            return true;
        }

        if (_center is null)
            return false;

        _center = null;
        Context.Preview.Clear();
        RestoreWorkPlaneFrame(_initialPlane);
        RestorePrompt();
        return true;
    }

    protected override void OnCanceled()
    {
        _center = null;
        _radius = null;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        if (_center is null)
        {
            _center = point;
            RestoreWorkPlaneFrame(_initialPlane, point);
            RestorePrompt();
            return true;
        }

        if (_radius is null)
        {
            if (!TryPlanarDirection(
                    _center.Value,
                    point,
                    out _,
                    out var radius))
                return false;

            _radius = radius;
            Context.Preview.Clear();
            RestorePrompt();
            return true;
        }

        if (!TryCreateEntity(
                _center.Value,
                _radius.Value,
                point,
                out var entity))
            return false;

        CommitPreview(entity);
        return true;
    }

    private bool TryResolveExactStagePoint(out OcctPoint3d point)
    {
        point = default;
        return _center is { } center &&
               CadExactInputGeometry.TryResolveLengthAnglePoint(
                   Context.Workspace,
                   center,
                   out point);
    }

    private void UpdatePreview(OcctPoint3d center, OcctPoint3d point)
    {
        if (_radius is null)
        {
            if (!TryPlanarDirection(center, point, out _, out var radius))
            {
                Context.Preview.Clear();
                return;
            }

            ShowPreview(
                new CadCircleEntity(
                    center,
                    Context.WorkPlane.Normal,
                    radius));
            return;
        }

        if (!TryCreateEntity(center, _radius.Value, point, out var entity))
        {
            Context.Preview.Clear();
            return;
        }

        ShowPreview(entity);
    }

    private bool TryCreateEntity(
        OcctPoint3d center,
        double radius,
        OcctPoint3d leaderPoint,
        out CadCircularDimensionEntity entity)
    {
        entity = null!;
        if (!TryPlanarDirection(
                center,
                leaderPoint,
                out var direction,
                out var leaderDistance))
            return false;

        var offset = leaderDistance - radius;
        if (!double.IsFinite(offset))
            return false;

        try
        {
            entity = new CadCircularDimensionEntity(
                IsDiameter
                    ? CadCircularDimensionKind.Diameter
                    : CadCircularDimensionKind.Radius,
                center,
                Context.WorkPlane.Normal,
                direction,
                radius,
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

    private bool TryPlanarDirection(
        OcctPoint3d origin,
        OcctPoint3d point,
        out OcctVector3d direction,
        out double distance)
    {
        var normal = Context.WorkPlane.Normal;
        var delta = point - origin;
        var axial = delta.Dot(normal);
        var planar = delta - normal * axial;
        distance = Math.Sqrt(planar.LengthSquared);
        if (!double.IsFinite(distance) ||
            distance <= 1e-9 ||
            !planar.TryNormalize(out direction))
        {
            direction = default;
            distance = 0.0;
            return false;
        }

        return true;
    }

    private void RestorePrompt()
    {
        var label = DisplayName;
        if (_center is null)
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.CircularDimension.Center",
                $"{label}: specify center [Esc cancel]");
            return;
        }

        if (_radius is null)
        {
            SetStageLocalized(
                1,
                "Cad.Prompt.CircularDimension.Radius",
                $"{label}: specify point on circle [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return;
        }

        SetStageLocalized(
            2,
            "Cad.Prompt.CircularDimension.Position",
            $"{label}: specify annotation position [Backspace undo, Esc cancel]",
            CadPrecisionInputKind.LengthAndAngle);
    }

    private static bool TryPositive(string value, out double number) =>
        CadValueTextConverter.TryParseFiniteDouble(value, out number) &&
        number > 1e-9;
}
