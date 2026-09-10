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
        entity is CadLineEntity or CadArcEntity;

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
        if (FirstEntity is CadLineEntity firstLine &&
            second is CadLineEntity secondLine)
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

        if (FirstEntity is CadLineEntity line &&
            second is CadArcEntity arc)
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

        if (FirstEntity is CadArcEntity firstArc &&
            second is CadLineEntity secondLine)
        {
            return CadLineArcFilletGeometry.TryFillet(
                secondLine,
                secondPick,
                firstArc,
                FirstPick,
                _radius,
                Context.WorkPlane,
                out replacements);
        }

        if (FirstEntity is CadArcEntity firstArcEntity &&
            second is CadArcEntity secondArcEntity)
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
