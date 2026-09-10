using OcctNet;

namespace OCCAD;

/// <summary>
/// Creates an associative center mark from a host circle. The tool owns only the
/// selection workflow; geometry, dependency refresh and persistence belong to
/// <see cref="CadCenterMarkEntity"/>.
/// </summary>
public sealed class CenterMarkTool : CadTool
{
    public override string Id => "centermark";
    public override string DisplayName => "Center Mark";
    public override CadToolInputKind InputKind => CadToolInputKind.Selection;
    public override CadToolInteractionPolicy InteractionPolicy =>
        CadToolInteractionPolicy.Selection;
    public override CadSelectionScope SelectionScope => CadSelectionScope.Entity;
    public override bool CanCommitCurrentStage => false;

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadEntityFilter(
                "centermark.host-circle",
                static entity => entity is CadCircleEntity));
        SetState(CadToolState.WaitForSelect);
        SetStageLocalized(
            0,
            "Cad.Prompt.CenterMark.Circle",
            "Center Mark: select host circle [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        var detected = Context.Workspace.Preselection.Current?.Entity;
        if (detected is not CadCircleEntity circle ||
            !Context.Document.IsEntitySelectable(circle))
        {
            SetPromptLocalized(
                "Cad.Prompt.CenterMark.CircleRequired",
                "Center Mark: select a circle entity");
            return true;
        }

        var mark = CadCenterMarkEntity.CreateAssociative(circle);
        Context.Workspace.AddGeneratedEntities(
            [mark],
            "Create Center Mark");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }
}
