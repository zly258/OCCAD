using OcctNet;

namespace OCCAD;

public sealed class HelixTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d _origin;
    private OcctVector3d _axis;
    private OcctVector3d _xAxis;
    private double _radius;
    private double _pitch = 1.0;
    private double _turns = 3.0;
    private WorkPlaneFrame _initial;

    public override string Id => "helix";
    public override string DisplayName => "Helix";
    protected override bool CanStepBackCore => Stage > 0;
    protected override bool CanFinishCore => Stage == 2;

    public override CadToolPanelDescriptor? ParameterPanel =>
        Stage != 2
            ? null
            : new(
                "Helix",
                [
                    new CadDoubleToolParameterDescriptor("Pitch", "Pitch", _pitch, -1000000, 1000000),
                    new CadDoubleToolParameterDescriptor("Turns", "Turns", _turns, 0.01, 10000)
                ]);

    protected override void OnActivated()
    {
        _initial = CaptureWorkPlaneFrame();
        SetStageLocalized(0, "Cad.Prompt.Helix.Center", "Helix: specify axis origin [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && Stage == 1)
        {
            Update(Context.ResolvePoint(input.X, input.Y, _origin).Point);
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;
        if (Stage < 2)
            return Accept(Context.ResolvePoint(input.X, input.Y, Stage == 1 ? _origin : null).Point);
        return true;
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, Stage == 1 ? _origin : null, Accept);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && Accept(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (Stage != 2 || !CadValueTextConverter.TryParseFiniteDouble(value, out var number))
            return false;

        if (id.Equals("Pitch", StringComparison.OrdinalIgnoreCase))
        {
            if (Math.Abs(number) <= 1e-9) return false;
            _pitch = number;
        }
        else if (id.Equals("Turns", StringComparison.OrdinalIgnoreCase))
        {
            if (number <= 0.0 || number > 10000.0) return false;
            _turns = number;
        }
        else
        {
            return false;
        }

        Show();
        NotifyUpdated();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinishCore) return false;
        CommitPreview(new CadHelixEntity(_origin, _axis, _xAxis, _radius, _pitch, _turns));
        return true;
    }

    protected override bool OnStepBack()
    {
        Context.Preview.Clear();
        if (Stage == 2)
        {
            SetStageLocalized(1, "Cad.Prompt.Helix.Radius", "Helix: specify radius and start direction [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
            return true;
        }
        RestoreWorkPlaneFrame(_initial);
        SetStageLocalized(0, "Cad.Prompt.Helix.Center", "Helix: specify axis origin [Esc cancel]");
        return true;
    }

    private bool Accept(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (Stage == 0)
        {
            _origin = point;
            _axis = Context.WorkPlane.Normal;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(1, "Cad.Prompt.Helix.Radius", "Helix: specify radius and start direction [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
            return true;
        }

        var direction = point - _origin;
        direction -= _axis * direction.Dot(_axis);
        if (!direction.TryNormalize(out _xAxis)) return false;
        _radius = direction.Length;
        _pitch = _radius;
        SetStageLocalized(2, "Cad.Prompt.Helix.Parameters", "Helix: set pitch and turns [Enter accept, Esc cancel]");
        Show();
        return true;
    }

    private void Update(OcctPoint3d point)
    {
        var direction = point - _origin;
        direction -= _axis * direction.Dot(_axis);
        if (!direction.TryNormalize(out var xAxis))
        {
            Context.Preview.Clear();
            return;
        }

        ShowPreview(new CadHelixEntity(_origin, _axis, xAxis, direction.Length, direction.Length, _turns));
    }

    private void Show() =>
        ShowPreview(new CadHelixEntity(_origin, _axis, _xAxis, _radius, _pitch, _turns));
}
