using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class ChamferTool : CadTwoCurveCornerToolBase
{
    private const double MinimumDistance = 1e-6;
    private double _firstDistance = 10.0;
    private double _secondDistance = 10.0;

    public override string Id => "chamfer";
    public override string DisplayName => "Chamfer";

    protected override void OnActivated()
    {
        base.OnActivated();
        SetSelectionFilter(
            new CadSelectionFilter(
                "chamfer.curves",
                static entity =>
                    entity is CadLineEntity or
                    CadPolylineEntity));
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (Context.Workspace.Preselection.Current is
            {
                Entity: CadPolylineEntity polyline,
                Point: var hit
            })
        {
            if (input.Kind == OcctPointerInputKind.Moved)
            {
                if (CadPolylineChamferGeometry.TryChamfer(
                        polyline,
                        hit,
                        _firstDistance,
                        _secondDistance,
                        Context.WorkPlane,
                        out var preview))
                    Context.Preview.Show(preview);
                else
                    Context.Preview.Clear();
                return true;
            }

            if (input.Kind != OcctPointerInputKind.Pressed ||
                input.Button != OcctPointerButton.Left)
                return true;
            if (!CadPolylineChamferGeometry.TryChamfer(
                    polyline,
                    hit,
                    _firstDistance,
                    _secondDistance,
                    Context.WorkPlane,
                    out var replacement))
            {
                Context.Preview.Clear();
                SetPromptLocalized(
                    "Cad.Prompt.chamfer.Invalid",
                    "Chamfer: selected geometry and distances do not define a valid corner.");
                return true;
            }

            Context.Preview.Clear();
            Context.Workspace.ReplaceEntities(
                [polyline],
                [replacement],
                "Chamfer");
            Context.Workspace.Tools.CompleteCurrent();
            return true;
        }

        return base.HandlePointer(input);
    }

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Chamfer",
            [
                new CadDoubleToolParameterDescriptor(
                    "Distance1",
                    "Distance 1",
                    _firstDistance,
                    MinimumDistance,
                    1e12),
                new CadDoubleToolParameterDescriptor(
                    "Distance2",
                    "Distance 2",
                    _secondDistance,
                    MinimumDistance,
                    1e12)
            ]);

    protected override bool TryBuild(
        CadEntity second,
        OcctPoint3d secondPick,
        out CadEntity[] replacements) =>
        second is CadLineEntity line &&
        CadLineCornerGeometry.TryChamfer(
            FirstLine,
            FirstPick,
            line,
            secondPick,
            _firstDistance,
            _secondDistance,
            Context.WorkPlane,
            out replacements);

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!TryPositive(value, out var distance))
            return false;

        if (id.Equals("Distance1", StringComparison.OrdinalIgnoreCase))
            _firstDistance = distance;
        else if (id.Equals("Distance2", StringComparison.OrdinalIgnoreCase))
            _secondDistance = distance;
        else
            return false;

        RefreshPreview();
        NotifyUpdated();
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
               value >= MinimumDistance;
    }
}
