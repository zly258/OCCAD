using OcctNet;

namespace OCCAD;

public sealed class SphereTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d? _center;
    private double? _radiusParameter;
    private WorkPlaneFrame _initialPlane;

    public override string Id => "sphere";
    public override string DisplayName => "Sphere";
    public override string PrecisionLengthLabel => "Radius";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Sphere",
            [
                new CadOptionalDoubleToolParameterDescriptor(
                    "Radius",
                    "Radius",
                    _radiusParameter,
                    1e-9,
                    double.MaxValue)
            ]);

    protected override bool CanStepBackCore => _center is not null;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        TryResolveExactPoint(out _)
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnActivated()
    {
        _center = null;
        _radiusParameter = null;
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

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactPoint(out var exactPoint))
            return AcceptPoint(exactPoint);

        return CommitResolvedPoint(pointer, _center, AcceptPoint);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals("Radius", StringComparison.OrdinalIgnoreCase) ||
            !TryOptionalPositive(value, out _radiusParameter))
            return false;

        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (TryResolveExactPoint(out var exactPoint) && _center is { } center)
        {
            Update(center, exactPoint);
            return true;
        }

        return base.OnPrecisionInputApplied(input);
    }

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
            RefreshPreview();
            return true;
        }

        var radius = _radiusParameter ?? _center.Value.DistanceTo(point);
        if (radius <= 1e-9)
            return false;

        CommitPreview(
            new CadSphereEntity(
                _center.Value,
                radius));
        return true;
    }

    private void RefreshPreview()
    {
        if (_center is not { } center)
            return;

        if (TryResolveExactPoint(out var exactPoint))
        {
            Update(center, exactPoint);
            return;
        }

        if (Context.Workspace.LastPointerPosition is { } pointer)
        {
            Update(
                center,
                Context.ResolvePoint(pointer.X, pointer.Y, center).Point);
        }
    }

    private bool TryResolveExactPoint(out OcctPoint3d point)
    {
        point = default;
        if (_center is not { } center)
            return false;

        var radius = _radiusParameter;
        if (radius is null &&
            Context.Workspace.Drafting.LengthLockEnabled &&
            Context.Workspace.Drafting.LockedLength > 1e-9)
        {
            radius = Context.Workspace.Drafting.LockedLength;
        }

        if (radius is not { } value || value <= 1e-9)
            return false;

        point = center + Context.WorkPlane.XAxis * value;
        return point.IsFinite;
    }

    private void Update(OcctPoint3d center, OcctPoint3d point)
    {
        var radius = _radiusParameter ?? center.DistanceTo(point);
        if (radius <= 1e-9)
        {
            Context.Preview.Clear();
            return;
        }

        ShowPreview(new CadSphereEntity(center, radius));
    }

    private void Reset()
    {
        _center = null;
        _radiusParameter = null;
    }

    private static bool TryOptionalPositive(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!CadValueTextConverter.TryParseFiniteDouble(text, out var parsed) ||
            parsed <= 0.0)
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }
}
