using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class ShapeOffsetTool : CadSelectionTransformToolBase
{
    private double _offset = 2.0;

    public override string Id => "shapeoffset";
    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Confirmation;
    public override string DisplayName => "Shape Offset";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Shape Offset",
            [
                new CadDoubleToolParameterDescriptor(
                    "Offset",
                    "Offset",
                    _offset,
                    -1e12,
                    1e12)
            ]);

    protected override bool IsSelectionValid(CadEntity[] entities) =>
        entities.Length == 1 &&
        CadSolidFeatureGeometry.IsSolid(entities[0]);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "shapeoffset.solids",
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
            "Cad.Prompt.shapeoffset.Ready",
            "Shape Offset: set signed offset, then click or press Enter [Esc cancel]");
        RefreshPreview();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
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

        return CommitOffset();
    }

    protected override bool CanCommitCurrentStageCore =>
        Entities.Count == 1;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer) =>
        CommitOffset();

    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals(
                "Offset",
                StringComparison.OrdinalIgnoreCase) ||
            !TryNonZero(value, out var offset))
            return false;

        _offset = offset;
        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState() =>
        _offset = 2.0;

    private bool CommitOffset()
    {
        if (Entities.Count != 1)
            return false;

        var feature =
            new CadShapeOffsetEntity(
                Entities[0],
                _offset);

        CommitReplacementPreview(
            () => Context.Workspace.ReplaceEntities(
                Entities,
                [feature],
                "Shape Offset"));
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void RefreshPreview()
    {
        if (Entities.Count == 1)
        {
            ShowReplacementPreview(
                Entities,
                [
                    new CadShapeOffsetEntity(
                        Entities[0],
                        _offset)
                ]);
        }
        else
        {
            ClearReplacementPreview();
        }
    }

    private static bool TryNonZero(string text, out double value)
    {
        var parsed =
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value) ||
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);

        return parsed &&
               double.IsFinite(value) &&
               Math.Abs(value) >= 1e-6;
    }
}
