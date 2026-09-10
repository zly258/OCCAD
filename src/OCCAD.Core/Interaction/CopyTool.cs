using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class CopyTool : CadTranslateToolBase
{
    private int _count = 1;

    public override string Id => "copy";
    public override string DisplayName => "Copy";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Copy",
            [
                new CadIntegerToolParameterDescriptor(
                    "Count",
                    "Count",
                    _count,
                    1,
                    100)
            ]);

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!id.Equals(
                "Count",
                StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var count) &&
            !int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out count) ||
            count is < 1 or > 100)
            return false;

        _count = count;
        RefreshTranslatedPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ShowTranslatedPreview(
        OcctVector3d displacement)
    {
        var previews = new List<CadEntity>(
            Entities.Count * _count);

        for (var index = 1; index <= _count; index++)
        {
            var step = displacement * index;
            foreach (var source in Entities)
            {
                var copy = source.Duplicate();
                copy.TranslatePlacement(step);
                previews.Add(copy);
            }
        }

        Context.Preview.Show(previews);
    }

    protected override void Commit(
        OcctVector3d displacement) =>
        Context.Workspace.CopyEntities(
            Entities,
            displacement,
            _count);

    protected override void ResetTransformState()
    {
        base.ResetTransformState();
        _count = 1;
    }
}
