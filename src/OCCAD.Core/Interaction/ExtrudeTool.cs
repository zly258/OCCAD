using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class ExtrudeTool : CadSelectionTransformToolBase
{
    private const double MinimumHeight = 1e-6;
    private double _height = 10.0;
    private bool _reverse;

    public override string Id => "extrude";
    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Confirmation;
    public override string DisplayName => "Extrude";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Extrude",
            [
                new CadDoubleToolParameterDescriptor(
                    "Height",
                    "Height",
                    _height,
                    MinimumHeight,
                    1e12),
                new CadBooleanToolParameterDescriptor(
                    "Reverse",
                    "Reverse",
                    _reverse)
            ]);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "extrude.profiles",
                CadPlanarProfileGeometry.IsSupported));

        if (Context.Selection.Selected.Count > 1)
            Context.Selection.Clear();

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        if (Entities.Count != 1 ||
            !CadPlanarProfileGeometry.IsSupported(Entities[0]))
        {
            Context.Preview.Clear();
            SetStageLocalized(
                0,
                "Cad.Prompt.extrude.Single",
                "Extrude: select exactly one closed planar profile [Esc cancel]");
            return;
        }

        SetStageLocalized(
            0,
            "Cad.Prompt.extrude.Height",
            "Extrude: set height and direction, then click or press Enter [Esc cancel]");
        RefreshPreview();
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect ||
            Entities.Count != 1)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            RefreshPreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return CommitExtrude();
    }

    protected override bool CanCommitCurrentStageCore =>
        Entities.Count == 1;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer) =>
        CommitExtrude();

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (id.Equals(
                "Height",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!TryPositive(value, out var height))
                return false;

            _height = height;
        }
        else if (id.Equals(
                     "Reverse",
                     StringComparison.OrdinalIgnoreCase))
        {
            if (!bool.TryParse(value, out var reverse))
                return false;

            _reverse = reverse;
        }
        else
        {
            return false;
        }

        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState()
    {
        _height = 10.0;
        _reverse = false;
    }

    private bool CommitExtrude()
    {
        if (!TryCreate(out var entity))
            return false;

        Context.Preview.Clear();
        Context.Workspace.AddGeneratedEntities(
            [entity],
            "Extrude");
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
        out CadExtrudeEntity entity)
    {
        entity = null!;
        if (Entities.Count != 1 ||
            !CadPlanarProfileGeometry.IsSupported(Entities[0]))
            return false;

        var source = Entities[0];
        var profile =
            CadPlanarProfileGeometry.Snapshot(source);
        var normal =
            CadPlanarProfileGeometry.Normal(profile);
        var vector =
            normal *
            (_height * (_reverse ? -1.0 : 1.0));

        entity = new CadExtrudeEntity(
            source,
            vector);
        entity.BindProfileSource(source);
        return true;
    }

    private static bool TryPositive(
        string text,
        out double value)
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
               value >= MinimumHeight;
    }
}
