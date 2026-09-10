using OcctNet;

namespace OCCAD;

public sealed class LoftTool : CadTool
{
    private readonly List<CadEntity> _sections = [];
    private bool _makeSolid = true;
    private bool _ruled;

    public override string Id => "loft";
    public override string DisplayName => "Loft";
    public override CadToolInputKind InputKind =>
        CadToolInputKind.Selection;
    public override CadToolInteractionPolicy InteractionPolicy =>
        CadToolInteractionPolicy.Selection;

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

    protected override void OnActivated()
    {
        _sections.Clear();
        SetSelectionFilter(
            new CadSelectionFilter(
                "loft.sections",
                CadPlanarProfileGeometry.IsWireProfile));
        SetState(CadToolState.WaitForSelect);
        Context.WorkPlane.EndToolPlane();
        SetPromptLocalized(
            "Cad.Prompt.loft.Select",
            "Loft: select sections in order [Enter or blank click finishes after 2, Esc cancel]");
    }

    protected override void OnCanceled()
    {
        _sections.Clear();
    }

    protected override void OnDeactivated()
    {
        _sections.Clear();
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        if (Context.Workspace.Preselection.Current is
            { Entity: var entity } &&
            CadPlanarProfileGeometry.IsWireProfile(entity))
        {
            if (_sections.Contains(entity))
            {
                SetPromptLocalized(
                    "Cad.Prompt.loft.Duplicate",
                    "Loft: section already selected; choose another section.");
                return true;
            }

            _sections.Add(entity);
            RefreshPreview();
            UpdatePrompt();
            return true;
        }

        if (_sections.Count >= 2)
            return CommitLoft();

        SetPromptLocalized(
            "Cad.Prompt.loft.Minimum",
            "Loft: select at least two sections before finishing.");
        return true;
    }

    protected override bool CanCommitCurrentStageCore =>
        _sections.Count >= 2;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer) =>
        CommitLoft();

    protected override bool CanFinishCore =>
        _sections.Count >= 2;

    protected override bool OnFinish() =>
        CommitLoft();

    protected override bool CanStepBackCore =>
        _sections.Count > 0;

    protected override bool OnStepBack()
    {
        if (_sections.Count == 0)
            return false;

        _sections.RemoveAt(_sections.Count - 1);
        RefreshPreview();
        UpdatePrompt();
        return true;
    }

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

    private void UpdatePrompt()
    {
        if (_sections.Count == 0)
        {
            SetPromptLocalized(
                "Cad.Prompt.loft.Select",
                "Loft: select sections in order [Enter or blank click finishes after 2, Esc cancel]");
            return;
        }

        SetPromptLocalized(
            "Cad.Prompt.loft.Selected",
            "Loft: {0} sections selected; continue selecting or press Enter/click blank to finish [Backspace removes last]",
            CadPrecisionInputKind.None,
            _sections.Count);
    }

    private bool CommitLoft()
    {
        if (!TryCreate(out var entity))
            return false;

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
        if (_sections.Count < 2 ||
            _sections.Any(section =>
                !CadPlanarProfileGeometry.IsWireProfile(section)))
            return false;

        entity = new CadLoftEntity(
            _sections,
            _makeSolid,
            _ruled);
        entity.BindSources(_sections);
        return true;
    }
}
