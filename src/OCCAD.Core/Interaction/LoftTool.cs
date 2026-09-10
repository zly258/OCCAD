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

    // Loft is a continuous selection tool. Pointer clicks keep adding sections;
    // Enter/right-click finishes through CanFinish instead of treating the next
    // pointer click as a commit request after the second section.
    public override bool CanCommitCurrentStage => false;

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
            if (CanFinish)
                return Context.Workspace.Tools.FinishCurrent();

            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            RefreshHoverPreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        if (Context.Workspace.Preselection.Current is not { Entity: var entity } ||
            !IsSelectableSection(entity))
            return false;

        _sections.Add(entity);
        Context.Workspace.Preselection.Clear();
        RefreshCommittedPreview();

        SetStageLocalized(
            _sections.Count,
            "Cad.Prompt.loft.NextSection",
            "Loft: select next cross section [Enter commit, Backspace undo, Esc cancel]");
        return true;
    }

    public bool TryAcceptPoint(OcctPoint3d point) => false;

    protected override bool CanStepBackCore => _sections.Count > 0;

    protected override bool OnStepBack()
    {
        if (_sections.Count == 0)
            return false;

        _sections.RemoveAt(_sections.Count - 1);
        Context.Workspace.Preselection.Clear();
        RefreshCommittedPreview();

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

        RefreshCommittedPreview();
        NotifyUpdated();
        return true;
    }

    private void RefreshHoverPreview()
    {
        if (Context.Workspace.Preselection.Current is not { Entity: var candidate } ||
            !IsSelectableSection(candidate) ||
            _sections.Count == 0)
        {
            RefreshCommittedPreview();
            return;
        }

        var previewSections = _sections
            .Append(candidate)
            .ToArray();
        if (previewSections.Length < 2)
        {
            Context.Preview.Clear();
            return;
        }

        ShowPreview(previewSections);
    }

    private void RefreshCommittedPreview()
    {
        if (_sections.Count < 2)
        {
            Context.Preview.Clear();
            return;
        }

        ShowPreview(_sections);
    }

    private void ShowPreview(IEnumerable<CadEntity> sections)
    {
        try
        {
            var loft = CreateLoft(sections);
            Context.Preview.Show(loft);
        }
        catch (Exception exception) when (IsRecoverablePreviewFailure(exception))
        {
            Context.Preview.Clear();
        }
    }

    private bool CommitLoft()
    {
        if (_sections.Count < 2)
            return false;

        var loft = CreateLoft(_sections);
        loft.BindSources(_sections);
        var engine = Context.Engine;
        Context.Preview.Clear();
        try
        {
            CadTransaction.ApplyCreatedEntity(
                Context.Workspace,
                loft,
                "Loft",
                Context.Workspace.Tools.CompleteCurrent);
        }
        catch
        {
            if (IsActive)
                RefreshCommittedPreview();
            throw;
        }

        engine.Redraw();
        return true;
    }

    private CadLoftEntity CreateLoft(IEnumerable<CadEntity> sections)
    {
        var values = sections.ToArray();
        if (values.Length < 2)
            throw new InvalidOperationException("Loft requires at least two sections.");

        var loft = new CadLoftEntity(values, _makeSolid, _ruled)
        {
            Layer = Context.Workspace.Layers.Current.Name
        };
        return loft;
    }

    private bool IsSelectableSection(CadEntity entity) =>
        CadPlanarProfileGeometry.IsWireProfile(entity) &&
        !_sections.Contains(entity);

    private static bool IsRecoverablePreviewFailure(Exception exception) =>
        exception is ArgumentException or
        InvalidOperationException or
        ArithmeticException or
        OcctException;

    protected override void OnCanceled() => ResetLoftState();

    protected override void OnDeactivated() => ResetLoftState();

    private void ResetLoftState()
    {
        Context.Preview.Clear();
        _sections.Clear();
        Context.Workspace.Preselection.Clear();
    }
}
