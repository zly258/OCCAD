using OcctNet;

namespace OCCAD;

public sealed class TrimTool : CadSelectionTransformToolBase
{
    public override string Id => "trim";
    public override string DisplayName => "Trim";
    public override bool AllowsPreselectionDuringDrawing => true;

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
        SetSelectionFilter(
            new CadSelectionFilter(
                "trim.targets",
                static entity =>
                    entity is CadLineEntity or
                    CadArcEntity or
                    CadCircleEntity or
                    CadPolylineEntity));
        SetStageLocalized(
            0,
            "Cad.Prompt.trim.Target",
            "Trim: click the line segment to remove [Esc cancel]");
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
        if (!TryBuild(out _, out var replacements))
        {
            Context.Preview.Clear();
            return;
        }

        Context.Preview.Show(replacements);
    }

    private bool CommitHovered()
    {
        if (!TryBuild(out var target, out var replacements))
        {
            SetPromptLocalized(
                "Cad.Prompt.trim.Invalid",
                "Trim: the hovered line cannot be trimmed by the selected cutting lines.");
            return true;
        }

        Context.Preview.Clear();
        Context.Workspace.ReplaceEntities(
            [target],
            replacements,
            "Trim");
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

        var boundaries = Entities.ToArray();

        var success = hovered switch
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
            _ => false
        };

        if (!success)
            return false;

        target = hovered;
        return true;
    }
}
