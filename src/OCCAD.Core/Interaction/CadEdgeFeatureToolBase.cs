using System.Globalization;
using OcctNet;

namespace OCCAD;

public abstract class CadEdgeFeatureToolBase : CadTool
{
    private CadEntity? _source;
    private int[] _edgeIndices = [];
    private double _value = 5.0;

    protected abstract string ValueId { get; }
    protected abstract string ValueLabel { get; }
    protected abstract CadEntity CreateFeature(
        CadEntity source,
        IReadOnlyList<int> edgeIndices,
        double value);

    protected double Value => _value;

    public override CadToolInputKind InputKind =>
        _source is null
            ? CadToolInputKind.Selection
            : CadToolInputKind.Confirmation;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            DisplayName,
            [
                new CadDoubleToolParameterDescriptor(
                    ValueId,
                    ValueLabel,
                    _value,
                    1e-6,
                    1e12)
            ]);

    protected override void OnActivated()
    {
        var edges =
            CadEdgeFeatureGeometry.CurrentEdges(
                Context.Workspace);

        if (edges.Count == 0)
        {
            _source = null;
            _edgeIndices = [];
            SetStageLocalized(
                0,
                $"Cad.Prompt.{Id}.Select",
                $"{DisplayName}: select one or more edges on the same solid before starting the command [Esc cancel]");
            return;
        }

        _source = edges[0].Entity;
        _edgeIndices = edges
            .Select(static edge => edge.SubshapeIndex)
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();

        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.Ready",
            $"{DisplayName}: set value, then click or Finish [Esc cancel]");
        RefreshPreview();
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (_source is null)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            RefreshPreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return CommitFeature();
    }

    protected override bool CanFinishCore =>
        _source is not null &&
        _edgeIndices.Length > 0;

    protected override bool OnFinish() =>
        CommitFeature();

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!id.Equals(
                ValueId,
                StringComparison.OrdinalIgnoreCase) ||
            !TryPositive(value, out var parsed))
            return false;

        _value = parsed;
        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void OnDeactivated()
    {
        _source = null;
        _edgeIndices = [];
        _value = 5.0;
    }

    private bool CommitFeature()
    {
        if (!TryCreate(out var feature))
            return false;

        var source = _source!;
        CommitReplacementPreview(
            () => Context.Workspace.ReplaceEntities(
                [source],
                [feature],
                DisplayName));
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void RefreshPreview()
    {
        if (TryCreate(out var feature))
            ShowReplacementPreview(
                [_source!],
                [feature]);
        else
            ClearReplacementPreview();
    }

    private bool TryCreate(out CadEntity feature)
    {
        feature = null!;
        if (_source is null ||
            _edgeIndices.Length == 0)
            return false;

        feature = CreateFeature(
            _source,
            _edgeIndices,
            _value);
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
               value >= 1e-6;
    }
}
