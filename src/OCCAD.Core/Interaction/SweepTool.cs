using OcctNet;

namespace OCCAD;

public sealed class SweepTool : CadSelectionTransformToolBase
{
    public override string Id => "sweep";
    public override string DisplayName => "Sweep";
    public override bool AllowsPreselectionDuringDrawing => true;

    protected override bool IsSelectionValid(
        CadEntity[] entities) =>
        entities.Length == 1 &&
        CadPlanarProfileGeometry.IsSupported(entities[0]);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "sweep.profiles",
                CadPlanarProfileGeometry.IsSupported));

        if (!IsSelectionValid(
                Context.Selection.Selected.ToArray()))
            Context.Selection.Clear();

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "sweep.path",
                static entity =>
                    entity is CadPolylineEntity { Closed: false }));

        SetStageLocalized(
            0,
            "Cad.Prompt.sweep.Path",
            "Sweep: select an open polyline path [Esc cancel]");
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

        if (!TryCreate(out var entity))
        {
            SetPromptLocalized(
                "Cad.Prompt.sweep.Invalid",
                "Sweep: the hovered object is not a valid open polyline path.");
            return true;
        }

        Context.Preview.Clear();
        Context.Workspace.AddGeneratedEntities(
            [entity],
            "Sweep");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    protected override void ResetTransformState()
    {
    }

    private void RefreshPreview()
    {
        if (TryCreate(out var entity))
            Context.Preview.Show(entity);
        else
            Context.Preview.Clear();
    }

    private bool TryCreate(
        out CadSweepEntity entity)
    {
        entity = null!;

        if (Entities.Count != 1 ||
            Context.Workspace.Preselection.Current is not
            {
                Entity: CadPolylineEntity { Closed: false } path
            })
            return false;

        entity = new CadSweepEntity(
            Entities[0],
            path);
        return true;
    }
}
