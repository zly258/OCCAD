using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class FilletTool : CadTwoCurveCornerToolBase
{
    private const double MinimumRadius = 1e-6;
    private double _radius = 10.0;

    public override string Id => "fillet";
    public override string DisplayName => "Fillet";

    protected override bool IsSupportedEntity(CadEntity entity) =>
        entity is CadLineEntity or
        CadArcEntity or
        CadPolylineEntity or
        CadPathEntity;

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (Context.Workspace.Preselection.Current is
            {
                Entity: var path,
                Point: var hit
            } &&
            path is CadPolylineEntity or CadPathEntity)
        {
            if (input.Kind == OcctPointerInputKind.Moved)
            {
                if (CadPathFilletGeometry.TryFillet(
                        path.CreateWorldGeometrySnapshot(),
                        hit,
                        _radius,
                        Context.WorkPlane,
                        out var preview))
                    Context.Preview.Show(preview);
                else
                    Context.Preview.Clear();
                return true;
            }

            if (input.Kind == OcctPointerInputKind.Pressed &&
                input.Button == OcctPointerButton.Left)
            {
                if (!CadPathFilletGeometry.TryFillet(
                        path.CreateWorldGeometrySnapshot(),
                        hit,
                        _radius,
                        Context.WorkPlane,
                        out var replacement))
                {
                    Context.Preview.Clear();
                    SetPromptLocalized(
                        "Cad.Prompt.fillet.Invalid",
                        "Fillet: selected vertex and radius do not define a valid fillet.");
                    return true;
                }

                Context.Preview.Clear();
                Context.Workspace.ReplaceEntities(
                    [path],
                    [replacement],
                    "Fillet");
                Context.Workspace.Tools.CompleteCurrent();
                return true;
            }
        }

        return base.HandlePointer(input);
    }

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Fillet",
            [
                new CadDoubleToolParameterDescriptor(
                    "Radius",
                    "Radius",
                    _radius,
                    MinimumRadius,
                    1e12)
            ]);

    protected override bool TryBuild(
        CadEntity second,
        OcctPoint3d secondPick,
        out CadEntity[] replacements)
    {
        var first =
            FirstEntity.CreateWorldGeometrySnapshot();
        var next =
            second.CreateWorldGeometrySnapshot();

        if (first is CadLineEntity firstLine &&
            next is CadLineEntity secondLine)
        {
            return CadLineCornerGeometry.TryFillet(
                firstLine,
                FirstPick,
                secondLine,
                secondPick,
                _radius,
                Context.WorkPlane,
                out replacements);
        }

        if (first is CadLineEntity line &&
            next is CadArcEntity arc)
        {
            return CadLineArcFilletGeometry.TryFillet(
                line,
                FirstPick,
                arc,
                secondPick,
                _radius,
                Context.WorkPlane,
                out replacements);
        }

        if (first is CadArcEntity firstArc &&
            next is CadLineEntity arcLine)
        {
            return CadLineArcFilletGeometry.TryFillet(
                arcLine,
                secondPick,
                firstArc,
                FirstPick,
                _radius,
                Context.WorkPlane,
                out replacements);
        }

        if (first is CadArcEntity firstArcEntity &&
            next is CadArcEntity secondArcEntity)
        {
            return CadArcArcFilletGeometry.TryFillet(
                firstArcEntity,
                FirstPick,
                secondArcEntity,
                secondPick,
                _radius,
                Context.WorkPlane,
                out replacements);
        }

        replacements = [];
        return false;
    }

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!id.Equals("Radius", StringComparison.OrdinalIgnoreCase) ||
            !TryPositive(value, out var radius))
            return false;

        _radius = radius;
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
               value >= MinimumRadius;
    }
}
