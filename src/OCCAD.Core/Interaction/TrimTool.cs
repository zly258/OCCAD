using OcctNet;

namespace OCCAD;

public sealed class TrimTool : CadSelectionTransformToolBase
{
    private bool _invalidTargetPrompt;
    public override string Id => "trim";
    public override string DisplayName => "Trim";
    public override CadToolInputKind InputKind =>
        CadToolInputKind.Selection;
    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with { PreselectionEnabled = true };

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "trim.boundaries",
                static entity =>
                    entity is CadLineEntity or
                    CadPolylineEntity or
                    CadCircleEntity or
                    CadArcEntity or
                    CadPathEntity));
        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        _invalidTargetPrompt = false;
        SetSelectionFilter(
            new CadSelectionFilter(
                "trim.targets",
                static entity =>
                    entity is CadLineEntity or
                    CadArcEntity or
                    CadCircleEntity or
                    CadPolylineEntity or
                    CadPathEntity));
        SetStageLocalized(
            0,
            "Cad.Prompt.trim.Target",
            "Trim: click the curve segment to remove [Esc cancel]");
    }

    protected override bool CanStepBackCore =>
        State == CadToolState.Drawing &&
        Entities.Count > 0;

    protected override bool OnStepBack()
    {
        _invalidTargetPrompt = false;
        SetSelectionFilter(
            new CadSelectionFilter(
                "trim.boundaries",
                static entity =>
                    entity is CadLineEntity or
                    CadPolylineEntity or
                    CadCircleEntity or
                    CadArcEntity or
                    CadPathEntity));
        RestartSelection();
        return true;
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            UpdatePreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return CommitHovered();
    }

    private void UpdatePreview()
    {
        if (!TryBuild(out var target, out var replacements))
        {
            ClearReplacementPreview();
            return;
        }

        if (_invalidTargetPrompt)
        {
            _invalidTargetPrompt = false;
            SetPromptLocalized(
                "Cad.Prompt.trim.Target",
                "Trim: click the curve segment to remove [Esc cancel]");
        }

        ShowReplacementPreview([target], replacements);
    }

    private bool CommitHovered()
    {
        if (!TryBuild(out var target, out var replacements))
        {
            ClearReplacementPreview();
            _invalidTargetPrompt = true;
            SetPromptLocalized(
                "Cad.Prompt.trim.Invalid",
                "Trim: the hovered curve cannot be trimmed by the selected boundaries.");
            return true;
        }

        _invalidTargetPrompt = false;
        CommitReplacementPreview(
            () =>
            {
                Context.Workspace.ReplaceEntities(
                    [target],
                    replacements,
                    "Trim");
                Context.Selection.Apply(
                    Entities,
                    CadSelectionOperation.Replace,
                    Entities.LastOrDefault());
            });
        SetPromptLocalized(
            "Cad.Prompt.trim.Target",
            "Trim: click the line segment to remove [Esc cancel]");
        return true;
    }

    private bool TryBuild(
        out CadEntity target,
        out CadEntity[] replacements)
    {
        target = null!;
        replacements = [];

        if (Context.Workspace.Preselection.Current is not
            {
                Entity: var hovered,
                Point: var hit
            } ||
            Entities.Contains(hovered))
            return false;

        var boundaries = Entities
            .Select(static entity =>
                entity.CreateWorldGeometrySnapshot())
            .ToArray();
        var working =
            hovered.CreateWorldGeometrySnapshot();

        var success = working switch
        {
            CadLineEntity line =>
                CadLineEditGeometry.TryTrim(
                    line,
                    boundaries,
                    hit,
                    Context.WorkPlane,
                    out replacements),
            CadArcEntity arc =>
                CadArcEditGeometry.TryTrim(
                    arc,
                    boundaries,
                    hit,
                    Context.WorkPlane,
                    out replacements),
            CadPolylineEntity polyline =>
                CadPolylineEditGeometry.TryTrim(
                    polyline,
                    boundaries,
                    hit,
                    Context.WorkPlane,
                    out replacements),
            CadCircleEntity circle =>
                CadCircleEditGeometry.TryTrim(
                    circle,
                    boundaries,
                    hit,
                    Context.WorkPlane,
                    out replacements),
            CadPathEntity path =>
                CadPathEditGeometry.TryTrim(
                    path,
                    boundaries,
                    hit,
                    Context.WorkPlane,
                    out replacements),
            _ => false
        };

        if (!success)
            return false;

        target = hovered;
        return true;
    }
}
