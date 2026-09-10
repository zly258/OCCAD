using OcctNet;

namespace OCCAD;

public sealed class MirrorTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _first;
    private OcctVector3d _workNormal;
    private bool _keepSource = true;
    public override string Id => "mirror";
    public override string DisplayName => "Mirror";
    public override OcctPoint3d? PrecisionReferencePoint => _first;
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing;
    protected override bool CanStepBackCore => _first is not null;
    public override CadToolPanelDescriptor? ParameterPanel => State != CadToolState.Drawing ? null :
        new("Mirror", [new CadBooleanToolParameterDescriptor("KeepSource", "Keep source", _keepSource)]);

    protected override void OnTransformStarted()
    {
        _workNormal = Context.WorkPlane.Normal;
        SetStageLocalized(0, "Cad.Prompt.mirror.First", "Mirror: specify first point of mirror line [Esc cancel]");
    }
    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (State != CadToolState.Drawing) return false;
        if (input.Kind == OcctPointerInputKind.Moved && _first is not null)
        {
            var point = Context.ResolvePoint(input.X, input.Y, _first).Point;
            if (TryNormal(point, out var normal))
                Context.Preview.Show(Entities.Select(entity => entity.MirroredCopy(_first.Value, normal)));
            else Context.Preview.Clear();
            return true;
        }
        return input.Kind == OcctPointerInputKind.Pressed && input.Button == OcctPointerButton.Left &&
            TryAcceptPoint(Context.ResolvePoint(input.X, input.Y, _first).Point);
    }
    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive || State != CadToolState.Drawing || !point.IsFinite) return false;
        if (_first is null)
        {
            _first = point;
            Context.WorkPlane.SetOrigin(point);
            Context.WorkPlane.SetToolPlaneFixed(true);
            SetStageLocalized(1, "Cad.Prompt.mirror.Second", "Mirror: specify second point of mirror line [Backspace undo, Esc cancel]");
            return true;
        }
        if (!TryNormal(point, out var normal)) return false;
        Context.Workspace.MirrorEntities(Entities, _first.Value, normal, _keepSource);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }
    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y, _first).Point);
    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals("KeepSource", StringComparison.OrdinalIgnoreCase) || !bool.TryParse(value, out var keep)) return false;
        _keepSource = keep;
        NotifyUpdated();
        return true;
    }
    protected override bool OnStepBack()
    {
        _first = null;
        Context.Preview.Clear();
        Context.WorkPlane.SetToolPlaneFixed(false);
        SetStageLocalized(0, "Cad.Prompt.mirror.First", "Mirror: specify first point of mirror line [Esc cancel]");
        return true;
    }
    protected override void ResetTransformState() { _first = null; _keepSource = true; }
    private bool TryNormal(OcctPoint3d point, out OcctVector3d normal) =>
        (point - _first!.Value).Cross(_workNormal).TryNormalize(out normal);
}
