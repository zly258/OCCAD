using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class RevolveTool : CadSelectionTransformToolBase
{
    private const double MinimumAngle = 1e-6;
    private double _angleDegrees = 360.0;

    public override string Id => "revolve";
    public override string DisplayName => "Revolve";
    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with { PreselectionEnabled = true };

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Revolve",
            [
                new CadDoubleToolParameterDescriptor(
                    "Angle",
                    "Angle",
                    _angleDegrees,
                    -360.0,
                    360.0)
            ]);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "revolve.profiles",
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
                "Cad.Prompt.revolve.Single",
                "Revolve: select exactly one closed planar profile [Esc cancel]");
            return;
        }

        SetSelectionFilter(
            new CadSelectionFilter(
                "revolve.axis",
                static entity => entity is CadLineEntity));
        SetStageLocalized(
            0,
            "Cad.Prompt.revolve.Axis",
            "Revolve: select an axis line [Esc cancel]");
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

        if (!TryCreate(out var entity))
        {
            SetPromptLocalized(
                "Cad.Prompt.revolve.Invalid",
                "Revolve: the hovered line is not a valid axis for this profile.");
            return true;
        }

        Context.Preview.Clear();
        Context.Workspace.AddGeneratedEntities(
            [entity],
            "Revolve");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!id.Equals(
                "Angle",
                StringComparison.OrdinalIgnoreCase) ||
            !TryAngle(value, out var angle))
            return false;

        _angleDegrees = angle;
        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState() =>
        _angleDegrees = 360.0;

    private void RefreshPreview()
    {
        if (TryCreate(out var entity))
            Context.Preview.Show(entity);
        else
            Context.Preview.Clear();
    }

    private bool TryCreate(
        out CadRevolveEntity entity)
    {
        entity = null!;

        if (Entities.Count != 1 ||
            Context.Workspace.Preselection.Current is not
            {
                Entity: CadLineEntity axis
            })
            return false;

        var profile =
            CadPlanarProfileGeometry.Snapshot(Entities[0]);
        var axisDirection =
            CadTransformMath.Between(
                axis.Start,
                axis.End);
        if (!axisDirection.TryNormalize(out var direction))
            return false;

        entity = new CadRevolveEntity(
            profile,
            axis.Start,
            direction,
            _angleDegrees);
        return true;
    }

    private static bool TryAngle(
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
               Math.Abs(value) >= MinimumAngle &&
               Math.Abs(value) <= 360.0;
    }
}
