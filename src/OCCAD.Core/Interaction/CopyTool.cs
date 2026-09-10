using System.ComponentModel;
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
        if (!id.Equals("Count", StringComparison.OrdinalIgnoreCase))
            return false;

        var descriptor = CadValueDescriptor.Create(
            "Count",
            typeof(int),
            minimum: 1,
            maximum: 100,
            semantic: CadValueSemantic.Integer);
        if (!CadValueTextConverter.TryParse(
                descriptor,
                typeof(int),
                TypeDescriptor.GetConverter(typeof(int)),
                value,
                out var parsed) ||
            parsed is not int count)
            return false;

        _count = count;
        RefreshTranslatedPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ShowTranslatedPreview(
        OcctVector3d displacement)
    {
        Context.Preview.Show(
            BuildCopies(displacement));
    }

    protected override void Commit(
        OcctVector3d displacement,
        Action complete)
    {
        var copies = BuildCopies(displacement);
        CadTransaction.ApplyCreatedEntities(
            Context.Workspace,
            copies,
            copies.Length == 1
                ? "Copy"
                : $"Copy {copies.Length}",
            complete);
    }

    protected override void ResetTransformState()
    {
        base.ResetTransformState();
        _count = 1;
    }

    private CadEntity[] BuildCopies(
        OcctVector3d displacement)
    {
        var copies = new List<CadEntity>(
            Entities.Count * _count);

        for (var index = 1; index <= _count; index++)
        {
            var step = displacement * index;
            foreach (var source in Entities)
            {
                var copy = source.Duplicate();
                copy.TranslatePlacement(step);
                copies.Add(copy);
            }
        }

        return copies.ToArray();
    }
}
