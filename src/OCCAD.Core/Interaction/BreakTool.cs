using OcctNet;

namespace OCCAD;

public sealed class BreakTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private OcctPoint3d? _firstPoint;

    public override string Id => "break";
    public override string DisplayName => "Break";

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "break.curves",
                static entity =>
                    entity is CadLineEntity or
                    CadPolylineEntity or
                    CadCircleEntity or
                    CadArcEntity or
                    CadPathEntity));

        if (Context.Selection.Selected.Count > 1)
            Context.Selection.Clear();

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        _firstPoint = null;
        Context.Preview.Clear();

        if (Entities.Count != 1)
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.break.Single",
                "Break: select exactly one curve or path [Esc cancel]");
            return;
        }

        SetStageLocalized(
            0,
            "Cad.Prompt.break.First",
            "Break: specify first break point [Esc cancel]");
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect ||
            Entities.Count != 1)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved &&
            _firstPoint is { } first)
        {
            var second =
                Context.ResolvePoint(
                    input.X,
                    input.Y,
                    first).Point;
            UpdatePreview(first, second);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        var point = Context.ResolvePoint(
            input.X,
            input.Y,
            _firstPoint).Point;
        return AcceptPoint(point);
    }

    protected override bool CanCommitCurrentStageCore =>
        Entities.Count == 1;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer)
    {
        if (Entities.Count != 1)
            return false;

        var point = Context.ResolvePoint(
            pointer.X,
            pointer.Y,
            _firstPoint).Point;
        return AcceptPoint(point);
    }

    public bool TryAcceptPoint(
        OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        Entities.Count == 1 &&
        AcceptPoint(point);

    protected override bool CanStepBackCore =>
        _firstPoint is not null;

    protected override bool OnStepBack()
    {
        if (_firstPoint is null)
            return false;

        _firstPoint = null;
        Context.Preview.Clear();
        SetStageLocalized(
            0,
            "Cad.Prompt.break.First",
            "Break: specify first break point [Esc cancel]");
        return true;
    }

    protected override void ResetTransformState()
    {
        _firstPoint = null;
    }

    private bool AcceptPoint(
        OcctPoint3d point)
    {
        if (_firstPoint is null)
        {
            _firstPoint = point;
            SetStageLocalized(
                1,
                "Cad.Prompt.break.Second",
                "Break: specify second break point [Backspace undo, Esc cancel]");
            return true;
        }

        if (!CadBreakGeometry.TryBreak(
                Entities[0],
                _firstPoint.Value,
                point,
                Context.WorkPlane,
                out var replacements))
        {
            Context.Preview.Clear();
            SetPromptLocalized(
                "Cad.Prompt.break.Invalid",
                "Break: the two points do not define a valid removable segment.");
            return true;
        }

        Context.Preview.Clear();
        Context.Workspace.ReplaceEntities(
            [Entities[0]],
            replacements,
            "Break");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void UpdatePreview(
        OcctPoint3d firstPoint,
        OcctPoint3d secondPoint)
    {
        if (CadBreakGeometry.TryBreak(
                Entities[0],
                firstPoint,
                secondPoint,
                Context.WorkPlane,
                out var replacements))
        {
            Context.Preview.Show(replacements);
        }
        else
        {
            Context.Preview.Clear();
        }
    }
}
