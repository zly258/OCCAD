using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class PathArrayTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private CadArrayPath? _path;
    private CadEntity? _pathEntity;
    private OcctPoint3d? _reference;
    private OcctVector3d _forward;
    private OcctVector3d _up;
    private double _spacing = 10;
    private bool _align = true;
    public override string Id => "patharray";
    public override string DisplayName => "Path Array";
    protected override bool CanCommitCurrentStageCore => State == CadToolState.Drawing && _reference is null;
    protected override bool CanStepBackCore => _path is not null;
    protected override bool CanFinishCore => State == CadToolState.Drawing && _reference is not null && CopyCount() > 0;
    public override CadToolPanelDescriptor? ParameterPanel => State != CadToolState.Drawing ? null :
        new("Path Array", [new CadDoubleToolParameterDescriptor("Spacing", "Spacing", _spacing, 0.000001, double.MaxValue),
            new CadBooleanToolParameterDescriptor("Align", "Align work-plane X to tangent", _align)]);

    protected override void OnTransformStarted()
    {
        _forward = Context.WorkPlane.XAxis;
        _up = Context.WorkPlane.Normal;
        Context.Document.Changed += DocumentChanged;
        UpdatePreview();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (State != CadToolState.Drawing || input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left) return false;
        AcceptAt(input.X, input.Y);
        return true;
    }

    private bool AcceptAt(int x, int y)
    {
        if (_path is not null) return TryAcceptPoint(Context.ResolvePoint(x, y).Point);
        foreach (var hit in Context.Engine.DetectAt(x, y))
        {
            var entity = Context.Document.FindByViewerObject(hit.Owner);
            if (entity is not null && TryAcceptPath(entity)) return true;
        }
        SetPromptLocalized("Cad.Prompt.patharray.InvalidPath", "Path Array: select a curve or connected wire outside the source selection [Esc cancel]");
        return false;
    }

    public bool TryAcceptPath(CadEntity entity)
    {
        if (!IsActive || State != CadToolState.Drawing || _path is not null ||
            !Context.Document.IsEntitySelectable(entity) || Entities.Contains(entity) || !CadArrayPath.Supports(entity)) return false;
        CadArrayPath path;
        try { path = new CadArrayPath(entity); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OcctException)
        {
            SetPromptLocalized("Cad.Prompt.patharray.InvalidPath", "Path Array: select a curve or connected wire outside the source selection [Esc cancel]");
            return false;
        }
        _path = path;
        _pathEntity = entity;
        Context.WorkPlane.SetToolPlaneFixed(true);
        UpdatePreview();
        return true;
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive || State != CadToolState.Drawing || _path is null || _reference is not null || !point.IsFinite) return false;
        _reference = point;
        UpdatePreview();
        return true;
    }
    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) => AcceptAt(pointer.X, pointer.Y);
    protected override bool OnSetParameter(string id, string value)
    {
        if (State != CadToolState.Drawing) return false;
        if (id.Equals("Spacing", StringComparison.OrdinalIgnoreCase))
        {
            if ((!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var spacing) &&
                !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out spacing)) ||
                !double.IsFinite(spacing) || spacing < 0.000001) return false;
            _spacing = spacing;
        }
        else if (id.Equals("Align", StringComparison.OrdinalIgnoreCase))
        {
            if (!bool.TryParse(value, out var align)) return false;
            _align = align;
        }
        else return false;
        UpdatePreview();
        return true;
    }
    protected override bool OnFinish()
    {
        if (!CanFinish) return false;
        Context.Workspace.CreatePathArray(Entities, _pathEntity!, _spacing, _reference!.Value, _forward, _up, _align);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }
    protected override bool OnStepBack()
    {
        if (_reference is not null) _reference = null;
        else { _path?.Dispose(); _path = null; _pathEntity = null; Context.WorkPlane.SetToolPlaneFixed(false); }
        UpdatePreview();
        return true;
    }
    protected override void OnDeactivated()
    {
        Context.Document.Changed -= DocumentChanged;
        base.OnDeactivated();
    }
    protected override void ResetTransformState()
    {
        _path?.Dispose(); _path = null; _pathEntity = null; _reference = null; _spacing = 10; _align = true;
    }
    private void DocumentChanged(object? sender, CadDocumentChangedEventArgs args)
    {
        if (args.Kind == CadDocumentChangeKind.Added) return;
        if (args.Entity is null || ReferenceEquals(args.Entity, _pathEntity) || Entities.Contains(args.Entity))
            Context.Workspace.Tools.CancelCurrent();
    }
    private int CopyCount()
    {
        if (_path is null || Entities.Count == 0 || Entities.Count > CadPathArray.MaximumCopies) return 0;
        try { return _path.StationCount(_spacing, CadPathArray.MaximumCopies / Entities.Count) * Entities.Count; }
        catch (ArgumentException) { return 0; }
    }
    private void UpdatePreview()
    {
        Context.Preview.Clear();
        Context.Snap.Active = _path is not null && _reference is null;
        if (_path is null)
        { SetStageLocalized(0, "Cad.Prompt.patharray.Path", "Path Array: select the path [Esc cancel]"); return; }
        if (_reference is null)
        { SetStageLocalized(1, "Cad.Prompt.patharray.Reference", "Path Array: specify source reference point [Backspace undo, Esc cancel]"); return; }
        SetStageLocalized(2, "Cad.Prompt.patharray.Parameters", "Path Array: set spacing and alignment [Enter accept, Esc cancel]");
        var count = CopyCount();
        if (count == 0)
        { SetPromptLocalized("Cad.Prompt.patharray.Invalid", "Path Array: increase spacing; at most 10000 copies [Esc cancel]"); return; }
        var copies = CadPathArray.CreateCopies(Entities, _path, _spacing, _reference.Value, _forward, _up, _align, CadPathArray.PreviewCopies);
        Context.Preview.Show(copies);
        SetPromptLocalized("Cad.Prompt.patharray.Preview", "Path Array: {0} copies; preview shows {1} [Enter accept, Backspace undo, Esc cancel]",
            CadPrecisionInputKind.None, count, copies.Length);
    }
}
