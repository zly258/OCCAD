using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class ShellTool : CadTool
{
    private CadEntity? _source;
    private int _faceIndex = -1;
    private double _thickness = 2.0;

    public override string Id => "shell";
    public override string DisplayName => "Shell";
    public override CadToolInputKind InputKind =>
        _source is null
            ? CadToolInputKind.Selection
            : CadToolInputKind.Confirmation;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Shell",
            [
                new CadDoubleToolParameterDescriptor(
                    "Thickness",
                    "Thickness",
                    _thickness,
                    -1e12,
                    1e12)
            ]);

    protected override void OnActivated()
    {
        var items = Context.Workspace.Subobjects.Selected;
        if (items.Count != 1 ||
            items[0].SubshapeType != OcctShapeType.Face ||
            items[0].SubshapeIndex < 0 ||
            !CadSolidFeatureGeometry.IsSolid(items[0].Entity))
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.shell.Select",
                "Shell: select one face on a solid before starting the command [Esc cancel]");
            return;
        }

        _source = items[0].Entity;
        _faceIndex = items[0].SubshapeIndex;
        SetStageLocalized(
            0,
            "Cad.Prompt.shell.Ready",
            "Shell: set thickness, then click or Finish [Esc cancel]");
        RefreshPreview();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
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

        return CommitShell();
    }

    protected override bool CanFinishCore =>
        _source is not null &&
        _faceIndex >= 0;

    protected override bool OnFinish() =>
        CommitShell();

    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals(
                "Thickness",
                StringComparison.OrdinalIgnoreCase) ||
            !TryNonZero(value, out var thickness))
            return false;

        _thickness = thickness;
        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void OnDeactivated()
    {
        _source = null;
        _faceIndex = -1;
        _thickness = 2.0;
    }

    private bool CommitShell()
    {
        if (!TryCreate(out var feature))
            return false;

        var source = _source!;
        Context.Preview.Clear();
        Context.Workspace.ReplaceEntities(
            [source],
            [feature],
            "Shell");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void RefreshPreview()
    {
        if (TryCreate(out var feature))
            Context.Preview.Show(feature);
        else
            Context.Preview.Clear();
    }

    private bool TryCreate(out CadShellEntity feature)
    {
        feature = null!;
        if (_source is null || _faceIndex < 0)
            return false;

        feature = new CadShellEntity(
            _source,
            _faceIndex,
            _thickness);
        return true;
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
