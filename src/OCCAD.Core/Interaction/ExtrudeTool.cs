using OcctNet;

namespace OCCAD;

public sealed class ExtrudeTool :
    CadSelectionTransformToolBase,
    ICadPointInputTool
{
    private const double MinimumHeight = 1e-6;
    private double _height = 10.0;
    private bool _reverse;
    private OcctPoint3d _center;
    private OcctVector3d _normal = OcctVector3d.UnitZ;

    public override string Id => "extrude";
    public override string DisplayName => "Extrude";

    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Point;

    public override CadToolInteractionPolicy InteractionPolicy =>
        State == CadToolState.WaitForSelect
            ? CadToolInteractionPolicy.Selection
            : CadToolInteractionPolicy.Drawing;

    public override string PrecisionLengthLabel => "Height";

    public override OcctPoint3d? PrecisionReferencePoint =>
        State == CadToolState.Drawing && Entities.Count == 1
            ? _center
            : base.PrecisionReferencePoint;

    // Extrude supports parameter-only precision input. Once a valid profile is
    // selected, Height/Reverse define a complete preview and Accept/Enter can
    // commit without requiring another pointer event.
    public override bool CanCommitCurrentStage =>
        State == CadToolState.WaitForSelect
            ? base.CanCommitCurrentStage
            : IsActive &&
              State == CadToolState.Drawing &&
              Entities.Count == 1 &&
              double.IsFinite(_height) &&
              _height >= MinimumHeight;

    protected override bool AutoCommitValidSelection => true;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Extrude",
            [
                new CadDoubleToolParameterDescriptor(
                    "Height",
                    "Height",
                    _height,
                    MinimumHeight,
                    1e12),
                new CadBooleanToolParameterDescriptor(
                    "Reverse",
                    "Reverse",
                    _reverse)
            ]);

    protected override bool IsSelectionValid(CadEntity[] entities) =>
        entities.Length == 1 &&
        CadPlanarProfileGeometry.IsSupported(entities[0]);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "extrude.profiles",
                CadPlanarProfileGeometry.IsSupported));

        if (!IsSelectionValid(Context.Selection.Selected.ToArray()))
            Context.Selection.Clear();

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        if (Entities.Count != 1 ||
            !CadPlanarProfileGeometry.IsSupported(Entities[0]))
        {
            RestartSelection();
            return;
        }

        var profile = CadPlanarProfileGeometry.Snapshot(Entities[0]);
        _center = CadPlanarProfileGeometry.Center(profile);
        _normal = CadPlanarProfileGeometry.Normal(profile).Normalized();
        var axes = CadTransformMath.PerpendicularAxes(_normal);

        SetSelectionFilter(null);
        SetWorkPlane(
            _center,
            axes.XAxis,
            _normal,
            lockPlane: true);
        SetStageLocalized(
            1,
            "Cad.Prompt.extrude.Height",
            "Extrude: move the pointer or enter Height, then Accept [Backspace profile, Esc cancel]",
            CadPrecisionInputKind.Length);
        LockStageAngle(90.0);
        RefreshPreview();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (State == CadToolState.WaitForSelect ||
            Entities.Count != 1)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            var point = Context.ResolvePoint(
                input.X,
                input.Y,
                _center).Point;
            if (TrySetHeightFromPoint(point))
                RefreshPreview();
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left)
        {
            var point = Context.ResolvePoint(
                input.X,
                input.Y,
                _center).Point;
            return TryAcceptPoint(point);
        }

        return false;
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive ||
            State != CadToolState.Drawing ||
            Entities.Count != 1 ||
            !point.IsFinite ||
            !TrySetHeightFromPoint(point))
            return false;

        RefreshPreview();
        return CommitExtrude();
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitExtrude();

    protected override bool CanStepBackCore =>
        State == CadToolState.Drawing &&
        Entities.Count == 1;

    protected override bool OnStepBack()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "extrude.profiles",
                CadPlanarProfileGeometry.IsSupported));
        RestartSelection();
        return true;
    }

    protected override bool CanFinishCore =>
        State == CadToolState.Drawing &&
        Entities.Count == 1 &&
        double.IsFinite(_height) &&
        _height >= MinimumHeight;

    protected override bool OnFinish() => CommitExtrude();

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (input.Length is { } length)
        {
            if (!double.IsFinite(length) || length < MinimumHeight)
                return false;
            _height = length;
        }

        RefreshPreview();
        return true;
    }

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Height", StringComparison.OrdinalIgnoreCase))
        {
            if (!CadValueTextConverter.TryParseFiniteDouble(value, out var height) ||
                height < MinimumHeight ||
                height > 1e12)
                return false;
            _height = height;
        }
        else if (id.Equals("Reverse", StringComparison.OrdinalIgnoreCase))
        {
            if (!bool.TryParse(value, out var reverse))
                return false;
            _reverse = reverse;
        }
        else
        {
            return false;
        }

        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState()
    {
        _height = 10.0;
        _reverse = false;
        _center = default;
        _normal = OcctVector3d.UnitZ;
    }

    private bool TrySetHeightFromPoint(OcctPoint3d point)
    {
        var signed = CadTransformMath.Dot(
            CadTransformMath.Between(_center, point),
            _normal);
        if (!double.IsFinite(signed) ||
            Math.Abs(signed) < MinimumHeight)
            return false;

        _height = Math.Abs(signed);
        _reverse = signed < 0.0;
        NotifyUpdated();
        return true;
    }

    private bool CommitExtrude()
    {
        if (!TryCreate(out var entity))
            return false;

        Context.Workspace.AddGeneratedEntities([entity], "Extrude");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void RefreshPreview()
    {
        if (TryCreate(out var entity))
            Context.Preview.Show(entity);
        else
            Context.Preview.Clear();
    }

    private bool TryCreate(out CadExtrudeEntity entity)
    {
        entity = null!;
        if (Entities.Count != 1 ||
            !CadPlanarProfileGeometry.IsSupported(Entities[0]) ||
            !double.IsFinite(_height) ||
            _height < MinimumHeight)
            return false;

        var vector = _normal * (_height * (_reverse ? -1.0 : 1.0));
        entity = new CadExtrudeEntity(Entities[0], vector);
        entity.BindProfileSource(Entities[0]);
        return true;
    }
}
