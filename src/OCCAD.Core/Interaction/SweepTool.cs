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

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            RefreshHoverPreview();
            return _profile is not null;
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
            _path = null;
            SetSelectionFilter(
                new CadEntityFilter(
                    "sweep.path",
                    candidate =>
                        !ReferenceEquals(candidate, _profile) &&
                        CadSweepEntity.IsPath(candidate)));
            Context.Workspace.Preselection.Clear();
            Context.Preview.Clear();
            SetStageLocalized(
                1,
                "Cad.Prompt.sweep.Path",
                "Sweep: select sweep path [Backspace undo, Esc cancel]");
            return true;
        }

        if (!IsValidPath(entity))
            return false;

        _path = entity;
        return CommitSweep();
    }

    public bool TryAcceptPoint(OcctPoint3d point) => false;

    protected override bool CanStepBackCore => _profile is not null;

    protected override bool OnStepBack()
    {
        if (_profile is null)
            return false;

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

    protected override void OnCanceled() => ResetSweepState();

    protected override void OnDeactivated() => ResetSweepState();

    private void RefreshHoverPreview()
    {
        if (_profile is null ||
            Context.Workspace.Preselection.Current is not { Entity: var candidate } ||
            !IsValidPath(candidate))
        {
            Context.Preview.Clear();
            return;
        }

        var sweep = CreateSweep(candidate);
        Context.Preview.Show(sweep);
    }

    private bool CommitSweep()
    {
        if (_profile is null || _path is null || !IsValidPath(_path))
            return false;

        var sweep = CreateSweep(_path);
        var engine = Context.Engine;
        Context.Preview.Clear();
        try
        {
            CadTransaction.ApplyCreatedEntity(
                Context.Workspace,
                sweep,
                "Sweep",
                Context.Workspace.Tools.CompleteCurrent);
        }
        catch
        {
            if (IsActive)
                RefreshHoverPreview();
            throw;
        }

        engine.Redraw();
        return true;
    }

    private CadSweepEntity CreateSweep(CadEntity path)
    {
        if (_profile is null)
            throw new InvalidOperationException("Sweep profile is not selected.");
        if (!IsValidPath(path))
            throw new InvalidOperationException("Sweep path is not valid.");

        var sweep = new CadSweepEntity(_profile, path)
        {
            Layer = Context.Workspace.Layers.Current.Name
        };
        sweep.BindSources(_profile, path);
        return sweep;
    }

    private bool IsValidPath(CadEntity entity) =>
        _profile is not null &&
        !ReferenceEquals(entity, _profile) &&
        CadSweepEntity.IsPath(entity);

    private void ResetSweepState()
    {
        Context.Preview.Clear();
        _profile = null;
        _path = null;
        Context.Workspace.Preselection.Clear();
    }
}
