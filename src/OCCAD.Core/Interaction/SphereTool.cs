using OcctNet;

namespace OCCAD;

public sealed class SphereTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d? _center;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "sphere";
    public override string DisplayName => "Sphere";

    protected override bool CanStepBackCore => _center is not null;

    protected override void OnActivated()
    {
        _center = null;
        _initialPlane = CaptureWorkPlaneFrame();
        SetStageLocalized(
            0,
            "Cad.Prompt.Sphere.Center",
            "Sphere: specify center [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;

        if (input.Kind == OcctPointerInputKind.Moved && _center is { } center)
        {
            Update(center, Context.ResolvePoint(input.X, input.Y, center).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(Context.ResolvePoint(input.X, input.Y, _center).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, _center, AcceptPoint);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnStepBack()
    {
        if (_center is null) return false;
        _center = null;
        Context.Preview.Clear();
        RestoreWorkPlaneFrame(_initialPlane);
        SetStageLocalized(
            0,
            "Cad.Prompt.Sphere.Center",
            "Sphere: specify center [Esc cancel]");
        return true;
    }

    protected override void OnCanceled() => Reset();

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (_center is null)
        {
            _center = point;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(
                1,
                "Cad.Prompt.Sphere.Radius",
                "Sphere: specify radius [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            return true;
        }

        var radius = _center.Value.DistanceTo(point);
        if (radius <= 1e-9) return true;

        Context.AddEntity(new CadSphereEntity(_center.Value, radius));
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void Update(OcctPoint3d center, OcctPoint3d point)
    {
        var radius = center.DistanceTo(point);
        if (radius <= 1e-9)
            return;

        ShowPreview(new CadSphereEntity(center, radius));
    }

    private void Reset()
    {
        _center = null;
    }
}
