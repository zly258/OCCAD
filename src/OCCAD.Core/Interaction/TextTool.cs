using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class TextTool : CadDrawingTool, ICadPointInputTool
{
    private string _text = "Text";
    private double _height = 5;
    private double _angleDegrees; private OcctVector3d _normal;
    private OcctVector3d _xAxis;
    private OcctPoint3d? _previewPoint;

    public override string Id => "text";
    public override string DisplayName => "Text";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Text",
            [
                new CadStringToolParameterDescriptor("Text", "Content", _text),
                new CadDoubleToolParameterDescriptor("Height", "Height", _height, 1e-9, 1_000_000),
                new CadDoubleToolParameterDescriptor("Angle", "Angle", _angleDegrees, -360_000, 360_000)
            ]);

    protected override void OnActivated()
    {
        _normal = Context.WorkPlane.Normal;
        _xAxis = Context.WorkPlane.XAxis;
        _previewPoint = null;
        SetStageLocalized(
            0,
            "Cad.Prompt.Text.Position",
            "Text: specify insertion point [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved)
        {
            UpdatePreview(Resolve(input));
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;
        return AcceptPoint(Resolve(input));
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, null, AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            _text = value;
        }
        else if (id.Equals("Height", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryDouble(value, out var number) || number <= 1e-9 || number > 1_000_000)
                return false;
            _height = number;
        }
        else if (id.Equals("Angle", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryDouble(value, out var number) || number < -360_000 || number > 360_000)
                return false;
            _angleDegrees = number;
        }

        else
        {
            return false;
        }

        if (_previewPoint is { } point) Show(point);
        NotifyUpdated();
        return true;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        CommitPreview(Create(point));
        return true;
    }

    private void UpdatePreview(OcctPoint3d point)
    {
        if (!point.IsFinite) return;
        _previewPoint = point;
        Show(point);
    }

    private void Show(OcctPoint3d point) => ShowPreview(Create(point));

    private CadTextEntity Create(OcctPoint3d point) =>
        new(_text, point, _normal, _xAxis, _height, _angleDegrees);

    private static bool TryDouble(string value, out double number) =>
        (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number) ||
         double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) &&
        double.IsFinite(number);
}


