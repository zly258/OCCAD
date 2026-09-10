using OcctNet;

namespace OCCAD;

public sealed class BooleanTool : CadSelectionTransformToolBase
{
    private CadBooleanKind _operation = CadBooleanKind.Union;

    public override string Id => "boolean";
    public override string DisplayName => "Boolean";
    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Confirmation;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Boolean",
            [
                new CadChoiceToolParameterDescriptor(
                    "Operation",
                    "Operation",
                    _operation.ToString(),
                    Enum.GetNames<CadBooleanKind>())
            ]);

    protected override bool IsSelectionValid(
        CadEntity[] entities) =>
        entities.Length == 2 &&
        entities.All(CadSolidFeatureGeometry.IsSolid);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "boolean.solids",
                CadSolidFeatureGeometry.IsSolid));

        if (!IsSelectionValid(
                Context.Selection.Selected.ToArray()))
            Context.Selection.Clear();

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        SetStageLocalized(
            0,
            "Cad.Prompt.boolean.Ready",
            "Boolean: review operation, then click or press Enter [Esc cancel]");
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

        return CommitBoolean();
    }

    protected override bool CanCommitCurrentStageCore =>
        Entities.Count == 2;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer) =>
        CommitBoolean();

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!id.Equals(
                "Operation",
                StringComparison.OrdinalIgnoreCase) ||
            !Enum.TryParse<CadBooleanKind>(
                value,
                ignoreCase: true,
                out var operation) ||
            !Enum.IsDefined(operation))
            return false;

        _operation = operation;
        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState() =>
        _operation = CadBooleanKind.Union;

    private bool CommitBoolean()
    {
        if (!TryCreate(out var entity))
            return false;

        CommitReplacementPreview(
            () => Context.Workspace.ReplaceEntities(
                Entities,
                [entity],
                $"Boolean {_operation}"));
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void RefreshPreview()
    {
        if (TryCreate(out var entity))
            ShowReplacementPreview(
                Entities,
                [entity]);
        else
            ClearReplacementPreview();
    }

    private bool TryCreate(
        out CadBooleanEntity entity)
    {
        entity = null!;
        if (Entities.Count != 2 ||
            !Entities.All(CadSolidFeatureGeometry.IsSolid))
            return false;

        entity = new CadBooleanEntity(
            Entities[0],
            Entities[1],
            _operation);
        return true;
    }
}
