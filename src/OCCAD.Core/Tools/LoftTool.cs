using OcctNet;

namespace OCCAD;

public sealed class LoftTool : CadTool, ICadPointInputTool
{
    private readonly List<CadEntity> _sections = [];
    private bool _ruled;
    private bool _makeSolid = true;

    public override string Id => "loft";
    public override string DisplayName => "Loft";

    public override CadToolInputKind InputKind => CadToolInputKind.Selection;

    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with { PreselectionEnabled = true };

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Loft",
            [
                new CadBooleanToolParameterDescriptor(
                    "Solid",
                    "Solid",
                    _makeSolid),
                new CadBooleanToolParameterDescriptor(
                    "Ruled",
                    "Ruled",
                    _ruled)
            ]);

    public override bool CanCommitCurrentStage => _sections.Count >= 2;

    protected override void OnActivated()
    {
        _sections.Clear();
        SetSelectionFilter(
            new CadEntityFilter(
                "loft.sections",
                CadPlanarProfileGeometry.IsWireProfile));
        Context.Selection.Clear();
        Context.Workspace.Preselection.Clear();
        SetStageLocalized(
            0,
            "Cad.Prompt.loft.FirstSection",
            "Loft: select first cross section [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Right)
        {
            if (_sections.Count >= 2)
                return CommitLoft();

            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        if (Context.Workspace.Preselection.Current is not { Entity: var entity } ||
            !CadPlanarProfileGeometry.IsWireProfile(entity))
            return false;

        if (_sections.Contains(entity))
            return true;

        _sections.Add(entity);
        Context.Workspace.Preselection.Clear();

        RefreshPreview();

        SetStageLocalized(
            _sections.Count,
            "Cad.Prompt.loft.NextSection",
            "Loft: select next cross section [Enter commit, Backspace undo, Esc cancel]");

        return true;
    }

    public override bool HandleKey(OcctKeyInputEventArgs input)
    {
        if (input.Kind == OcctKeyInputKind.Pressed && input.Key == OcctKey.Enter)
        {
            if (_sections.Count >= 2)
                return CommitLoft();
        }

        return base.HandleKey(input);
    }

    public bool TryAcceptPoint(OcctPoint3d point) => false;

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        _sections.Count >= 2 && CommitLoft();

    protected override bool CanStepBackCore => _sections.Count > 0;

    protected override bool OnStepBack()
    {
        if (_sections.Count == 0)
            return false;

        _sections.RemoveAt(_sections.Count - 1);
        Context.Workspace.Preselection.Clear();
        RefreshPreview();

        if (_sections.Count == 0)
        {
            SetStageLocalized(
                0,
                "Cad.Prompt.loft.FirstSection",
                "Loft: select first cross section [Esc cancel]");
        }
        else
        {
            SetStageLocalized(
                _sections.Count,
                "Cad.Prompt.loft.NextSection",
                "Loft: select next cross section [Enter commit, Backspace undo, Esc cancel]");
        }

        return true;
    }

    protected override bool CanFinishCore => _sections.Count >= 2;

    protected override bool OnFinish() => CommitLoft();

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Solid", StringComparison.OrdinalIgnoreCase))
        {
            if (!bool.TryParse(value, out var solid))
                return false;
            _makeSolid = solid;
        }
        else if (id.Equals("Ruled", StringComparison.OrdinalIgnoreCase))
        {
            if (!bool.TryParse(value, out var ruled))
                return false;
            _ruled = ruled;
        }
        else
        {
            return false;
        }

        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    private void RefreshPreview()
    {
        if (_sections.Count < 2)
        {
            Context.Preview.Clear();
            return;
        }

        try
        {
            var loft = new CadLoftEntity(_sections, _makeSolid, _ruled);
            Context.Preview.Show(loft);
        }
        catch
        {
            Context.Preview.Clear();
        }
    }

    private bool CommitLoft()
    {
        if (_sections.Count < 2)
            return false;

        try
        {
            var loft = new CadLoftEntity(_sections, _makeSolid, _ruled);
            loft.BindSources(_sections);
            Context.Preview.Clear();
            Context.Workspace.AddGeneratedEntities([loft], "Loft");
            Context.Workspace.Tools.CompleteCurrent();
            return true;
        }
        catch
        {
            Context.Preview.Clear();
            return false;
        }
    }

    protected override void OnCanceled()
    {
        Context.Preview.Clear();
        _sections.Clear();
        Context.Workspace.Preselection.Clear();
    }

    protected override void OnDeactivated()
    {
        Context.Preview.Clear();
        _sections.Clear();
        Context.Workspace.Preselection.Clear();
    }
}
