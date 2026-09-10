using OcctNet;

namespace OCCAD;

public sealed class MirrorTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _first;
    private OcctVector3d _workNormal;
    private bool _keepSource = true;
    private OcctPoint3d _initialOrigin;
    public override string Id => "mirror";
    public override string DisplayName => "Mirror";
    public override OcctPoint3d? PrecisionReferencePoint => _first;
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing;
    protected override bool CanStepBackCore => _first is not null;
    public override CadToolPanelDescriptor? ParameterPanel => State != CadToolState.Drawing ? null :
        new("Mirror", [new CadBooleanToolParameterDescriptor("KeepSource", "Keep source", _keepSource)]);

    protected override void OnTransformStarted()
    {
        _initialOrigin =
            Context.WorkPlane.Origin;
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
                ShowMirrorPreview(normal);
            else
                ClearReplacementPreview();
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
        if (!TryNormal(point, out var normal))
            return false;

        if (_keepSource)
        {
            Context.Workspace.MirrorEntities(
                Entities,
                _first.Value,
                normal,
                keepSource: true);
        }
        else
        {
            CommitReplacementPreview(
                () => Context.Workspace.MirrorEntities(
                    Entities,
                    _first.Value,
                    normal,
                    keepSource: false));
        }

        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }
    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y, _first).Point);
    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals(
                "KeepSource",
                StringComparison.OrdinalIgnoreCase) ||
            !bool.TryParse(value, out var keep))
            return false;

        _keepSource = keep;
        RefreshPreviewFromLastPointer();
        NotifyUpdated();
        return true;
    }
    protected override bool OnStepBack()
    {
        _first = null;
        Context.Preview.Clear();
        Context.WorkPlane.SetToolPlaneFixed(false);
        Context.WorkPlane.SetOrigin(
            _initialOrigin);
        SetStageLocalized(0, "Cad.Prompt.mirror.First", "Mirror: specify first point of mirror line [Esc cancel]");
        return true;
    }
    protected override void ResetTransformState()
    {
        _first = null;
        _keepSource = true;
        _initialOrigin = default;
    }
    private void RefreshPreviewFromLastPointer()
    {
        if (_first is null ||
            Context.Workspace.LastPointerPosition is not { } pointer)
        {
            ClearReplacementPreview();
            return;
        }

        var point =
            Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                _first).Point;
        if (TryNormal(point, out var normal))
            ShowMirrorPreview(normal);
        else
            ClearReplacementPreview();
    }

    private void ShowMirrorPreview(OcctVector3d normal)
    {
        var previews = Entities
            .Select(entity =>
                entity.MirroredCopy(
                    _first!.Value,
                    normal))
            .ToArray();

        if (_keepSource)
        {
            // A copy mirror previews the final state: source + new copies.
            ClearReplacementPreview();
            Context.Preview.Show(previews);
            return;
        }

        // A replace mirror mutates the original entities in place. Suppress
        // them while showing the mirrored result so preview matches commit.
        ShowReplacementPreview(
            Entities,
            previews);
    }

    private bool TryNormal(OcctPoint3d point, out OcctVector3d normal) =>
        (point - _first!.Value).Cross(_workNormal).TryNormalize(out normal);
}
