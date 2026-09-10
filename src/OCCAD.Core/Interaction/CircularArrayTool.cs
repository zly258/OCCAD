using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class CircularArrayTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _center;
    private OcctPoint3d? _reference;
    private OcctVector3d _axis;
    private int _count = 6;
    private double _sweep = 360;
    private bool _rotate = true;
    public override string Id => "circlearray";
    public override string DisplayName => "Circular Array";
    public override OcctPoint3d? PrecisionReferencePoint => _center;
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing && (_center is null || (!_rotate && _reference is null));
    protected override bool CanStepBackCore => _center is not null;
    protected override bool CanFinishCore => State == CadToolState.Drawing && _center is not null &&
        (_rotate || _reference is not null) && CadCircularArray.IsValid(Entities.Count, _count, _sweep);
    public override CadToolPanelDescriptor? ParameterPanel => State != CadToolState.Drawing ? null : new("Circular Array",
        [new CadIntegerToolParameterDescriptor("Count", "Count (including source)", _count, 2, 1000),
         new CadDoubleToolParameterDescriptor("Sweep", "Sweep angle", _sweep, -360, 360),
         new CadBooleanToolParameterDescriptor("RotateItems", "Rotate copies", _rotate)]);

    protected override void OnTransformStarted() { _axis = Context.WorkPlane.Normal; UpdatePreview(); }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (State != CadToolState.Drawing || input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left) return false;
        if (_center is null || (!_rotate && _reference is null))
            TryAcceptPoint(Context.ResolvePoint(input.X, input.Y).Point);
        return true;
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive || State != CadToolState.Drawing || !point.IsFinite) return false;
        if (_center is null)
        {
            _center = point;
            Context.WorkPlane.SetOrigin(point);
            Context.WorkPlane.SetToolPlaneFixed(true);
        }
        else if (!_rotate && _reference is null)
        {
            var delta = point - _center.Value;
            if ((delta - _axis * delta.Dot(_axis)).Length <= 1e-9) return false;
            _reference = point;
        }
        else return false;
        UpdatePreview();
        return true;
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) => TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y).Point);
    protected override bool OnSetParameter(string id, string value)
    {
        if (State != CadToolState.Drawing) return false;
        switch (id.ToUpperInvariant())
        {
            case "COUNT":
                if (!int.TryParse(value, out var count) || count < 2 || count > 1000) return false;
                _count = count; break;
            case "SWEEP":
                if ((!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var sweep) &&
                    !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out sweep)) ||
                    !double.IsFinite(sweep) || Math.Abs(sweep) > 360) return false;
                _sweep = sweep; break;
            case "ROTATEITEMS":
                if (!bool.TryParse(value, out var rotate)) return false;
                _rotate = rotate; break;
            default: return false;
        }
        UpdatePreview();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinish) return false;
        Context.Workspace.CreateCircularArray(Entities, _center!.Value, _axis, _count, _sweep, _rotate, _reference);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }
    protected override bool OnStepBack()
    {
        if (!_rotate && _reference is not null) _reference = null;
        else { _center = null; _reference = null; Context.WorkPlane.SetToolPlaneFixed(false); }
        UpdatePreview();
        return true;
    }
    protected override void ResetTransformState() { _center = null; _reference = null; _count = 6; _sweep = 360; _rotate = true; }

    private void UpdatePreview()
    {
        Context.Preview.Clear();
        Context.Snap.Active = _center is null || (!_rotate && _reference is null);
        if (_center is null)
        { SetStageLocalized(0, "Cad.Prompt.circlearray.Center", "Circular Array: specify center [Esc cancel]"); return; }
        if (!_rotate && _reference is null)
        { SetStageLocalized(1, "Cad.Prompt.circlearray.Reference", "Circular Array: specify source reference point away from the axis [Backspace undo, Esc cancel]"); return; }
        SetStageLocalized(2, "Cad.Prompt.circlearray.Parameters", "Circular Array: set count and sweep [Enter accept, Esc cancel]");
        if (!CanFinish)
        { SetPromptLocalized("Cad.Prompt.circlearray.Invalid", "Circular Array: angle must be nonzero; at most 10000 copies [Esc cancel]"); return; }
        var copies = CadCircularArray.CreateCopies(Entities, _center.Value, _axis, _count, _sweep, _rotate, _reference, CadCircularArray.PreviewCopies);
        Context.Preview.Show(copies);
        SetPromptLocalized("Cad.Prompt.circlearray.Preview", "Circular Array: {0} copies; preview shows {1} [Enter accept, Backspace undo, Esc cancel]",
            CadPrecisionInputKind.None, Entities.Count * (_count - 1), copies.Length);
    }
}
