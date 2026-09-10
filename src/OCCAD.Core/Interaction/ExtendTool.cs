using OcctNet;

namespace OCCAD;

public sealed class ExtendTool : CadSelectionTransformToolBase
{
    public override string Id => "extend";
    public override string DisplayName => "Extend";
    public override bool AllowsPreselectionDuringDrawing => true;

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "extend.lines",
                static entity => entity is CadLineEntity));
        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        SetStageLocalized(
            0,
            "Cad.Prompt.extend.Target",
            "Extend: click near the endpoint to extend [Esc cancel]");
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
        if (!TryBuild(out _, out var replacement))
        {
            Context.Preview.Clear();
            return;
        }

        Context.Preview.Show(replacement);
    }

    private bool CommitHovered()
    {
        if (!TryBuild(out var target, out var replacement))
        {
            SetPromptLocalized(
                "Cad.Prompt.extend.Invalid",
                "Extend: the hovered line cannot be extended to the selected boundary lines.");
            return true;
        }

        Context.Preview.Clear();
        Context.Workspace.ApplyGeneratedGeometryChange(
            [target],
            "Extend",
            entity => entity.RestoreGeometry(replacement));
        SetPromptLocalized(
            "Cad.Prompt.extend.Target",
            "Extend: click near the endpoint to extend [Esc cancel]");
        return true;
    }

    private bool TryBuild(
        out CadLineEntity target,
        out CadLineEntity replacement)
    {
        target = null!;
        replacement = null!;

        if (Context.Workspace.Preselection.Current is not
            {
                Entity: CadLineEntity hovered,
                Point: var hit
            } ||
            Entities.Contains(hovered))
            return false;

        var boundaries = Entities
            .OfType<CadLineEntity>()
            .ToArray();
        if (!CadLineEditGeometry.TryExtend(
                hovered,
                boundaries,
                hit,
                Context.WorkPlane,
                out replacement))
            return false;

        target = hovered;
        return true;
    }
}
