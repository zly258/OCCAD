using OcctNet;

namespace OCCAD;

public readonly record struct CadDistanceMeasurement(OcctPoint3d Start, OcctPoint3d End)
{
    public double Distance => Start.DistanceTo(End);
    public double DeltaX => End.X - Start.X;
    public double DeltaY => End.Y - Start.Y;
    public double DeltaZ => End.Z - Start.Z;
}

public sealed class MeasureDistanceTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d? _first;
    private OcctPoint3d _initialOrigin;
    public override string Id => "distance";
    public override string DisplayName => "Distance";
    public CadDistanceMeasurement? Measurement { get; private set; }
    public override OcctPoint3d? PrecisionReferencePoint => _first;
    protected override bool CanStepBackCore => _first is not null;
    protected override bool CanFinishCore => Stage == 2;

    protected override void OnActivated()
    {
        _initialOrigin =
            Context.WorkPlane.Origin;
        Reset();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && Stage == 1)
        {
            // Measurement reports actual coordinates, not drawing constraints.
            UpdateMeasurement(Context.ResolvePoint(input.X, input.Y).Point, false);
            return true;
        }
        return input.Kind == OcctPointerInputKind.Pressed && input.Button == OcctPointerButton.Left &&
            TryAcceptPoint(Context.ResolvePoint(input.X, input.Y).Point);
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive || State != CadToolState.Drawing || !point.IsFinite) return false;
        if (_first is null || Stage == 2)
        {
            _first = point;
            Measurement = null;
            Context.Preview.Clear();
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(1, "Cad.Prompt.Distance.Second", "Distance: specify second point [Backspace undo, Esc cancel]");
            return true;
        }
        SetStageLocalized(
            2,
            "Cad.Prompt.Distance.Completed",
            "Distance measured. Click to measure again [Enter finish, Backspace undo, Esc cancel]");
        UpdateMeasurement(point, true);
        return true;
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y).Point);

    protected override bool OnStepBack()
    {
        if (Stage == 2)
        {
            Measurement = null;
            Context.Preview.Clear();
            SetStageLocalized(1, "Cad.Prompt.Distance.Second", "Distance: specify second point [Backspace undo, Esc cancel]");
        }
        else Reset();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinish) return false;
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void UpdateMeasurement(OcctPoint3d point, bool accepted)
    {
        var value = new CadDistanceMeasurement(_first!.Value, point);
        Measurement = value;
        if (value.Distance > 1e-9) ShowPreview(new CadLineEntity(value.Start, value.End));
        else Context.Preview.Clear();
        SetPromptLocalized(
            accepted ? "Cad.Prompt.Distance.Result" : "Cad.Prompt.Distance.Preview",
            accepted
                ? "Distance {0:0.000}; dX {1:0.000}; dY {2:0.000}; dZ {3:0.000}. Click to measure again [Enter finish, Backspace undo, Esc cancel]"
                : "Distance {0:0.000}; dX {1:0.000}; dY {2:0.000}; dZ {3:0.000}. Specify second point [Backspace undo, Esc cancel]",
            CadPrecisionInputKind.None, value.Distance, value.DeltaX, value.DeltaY, value.DeltaZ);
    }

    private void Reset()
    {
        _first = null;
        Measurement = null;
        Context.Preview.Clear();
        Context.WorkPlane.SetOrigin(
            _initialOrigin);
        SetStageLocalized(0, "Cad.Prompt.Distance.First", "Distance: specify first point [Esc cancel]");
    }
}
