using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class RectangleTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d? _first;
    private OcctPoint3d _planeOrigin;
    private OcctVector3d _baseX;
    private OcctVector3d _baseY;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private double? _width;
    private double? _height;
    private double _angleDegrees;
    private CadRectangleEntity? _preview;

    public override string Id => "rectangle";
    public override string DisplayName => "Rectangle";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Rectangle",
            [
                new CadOptionalDoubleToolParameterDescriptor(
                    "Width",
                    "Width",
                    _width,
                    1e-9,
                    double.MaxValue),
                new CadOptionalDoubleToolParameterDescriptor(
                    "Height",
                    "Height",
                    _height,
                    1e-9,
                    double.MaxValue),
                new CadDoubleToolParameterDescriptor(
                    "Angle",
                    "Angle",
                    _angleDegrees,
                    -360000.0,
                    360000.0)
            ]);

    protected override bool CanStepBackCore => _first is not null;

    protected override void OnActivated()
    {
        _first = null;
        _preview = null;
        _width = null;
        _height = null;
        _angleDegrees = 0.0;
        _planeOrigin = Context.WorkPlane.Origin;
        _baseX = Context.WorkPlane.XAxis;
        _baseY = Context.WorkPlane.YAxis;
        UpdateAxes();
        RestorePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;

        if (input.Kind == OcctPointerInputKind.Moved && _first is { } first)
        {
            UpdatePreview(first, Context.ResolvePoint(input.X, input.Y, first).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(Context.ResolvePoint(input.X, input.Y, _first).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, _first, AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        switch (id.Trim().ToUpperInvariant())
        {
            case "WIDTH":
                if (!TryOptionalPositive(value, out _width)) return false;
                break;
            case "HEIGHT":
                if (!TryOptionalPositive(value, out _height)) return false;
                break;
            case "ANGLE":
                if (!TryFinite(value, out _angleDegrees)) return false;
                UpdateAxes();
                break;
            default:
                return false;
        }

        RefreshPreviewFromLastPointer();
        NotifyUpdated();
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_first is null) return false;
        _first = null;
        _preview = null;
        Context.Preview.Clear();
        RestoreWorkPlane();
        RestorePrompt();
        return true;
    }

    protected override void OnCanceled() => Reset();

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (_first is null)
        {
            _first = point;
            Context.WorkPlane.SetOrigin(point);
            RestorePrompt();
            return true;
        }

        if (!TryGeometry(_first.Value, point, out var center, out var width, out var height))
            return true;

        _preview = new CadRectangleEntity(center, _xAxis, _yAxis, width, height);
        Context.Preview.Show(_preview);
        Context.AddEntity(_preview.Duplicate());
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void UpdatePreview(OcctPoint3d first, OcctPoint3d second)
    {
        if (!TryGeometry(first, second, out var center, out var width, out var height))
        {
            Context.Preview.Clear();
            _preview = null;
            return;
        }

        _preview = new CadRectangleEntity(center, _xAxis, _yAxis, width, height);
        Context.Preview.Show(_preview);
    }

    private bool TryGeometry(
        OcctPoint3d first,
        OcctPoint3d second,
        out OcctPoint3d center,
        out double width,
        out double height)
    {
        var delta = CadTransformMath.Between(first, second);
        var dx = CadTransformMath.Dot(delta, _xAxis);
        var dy = CadTransformMath.Dot(delta, _yAxis);
        var signX = dx < 0.0 ? -1.0 : 1.0;
        var signY = dy < 0.0 ? -1.0 : 1.0;

        width = _width ?? Math.Abs(dx);
        height = _height ?? Math.Abs(dy);
        center = first
            + _xAxis * (signX * width * 0.5)
            + _yAxis * (signY * height * 0.5);

        return width > 1e-9 && height > 1e-9;
    }

    private void RefreshPreviewFromLastPointer()
    {
        if (_first is not { } first ||
            Context.Workspace.LastPointerPosition is not { } pointer)
            return;

        var point = Context.ResolvePoint(pointer.X, pointer.Y, first).Point;
        UpdatePreview(first, point);
    }

    private void UpdateAxes()
    {
        var radians = _angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        _xAxis = new OcctVector3d(
            _baseX.X * cos + _baseY.X * sin,
            _baseX.Y * cos + _baseY.Y * sin,
            _baseX.Z * cos + _baseY.Z * sin).Normalized();
        _yAxis = new OcctVector3d(
            -_baseX.X * sin + _baseY.X * cos,
            -_baseX.Y * sin + _baseY.Y * cos,
            -_baseX.Z * sin + _baseY.Z * cos).Normalized();
    }

    private void RestoreWorkPlane()
    {
        Context.WorkPlane.SetPlaneLocked(false);
        Context.WorkPlane.SetCustom(_planeOrigin, _baseX, _baseY);
    }

    private void RestorePrompt()
    {
        if (_first is null)
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.Rectangle.First",
                "Rectangle: specify first corner [Esc cancel]");
            return;
        }

        SetStageLocalized(
            1,
            "Cad.Prompt.Rectangle.Opposite",
            "Rectangle: specify opposite corner [Backspace undo, Esc cancel]");
    }

    private void Reset()
    {
        _first = null;
        _preview = null;
    }

    private static bool TryOptionalPositive(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!TryFinite(text, out var parsed) || parsed <= 0.0)
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryFinite(string text, out double value)
    {
        var parsed = double.TryParse(
                         text,
                         NumberStyles.Float,
                         CultureInfo.CurrentCulture,
                         out value) ||
                     double.TryParse(
                         text,
                         NumberStyles.Float,
                         CultureInfo.InvariantCulture,
                         out value);
        return parsed && double.IsFinite(value);
    }
}
