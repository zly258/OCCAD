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
                new CadOptionalDoubleToolParameterDescriptor("Width", "Width", _width, 1e-9, double.MaxValue),
                new CadOptionalDoubleToolParameterDescriptor("Height", "Height", _height, 1e-9, double.MaxValue),
                new CadDoubleToolParameterDescriptor("Angle", "Angle", _angleDegrees, -360000.0, 360000.0)
            ]);

    protected override bool CanStepBackCore => _first is not null;

    public override bool CanCommitCurrentStage =>
        _first is not null &&
        IsActive &&
        State == CadToolState.Drawing &&
        _width is > 1e-9 &&
        _height is > 1e-9
            ? true
            : base.CanCommitCurrentStage;

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

    protected internal override void OnWorkPlaneChanged()
    {
        _planeOrigin = Context.WorkPlane.Origin;
        _baseX = Context.WorkPlane.XAxis;
        _baseY = Context.WorkPlane.YAxis;
        UpdateAxes();
        base.OnWorkPlaneChanged();
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

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (_first is { } first &&
            _width is { } width && width > 1e-9 &&
            _height is { } height && height > 1e-9)
        {
            return AcceptPoint(
                first + _xAxis * width + _yAxis * height);
        }

        return CommitResolvedPoint(pointer, _first, AcceptPoint);
    }

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
                if (!CadValueTextConverter.TryParseFiniteDouble(value, out _angleDegrees)) return false;
                UpdateAxes();
                break;
            default:
                return false;
        }

        RefreshPreviewFromInput();
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
            RefreshPreviewFromInput();
            return true;
        }

        if (!TryGeometry(_first.Value, point, out var center, out var width, out var height))
            return false;

        try
        {
            var entity = new CadRectangleEntity(center, _xAxis, _yAxis, width, height);
            _first = null;
            _preview = null;
            CommitPreview(entity);
            return true;
        }
        catch (ArgumentException)
        {
            Context.Preview.Clear();
            _preview = null;
            return false;
        }
    }

    private void UpdatePreview(OcctPoint3d first, OcctPoint3d second)
    {
        if (!TryGeometry(first, second, out var center, out var width, out var height))
        {
            Context.Preview.Clear();
            _preview = null;
            return;
        }

        try
        {
            _preview = new CadRectangleEntity(center, _xAxis, _yAxis, width, height);
            ShowPreview(_preview);
        }
        catch (ArgumentException)
        {
            Context.Preview.Clear();
            _preview = null;
        }
    }

    private bool TryGeometry(
        OcctPoint3d first,
        OcctPoint3d second,
        out OcctPoint3d center,
        out double width,
        out double height)
    {
        center = first;
        width = 0.0;
        height = 0.0;
        if (!first.IsFinite || !second.IsFinite ||
            !_xAxis.TryNormalize(out var xAxis) ||
            !_yAxis.TryNormalize(out var yAxis) ||
            !xAxis.Cross(yAxis).TryNormalize(out _))
            return false;

        var delta = CadTransformMath.Between(first, second);
        var dx = CadTransformMath.Dot(delta, xAxis);
        var dy = CadTransformMath.Dot(delta, yAxis);
        if (!double.IsFinite(dx) || !double.IsFinite(dy))
            return false;

        var signX = dx < 0.0 ? -1.0 : 1.0;
        var signY = dy < 0.0 ? -1.0 : 1.0;
        width = _width ?? Math.Abs(dx);
        height = _height ?? Math.Abs(dy);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 1e-9 || height <= 1e-9)
            return false;

        center = first + xAxis * (signX * width * 0.5) + yAxis * (signY * height * 0.5);
        return center.IsFinite;
    }

    private void RefreshPreviewFromInput()
    {
        if (_first is not { } first)
            return;

        // Fully specified dimensions are deterministic and must not depend on
        // a stale pointer quadrant. Pointer direction is only a fallback while
        // one or both dimensions are still unspecified.
        if (_width is { } width && width > 1e-9 &&
            _height is { } height && height > 1e-9)
        {
            UpdatePreview(
                first,
                first + _xAxis * width + _yAxis * height);
            return;
        }

        if (Context.Workspace.LastPointerPosition is { } pointer)
        {
            var point = Context.ResolvePoint(pointer.X, pointer.Y, first).Point;
            UpdatePreview(first, point);
        }
    }

    private void UpdateAxes()
    {
        if (!_baseX.TryNormalize(out var baseX) ||
            !_baseY.TryNormalize(out var baseY) ||
            !baseX.Cross(baseY).TryNormalize(out _))
        {
            var frame = Context.WorkPlane.EffectivePlane;
            baseX = frame.XAxis;
            baseY = frame.YAxis;
            _baseX = baseX;
            _baseY = baseY;
        }

        var radians = _angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var x = new OcctVector3d(
            baseX.X * cos + baseY.X * sin,
            baseX.Y * cos + baseY.Y * sin,
            baseX.Z * cos + baseY.Z * sin);
        var y = new OcctVector3d(
            -baseX.X * sin + baseY.X * cos,
            -baseX.Y * sin + baseY.Y * cos,
            -baseX.Z * sin + baseY.Z * cos);

        if (!x.TryNormalize(out _xAxis) || !y.TryNormalize(out _yAxis))
        {
            _xAxis = baseX;
            _yAxis = baseY;
        }
    }

    private void RestoreWorkPlane()
    {
        Context.WorkPlane.SetToolPlaneFixed(false);
        Context.WorkPlane.SetToolPlane(_planeOrigin, _baseX, _baseY);
    }

    private void RestorePrompt()
    {
        if (_first is null)
        {
            SetStageLocalized(0, "Cad.Prompt.Rectangle.First", "Rectangle: specify first corner [Esc cancel]");
            return;
        }

        SetStageLocalized(1, "Cad.Prompt.Rectangle.Opposite", "Rectangle: specify opposite corner [Backspace undo, Esc cancel]");
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

        if (!CadValueTextConverter.TryParseFiniteDouble(text, out var parsed) || parsed <= 0.0)
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }
}
