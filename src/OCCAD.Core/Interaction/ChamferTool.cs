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
                    CadPolylineEntity or
                    CadPathEntity));
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (!HasFirstEntity &&
            Context.Workspace.Preselection.Current is
            {
                Entity: var chain,
                Point: var hit
            } &&
            chain is CadPolylineEntity or CadPathEntity)
        {
            if (input.Kind == OcctPointerInputKind.Moved)
            {
                if (TryChamferChain(
                        chain,
                        hit,
                        out var preview))
                    ShowReplacementPreview([chain], [preview]);
                else
                    ClearReplacementPreview();
                return true;
            }

            if (input.Kind != OcctPointerInputKind.Pressed ||
                input.Button != OcctPointerButton.Left)
                return true;

            if (!TryChamferChain(
                    chain,
                    hit,
                    out var replacement))
            {
                ClearReplacementPreview();
                SetPromptLocalized(
                    "Cad.Prompt.chamfer.Invalid",
                    "Chamfer: selected geometry and distances do not define a valid corner.");
                return true;
            }

            ClearReplacementPreview();
            Context.Workspace.ReplaceEntities(
                [chain],
                [replacement],
                "Chamfer");
            Context.Workspace.Tools.CompleteCurrent();
            return true;
        }

        return base.HandlePointer(input);
    }

    private bool TryChamferChain(
        CadEntity source,
        OcctPoint3d hitPoint,
        out CadEntity replacement)
    {
        replacement = null!;

        var working =
            source.CreateWorldGeometrySnapshot();

        switch (working)
        {
            case CadPolylineEntity polyline:
                if (!CadPolylineChamferGeometry.TryChamfer(
                        polyline,
                        hitPoint,
                        _firstDistance,
                        _secondDistance,
                        Context.WorkPlane,
                        out var polylineReplacement))
                    return false;
                replacement = polylineReplacement;
                return true;

            case CadPathEntity path:
                if (!CadPathChamferGeometry.TryChamfer(
                        path,
                        hitPoint,
                        _firstDistance,
                        _secondDistance,
                        Context.WorkPlane,
                        out var pathReplacement))
                    return false;
                replacement = pathReplacement;
                return true;

            default:
                return false;
        }
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
        out CadEntity[] replacements)
    {
        replacements = [];
        var first =
            FirstLine.CreateWorldGeometrySnapshot<CadLineEntity>();
        var next =
            second.CreateWorldGeometrySnapshot();
        if (next is not CadLineEntity line)
            return false;

        return CadLineCornerGeometry.TryChamfer(
            first,
            FirstPick,
            line,
            secondPick,
            _firstDistance,
            _secondDistance,
            Context.WorkPlane,
            out replacements);
    }

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

        if (!HasFirstEntity &&
            Context.Workspace.Preselection.Current is
            {
                Entity: var chain,
                Point: var hit
            } &&
            chain is CadPolylineEntity or CadPathEntity)
        {
            if (TryChamferChain(
                    chain,
                    hit,
                    out var preview))
                ShowReplacementPreview([chain], [preview]);
            else
                ClearReplacementPreview();
        }
        else
        {
            RefreshPreview();
        }
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
