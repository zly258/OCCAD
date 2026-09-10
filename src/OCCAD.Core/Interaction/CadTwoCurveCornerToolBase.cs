using System.Drawing;
using OcctNet;

namespace OCCAD;

public abstract class CadTwoCurveCornerToolBase : CadTool
{
    private CadEntity? _first;
    private OcctPoint3d _firstPick;
    private bool _invalidSecondPrompt;

    public override CadToolInputKind InputKind =>
        CadToolInputKind.Selection;
    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with { PreselectionEnabled = true };

    protected CadEntity FirstEntity =>
        _first ?? throw new InvalidOperationException("First entity is not selected.");

    protected CadLineEntity FirstLine =>
        FirstEntity as CadLineEntity ??
        throw new InvalidOperationException("First entity is not a line.");

    protected OcctPoint3d FirstPick => _firstPick;
    protected bool HasFirstEntity => _first is not null;

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                $"{Id}.curves",
                IsSupportedEntity));
        Context.Selection.Clear();
        Context.Workspace.Preselection.Clear();
        _first = null;
        _invalidSecondPrompt = false;
        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.First",
            $"{DisplayName}: select first curve and keep side [Esc cancel]");
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

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            RefreshPreview();
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        if (Context.Workspace.Preselection.Current is not
            {
                Entity: var entity,
                Point: var hit
            } ||
            !IsSupportedEntity(entity))
            return true;

        if (_first is null)
        {
            _first = entity;
            _firstPick = hit;
            _invalidSecondPrompt = false;
            Context.Workspace.Preselection.Clear();
            ShowFirstEntityPreview();
            SetStageLocalized(
                1,
                $"Cad.Prompt.{Id}.Second",
                $"{DisplayName}: select second curve and keep side [Backspace undo, Esc cancel]");
            return true;
        }

        if (ReferenceEquals(_first, entity))
            return true;

        if (!TryBuild(
                entity,
                hit,
                out var replacements))
        {
            ClearReplacementPreview();
            _invalidSecondPrompt = true;
            SetPromptLocalized(
                $"Cad.Prompt.{Id}.Invalid",
                $"{DisplayName}: selected curves and parameters do not define a valid corner.");
            return true;
        }

        _invalidSecondPrompt = false;
        CommitReplacementPreview(
            () => Context.Workspace.ReplaceEntities(
                [_first, entity],
                replacements,
                DisplayName));
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    protected override bool CanStepBackCore =>
        _first is not null;

    protected override bool OnStepBack()
    {
        if (_first is null)
            return false;

        _first = null;
        _invalidSecondPrompt = false;
        Context.Workspace.Preselection.Clear();
        ClearReplacementPreview();
        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.First",
            $"{DisplayName}: select first curve and keep side [Esc cancel]");
        return true;
    }

    protected override void OnCanceled()
    {
        _first = null;
        _invalidSecondPrompt = false;
        Context.Workspace.Preselection.Clear();
    }

    protected void RefreshPreview()
    {
        if (_first is null)
        {
            ClearReplacementPreview();
            return;
        }

        if (Context.Workspace.Preselection.Current is not
            {
                Entity: var second,
                Point: var hit
            } ||
            !IsSupportedEntity(second) ||
            ReferenceEquals(_first, second) ||
            !TryBuild(second, hit, out var replacements))
        {
            ClearReplacementPreview();
            ShowFirstEntityPreview();
            return;
        }

        if (_invalidSecondPrompt)
        {
            _invalidSecondPrompt = false;
            SetPromptLocalized(
                $"Cad.Prompt.{Id}.Second",
                $"{DisplayName}: select second curve and keep side [Backspace undo, Esc cancel]");
        }

        ShowReplacementPreview([_first, second], replacements);
    }

    private void ShowFirstEntityPreview()
    {
        if (_first is null)
            return;

        var preview = _first.Duplicate();
        var appearance =
            Context.Document.ResolveAppearance(_first);
        preview.ColorByLayer = false;
        preview.LineWidthByLayer = false;
        preview.Color = Color.Gold;
        preview.LineWidth =
            Math.Max(2.0, appearance.LineWidth + 1.0);
        preview.Transparency = 0.0;
        Context.Preview.Show(preview);
    }

    protected virtual bool IsSupportedEntity(CadEntity entity) =>
        entity is CadLineEntity;

    protected abstract bool TryBuild(
        CadEntity second,
        OcctPoint3d secondPick,
        out CadEntity[] replacements);
}
