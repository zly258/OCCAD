using OcctNet;

namespace OCCAD;

public sealed class SweepTool : CadTool, ICadPointInputTool
{
    private CadEntity? _profile;
    private CadEntity? _path;

    public override string Id => "sweep";
    public override string DisplayName => "Sweep";

    public override CadToolInputKind InputKind => CadToolInputKind.Selection;

    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with { PreselectionEnabled = true };

    protected override void OnActivated()
    {
        _profile = null;
        _path = null;
        SetSelectionFilter(
            new CadEntityFilter(
                "sweep.profile",
                CadPlanarProfileGeometry.IsSupported));
        Context.Selection.Clear();
        Context.Workspace.Preselection.Clear();
        SetStageLocalized(
            0,
            "Cad.Prompt.sweep.Profile",
            "Sweep: select planar profile [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        if (Context.Workspace.Preselection.Current is not { Entity: var entity })
            return false;

        if (_profile is null)
        {
            if (!CadPlanarProfileGeometry.IsSupported(entity))
                return false;

            _profile = entity;
            SetSelectionFilter(
                new CadEntityFilter(
                    "sweep.path",
                    CadSweepEntity.IsPath));
            Context.Workspace.Preselection.Clear();
            SetStageLocalized(
                1,
                "Cad.Prompt.sweep.Path",
                "Sweep: select sweep path [Backspace undo, Esc cancel]");
            return true;
        }

        if (_path is null)
        {
            if (!CadSweepEntity.IsPath(entity))
                return false;

            _path = entity;
            return CommitSweep();
        }

        return false;
    }

    public bool TryAcceptPoint(OcctPoint3d point) => false;

    private bool CommitSweep()
    {
        if (_profile is null || _path is null)
            return false;

        try
        {
            var sweep = new CadSweepEntity(_profile, _path);
            sweep.BindSources(_profile, _path);
            Context.Preview.Clear();
            Context.Workspace.AddGeneratedEntities([sweep], "Sweep");
            Context.Workspace.Tools.CompleteCurrent();
            return true;
        }
        catch
        {
            Context.Preview.Clear();
            return false;
        }
    }

    protected override bool CanStepBackCore => _profile is not null;

    protected override bool OnStepBack()
    {
        if (_profile is not null)
        {
            _profile = null;
            _path = null;
            Context.Preview.Clear();
            SetSelectionFilter(
                new CadEntityFilter(
                    "sweep.profile",
                    CadPlanarProfileGeometry.IsSupported));
            Context.Workspace.Preselection.Clear();
            SetStageLocalized(
                0,
                "Cad.Prompt.sweep.Profile",
                "Sweep: select planar profile [Esc cancel]");
            return true;
        }

        return false;
    }

    protected override void OnCanceled()
    {
        Context.Preview.Clear();
        _profile = null;
        _path = null;
        Context.Workspace.Preselection.Clear();
    }

    protected override void OnDeactivated()
    {
        Context.Preview.Clear();
        _profile = null;
        _path = null;
        Context.Workspace.Preselection.Clear();
    }
}
