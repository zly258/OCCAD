using OcctNet;

namespace OCCAD;

public sealed class LoftTool : CadSelectionTransformToolBase
{
    private bool _makeSolid = true;
    private bool _ruled;

    public override string Id => "loft";
    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Confirmation;
    public override string DisplayName => "Loft";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Loft",
            [
                new CadBooleanToolParameterDescriptor(
                    "MakeSolid",
                    "Solid",
                    _makeSolid),
                new CadBooleanToolParameterDescriptor(
                    "Ruled",
                    "Ruled",
                    _ruled)
            ]);

    protected override bool IsSelectionValid(
        CadEntity[] entities) =>
        entities.Length >= 2 &&
        entities.All(
            CadPlanarProfileGeometry.IsWireProfile);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "loft.sections",
                CadPlanarProfileGeometry.IsWireProfile));

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        SetStageLocalized(
            0,
            "Cad.Prompt.loft.Ready",
            "Loft: review section order and options, then click or press Enter [Esc cancel]");
        RefreshPreview();
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            RefreshPreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return CommitLoft();
    }

    protected override bool CanCommitCurrentStageCore =>
        Entities.Count >= 2;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer) =>
        CommitLoft();

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!bool.TryParse(value, out var parsed))
            return false;

        if (id.Equals(
                "MakeSolid",
                StringComparison.OrdinalIgnoreCase))
            _makeSolid = parsed;
        else if (id.Equals(
                     "Ruled",
                     StringComparison.OrdinalIgnoreCase))
            _ruled = parsed;
        else
            return false;

        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState()
    {
        _makeSolid = true;
        _ruled = false;
    }

    private bool CommitLoft()
    {
        if (!TryCreate(out var entity))
            return false;

        Context.Preview.Clear();
        Context.Workspace.AddGeneratedEntities(
            [entity],
            "Loft");
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

    private bool TryCreate(
        out CadLoftEntity entity)
    {
        entity = null!;

        if (Entities.Count < 2 ||
            !Entities.All(
                CadPlanarProfileGeometry.IsWireProfile))
            return false;

        entity = new CadLoftEntity(
            Entities,
            _makeSolid,
            _ruled);
        entity.BindSources(Entities);
        return true;
    }
}
