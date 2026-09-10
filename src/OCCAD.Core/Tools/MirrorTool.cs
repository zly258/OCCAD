using OcctNet;

namespace OCCAD;

public sealed class MirrorTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _firstPoint;
    private OcctPoint3d _initialOrigin;
    private bool _keepSource = true;

    public override string Id => "mirror";
    public override string DisplayName => "Mirror";
    public override OcctPoint3d? PrecisionReferencePoint => _firstPoint;

    public override CadToolParameterSchema ParameterSchema =>
        new(
            "Mirror",
            [
                new CadBooleanToolParameterDescriptor(
                    "KeepSource",
                    "Keep Source",
                    _keepSource)
            ]);

    protected override bool SuppressSourcesDuringPreview => !_keepSource;
    protected override bool CanCommitCurrentStageCore => true;
    protected override bool CanStepBackCore => _firstPoint is not null;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        Entities.Count > 0 &&
        TryResolveExactAxisPoint(out _)
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnTransformStarted()
    {
        _firstPoint = null;
        _initialOrigin = Context.WorkPlane.Origin;
        SetStageLocalized(
            0,
            "Cad.Prompt.mirror.First",
            "Mirror: specify first point of mirror axis [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect ||
            Entities.Count == 0)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved &&
            _firstPoint is { } first)
        {
            var point = Context.ResolvePoint(
                input.X,
                input.Y,
                first).Point;
            UpdatePreview(point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                _firstPoint).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (State != CadToolState.Drawing)
            return false;

        if (TryResolveExactAxisPoint(out var exactPoint))
            return AcceptPoint(exactPoint);

        return AcceptPoint(
            Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                _firstPoint).Point);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        Entities.Count > 0 &&
        AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals("KeepSource", StringComparison.OrdinalIgnoreCase) ||
            !bool.TryParse(value, out var keepSource))
            return false;

        if (_keepSource == keepSource)
            return true;

        _keepSource = keepSource;
        RefreshExactOrPointerPreview();
        NotifyUpdated();
        return true;
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        RefreshExactOrPointerPreview();
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_firstPoint is null)
            return false;

        _firstPoint = null;
        ClearTransformPreview();
        Context.WorkPlane.SetOrigin(_initialOrigin);
        SetStageLocalized(
            0,
            "Cad.Prompt.mirror.First",
            "Mirror: specify first point of mirror axis [Esc cancel]");
        return true;
    }

    protected internal override void RefreshPreviewFromLastPointer(
        CadPointerPosition pointer)
    {
        if (_firstPoint is not { } first)
            return;

        if (TryResolveExactAxisPoint(out var exactPoint))
        {
            UpdatePreview(exactPoint);
            return;
        }

        var point = Context.ResolvePoint(
            pointer.X,
            pointer.Y,
            first).Point;
        UpdatePreview(point);
    }

    protected override void ResetTransformState()
    {
        _firstPoint = null;
        _initialOrigin = default;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        if (_firstPoint is null)
        {
            _firstPoint = point;
            Context.WorkPlane.SetOrigin(point);
            ClearTransformPreview();
            SetStageLocalized(
                1,
                "Cad.Prompt.mirror.Second",
                "Mirror: specify second point of mirror axis [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.LengthAndAngle);
            return true;
        }

        if (!TryMirrorPlane(point, out var planeNormal))
        {
            ClearTransformPreview();
            return false;
        }

        try
        {
            CommitTransform(() => Context.Workspace.MirrorEntities(
                Entities,
                _firstPoint.Value,
                planeNormal,
                _keepSource));
            return true;
        }
        catch (NotSupportedException)
        {
            ClearTransformPreview();
            return false;
        }
    }

    private void RefreshExactOrPointerPreview()
    {
        if (_firstPoint is null)
            return;

        if (TryResolveExactAxisPoint(out var exactPoint))
        {
            UpdatePreview(exactPoint);
            return;
        }

        if (Context.Workspace.LastPointerPosition is { } pointer)
            RefreshPreviewFromLastPointer(pointer);
    }

    private bool TryResolveExactAxisPoint(out OcctPoint3d point)
    {
        point = default;
        if (_firstPoint is not { } first)
            return false;

        return CadExactInputGeometry.TryResolveLengthAnglePoint(
                   Context.Workspace,
                   first,
                   out point) ||
               CadExactInputGeometry.TryResolveLockedAnglePoint(
                   Context.Workspace,
                   first,
                   out point);
    }

    private void UpdatePreview(OcctPoint3d secondPoint)
    {
        if (_firstPoint is null ||
            !TryMirrorPlane(secondPoint, out var planeNormal))
        {
            ClearTransformPreview();
            return;
        }

        CadEntity[] previews;
        try
        {
            previews = Entities
                .Select(entity =>
                    entity.MirroredCopy(
                        _firstPoint.Value,
                        planeNormal))
                .ToArray();
        }
        catch (NotSupportedException)
        {
            ClearTransformPreview();
            return;
        }

        if (_keepSource)
        {
            ClearReplacementPreview();
            Context.Preview.Show(previews);
        }
        else
        {
            ShowReplacementPreview(Entities, previews);
        }
    }

    private bool TryMirrorPlane(
        OcctPoint3d secondPoint,
        out OcctVector3d planeNormal)
    {
        planeNormal = default;
        if (_firstPoint is not { } first ||
            !secondPoint.IsFinite)
            return false;

        var frame = Context.WorkPlane.EffectivePlane;
        var delta = secondPoint - first;
        var axial = delta.Dot(frame.Normal);
        var planar = delta - frame.Normal * axial;
        if (!planar.TryNormalize(out var mirrorAxis))
            return false;

        return frame.Normal
            .Cross(mirrorAxis)
            .TryNormalize(out planeNormal);
    }
}
