using OcctNet;

namespace OCCAD;

/// <summary>
/// Creates an associative center line from two host line entities, matching the
/// OCCTBIM-Source CenterLineTool workflow rather than drawing a free two-point
/// segment.
/// </summary>
public sealed class CenterLineTool : CadTool
{
    private CadLineEntity? _firstLine;

    public override string Id => "centerline";
    public override string DisplayName => "Center Line";
    public override CadToolInputKind InputKind => CadToolInputKind.Selection;
    public override CadToolInteractionPolicy InteractionPolicy =>
        CadToolInteractionPolicy.Selection;
    public override CadSelectionScope SelectionScope =>
        CadSelectionScope.Entity;

    public override bool CanCommitCurrentStage => false;

    protected override void OnActivated()
    {
        _firstLine = null;
        SetSelectionFilter(
            new CadEntityFilter(
                "centerline.host-line",
                static entity => entity is CadLineEntity));
        SetState(CadToolState.WaitForSelect);
        SetStageLocalized(
            0,
            "Cad.Prompt.CenterLine.First",
            "Center Line: select first line [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        var detected =
            Context.Workspace.Preselection.Current?.Entity;
        if (detected is not CadLineEntity line ||
            !Context.Document.IsEntitySelectable(line))
        {
            SetPromptLocalized(
                "Cad.Prompt.CenterLine.LineRequired",
                "Center Line: select a line entity");
            return true;
        }

        if (_firstLine is null)
        {
            _firstLine = line;
            SetStageLocalized(
                1,
                "Cad.Prompt.CenterLine.Second",
                "Center Line: select second line [Esc cancel]");
            return true;
        }

        if (ReferenceEquals(_firstLine, line) ||
            _firstLine.Id == line.Id)
        {
            SetPromptLocalized(
                "Cad.Prompt.CenterLine.DifferentLine",
                "Center Line: select a different second line");
            return true;
        }

        try
        {
            var centerLine =
                CadCenterLineEntity.CreateAssociative(
                    _firstLine,
                    line);

            Context.Workspace.AddGeneratedEntities(
                [centerLine],
                "Create Center Line");
            Context.Workspace.Tools.CompleteCurrent();
        }
        catch (Exception exception)
            when (exception is ArgumentException or InvalidOperationException)
        {
            SetPromptLocalized(
                "Cad.Prompt.CenterLine.Invalid",
                "Center Line: the selected lines do not define a valid center line");
        }

        return true;
    }

    protected override void OnCanceled() =>
        _firstLine = null;

    protected override void OnDeactivated() =>
        _firstLine = null;
}
