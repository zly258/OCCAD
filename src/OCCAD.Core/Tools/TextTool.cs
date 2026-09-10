using OcctNet;

namespace OCCAD;

public sealed class TextTool : CadDrawingTool, ICadPointInputTool
{
    private string _text = "Text";
    private double _height = 5.0;
    private double _angleDegrees;
    private string _fontName = "Arial";
    private CadTextEntity? _preview;

    public override string Id => "text";
    public override string DisplayName => "Text";

    public override CadToolParameterSchema ParameterSchema =>
        new(
            "Text",
            [
                new CadStringToolParameterDescriptor("Text", "Text", _text),
                new CadDoubleToolParameterDescriptor(
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
                    360000.0),
                new CadStringToolParameterDescriptor("Font", "Font", _fontName)
            ]);

    protected override void OnActivated()
    {
        _preview = null;
        SetStageLocalized(
            0,
            "Cad.Prompt.Text.Position",
            "Text: specify insertion point [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            UpdatePreview(Context.ResolvePoint(input.X, input.Y).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(Context.ResolvePoint(input.X, input.Y).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, null, AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            _text = value;
        }
        else if (id.Equals("Height", StringComparison.OrdinalIgnoreCase))
        {
            if (!CadValueTextConverter.TryParseFiniteDouble(value, out var height) ||
                height <= 1e-9)
                return false;
            _height = height;
        }
        else if (id.Equals("Angle", StringComparison.OrdinalIgnoreCase))
        {
            if (!CadValueTextConverter.TryParseFiniteDouble(value, out _angleDegrees))
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

    protected override void OnCanceled()
    {
        _preview = null;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        var entity = CreateEntity(point);
        _preview = null;
        CommitPreview(entity);
        return true;
    }

    private void UpdatePreview(OcctPoint3d point)
    {
        if (!point.IsFinite)
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }

        try
        {
            _preview = CreateEntity(point);
            ShowPreview(_preview);
        }
        catch (ArgumentException)
        {
            _preview = null;
            Context.Preview.Clear();
        }
    }

    private CadTextEntity CreateEntity(OcctPoint3d point)
    {
        var frame = Context.WorkPlane.EffectivePlane;
        return new CadTextEntity(
            _text,
            point,
            frame.Normal,
            frame.XAxis,
            _height,
            _angleDegrees,
            _fontName);
    }
}
