using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class RegularPolygonTool : CadDrawingTool, ICadPointInputTool
{
    private const int DefaultSides = 6;
    private const string InscribedMode = "Inscribed";
    private const string CircumscribedMode = "Circumscribed";

    private OcctPoint3d? _center;
    private OcctPoint3d? _currentPoint;
    private OcctVector3d _normal;
    private OcctVector3d _planeX;
    private OcctVector3d _planeY;
    private CadRegularPolygonEntity? _preview;
    private int _sides = DefaultSides;
    private bool _inscribed = true;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "regularpolygon";
    public override string DisplayName => "Regular Polygon";
    public override string PrecisionLengthLabel => "Radius";
    protected override bool CanStepBackCore =>
        _center is not null;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Regular Polygon",
            [
                new CadIntegerToolParameterDescriptor(
                    "sides",
                    "Sides",
                    _sides,
                    3,
                    360),
                new CadChoiceToolParameterDescriptor(
                    "mode",
                    "Mode",
                    _inscribed ? InscribedMode : CircumscribedMode,
                    [InscribedMode, CircumscribedMode])
            ]);

    protected override void OnActivated()
    {
        _center = null;
        _currentPoint = null;
        _preview = null;
        _sides = DefaultSides;
        _inscribed = true;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.RegularPolygon.Center",
            "Regular Polygon: specify center [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (input.Kind == OcctPointerInputKind.Moved &&
            _center is { } center)
        {
            _currentPoint =
                Context.ResolvePoint(input.X, input.Y, center).Point;
            UpdatePreview(center, _currentPoint.Value);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        var point = Context.ResolvePoint(
            input.X,
            input.Y,
            _center).Point;
        _currentPoint = point;
        return AcceptPoint(point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        var point = Context.ResolvePoint(
            pointer.X,
            pointer.Y,
            _center).Point;
        _currentPoint = point;
        return AcceptPoint(point);
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive ||
            State != CadToolState.Drawing ||
            !point.IsFinite)
            return false;
        _currentPoint = point;
        return AcceptPoint(point);
    }

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (string.Equals(
                id,
                "sides",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sides) ||
                sides < 3 ||
                sides > 360)
                return false;

            _sides = sides;
            RefreshParameterDrivenPreview();
            RefreshStagePrompt();
            return true;
        }

        if (string.Equals(
                id,
                "mode",
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(
                    value,
                    InscribedMode,
                    StringComparison.OrdinalIgnoreCase))
                _inscribed = true;
            else if (string.Equals(
                         value,
                         CircumscribedMode,
                         StringComparison.OrdinalIgnoreCase))
                _inscribed = false;
            else
                return false;

            RefreshParameterDrivenPreview();
            RefreshStagePrompt();
            return true;
        }

        return false;
    }

    protected override bool OnStepBack()
    {
        if (_center is null)
            return false;

        _center = null;
        _currentPoint = null;
        _preview = null;
        Context.Preview.Clear();
        RestoreWorkPlaneFrame(_initialPlane);
        SetStageLocalized(
            0,
            "Cad.Prompt.RegularPolygon.Center",
            "Regular Polygon: specify center [Esc cancel]");
        return true;
    }

    protected override void OnCanceled()
    {
        _center = null;
        _currentPoint = null;
        _preview = null;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (_center is null)
        {
            _center = point;
            _currentPoint = point;
            _normal = Context.WorkPlane.Normal;
            _planeX = Context.WorkPlane.XAxis;
            _planeY = Context.WorkPlane.YAxis;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(
                1,
                StagePromptKey(),
                StagePromptFormat(),
                CadPrecisionInputKind.LengthAndAngle,
                _sides);
            return true;
        }

        if (!TryGeometry(point, out var axis, out var radius))
            return false;

        var polygon = new CadRegularPolygonEntity(
            _center.Value,
            _normal,
            axis,
            radius,
            _sides);
        ShowPreview(polygon);
        Context.AddEntity(polygon.Duplicate());
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void UpdatePreview(
        OcctPoint3d center,
        OcctPoint3d point)
    {
        if (!TryGeometry(point, out var axis, out var radius))
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }

        _preview = new CadRegularPolygonEntity(
            center,
            _normal,
            axis,
            radius,
            _sides);
        ShowPreview(_preview);
    }

    private void RefreshParameterDrivenPreview()
    {
        if (_center is { } center &&
            _currentPoint is { } point)
            UpdatePreview(center, point);
    }

    private void RefreshStagePrompt()
    {
        if (Stage == 1)
        {
            SetPromptLocalized(
                StagePromptKey(),
                StagePromptFormat(),
                CadPrecisionInputKind.LengthAndAngle,
                _sides);
            return;
        }

        NotifyUpdated();
    }

    private string StagePromptKey() =>
        _inscribed
            ? "Cad.Prompt.RegularPolygon.RadiusInscribed"
            : "Cad.Prompt.RegularPolygon.RadiusCircumscribed";

    private string StagePromptFormat() =>
        _inscribed
            ? "Regular Polygon: specify radius [{0} sides, inscribed, Backspace undo, Esc cancel]"
            : "Regular Polygon: specify radius [{0} sides, circumscribed, Backspace undo, Esc cancel]";

    private bool TryGeometry(
        OcctPoint3d point,
        out OcctVector3d axis,
        out double radius)
    {
        if (_center is null)
        {
            axis = default;
            radius = 0.0;
            return false;
        }

        var delta = CadTransformMath.Between(_center.Value, point);
        var x = CadTransformMath.Dot(delta, _planeX);
        var y = CadTransformMath.Dot(delta, _planeY);
        var inputRadius = Math.Sqrt(x * x + y * y);
        if (inputRadius <= 1e-9)
        {
            axis = default;
            radius = 0.0;
            return false;
        }

        var inputDirection = new OcctVector3d(
            _planeX.X * x + _planeY.X * y,
            _planeX.Y * x + _planeY.Y * y,
            _planeX.Z * x + _planeY.Z * y).Normalized();

        if (_inscribed)
        {
            radius = inputRadius;
            axis = inputDirection;
            return true;
        }

        var halfSectorDegrees = 180.0 / _sides;
        radius = inputRadius / Math.Cos(Math.PI / _sides);
        axis = CadTransformMath.RotateVector(
            inputDirection,
            _normal,
            -halfSectorDegrees).Normalized();
        return true;
    }
}
