using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class LengthDimensionTool : CadDrawingTool, ICadPointInputTool
{
    private readonly List<OcctPoint3d> _points = [];
    private OcctPoint3d _planeOrigin;
    private OcctVector3d _planeX;
    private OcctVector3d _planeY;
    private OcctVector3d _normal;
    private double _textHeight = 4;
    private double _arrowSize = 2.5;
    private CadLengthDimensionEntity? _preview;

    public override string Id => "lengthdimension";
    public override string DisplayName => "Length Dimension";
    protected override bool CanStepBackCore => _points.Count > 0;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Length Dimension",
            [
                new CadDoubleToolParameterDescriptor("TextHeight", "Text Height", _textHeight, 1e-9, 1_000_000),
                new CadDoubleToolParameterDescriptor("ArrowSize", "Arrow Size", _arrowSize, 1e-9, 1_000_000)
            ]);

    protected override void OnActivated()
    {
        _points.Clear();
        _preview = null;
        _planeOrigin = Context.WorkPlane.Origin;
        _planeX = Context.WorkPlane.XAxis;
        _planeY = Context.WorkPlane.YAxis;
        _normal = Context.WorkPlane.Normal;
        UpdatePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && _points.Count > 0)
        {
            UpdatePreview(Resolve(input, _points[^1]));
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;
        return AcceptPoint(Resolve(input, _points.Count == 0 ? null : _points[^1]));
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, _points.Count == 0 ? null : _points[^1], AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (!TryPositive(value, out var number)) return false;
        if (id.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))
            _textHeight = number;
        else if (id.Equals("ArrowSize", StringComparison.OrdinalIgnoreCase))
            _arrowSize = number;
        else
            return false;
        if (_preview is not null) UpdatePreview(_preview.GetGripPoints()[2].Position);
        NotifyUpdated();
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_points.Count == 0) return false;
        _points.RemoveAt(_points.Count - 1);
        _preview = null;
        Context.Preview.Clear();
        Context.WorkPlane.SetToolPlaneFixed(false);
        if (_points.Count > 0)
        {
            Context.WorkPlane.SetOrigin(_points[0]);
            Context.WorkPlane.SetToolPlaneFixed(true);
        }
        UpdatePrompt();
        return true;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        point = Project(point);
        if (_points.Count == 0)
        {
            _points.Add(point);
            Context.WorkPlane.SetOrigin(point);
            Context.WorkPlane.SetToolPlaneFixed(true);
            UpdatePrompt();
            return true;
        }
        if (_points.Count == 1)
        {
            if ((point - _points[0]).Length <= 1e-9) return true;
            _points.Add(point);
            UpdatePrompt();
            return true;
        }

        var entity = Create(point);
        if (entity is null) return true;
        Context.AddEntity(entity);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void UpdatePreview(OcctPoint3d point)
    {
        point = Project(point);
        if (_points.Count == 1)
        {
            if ((point - _points[0]).Length <= 1e-9) return;
            ShowPreview(new CadLineEntity(_points[0], point));
            return;
        }
        if (_points.Count != 2) return;
        _preview = Create(point);
        if (_preview is not null) ShowPreview(_preview);
    }

    private CadLengthDimensionEntity? Create(OcctPoint3d placement)
    {
        var axis = (_points[1] - _points[0]).Normalized();
        var perpendicular = _normal.Cross(axis).Normalized();
        var middle = _points[0] + (_points[1] - _points[0]) * 0.5;
        var offset = (placement - middle).Dot(perpendicular);
        if (Math.Abs(offset) <= 1e-9) return null;
        return new CadLengthDimensionEntity(
            _points[0], _points[1], _normal, offset, _textHeight, _arrowSize);
    }

    private OcctPoint3d Project(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_planeOrigin, point, _planeX, _planeY);

    private void UpdatePrompt()
    {
        if (_points.Count == 0)
            SetStageLocalized(0, "Cad.Prompt.LengthDimension.First", "Length dimension: specify first extension origin [Esc cancel]");
        else if (_points.Count == 1)
            SetStageLocalized(1, "Cad.Prompt.LengthDimension.Second", "Length dimension: specify second extension origin [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
        else
            SetStageLocalized(2, "Cad.Prompt.LengthDimension.Position", "Length dimension: specify dimension line position [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
    }

    private static bool TryPositive(string text, out double value) =>
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
         double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
        double.IsFinite(value) && value > 1e-9 && value <= 1_000_000;
}

