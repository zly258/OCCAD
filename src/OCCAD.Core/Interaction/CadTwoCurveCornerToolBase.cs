using OcctNet;

namespace OCCAD;

public abstract class CadTwoCurveCornerToolBase : CadTool
{
    private CadEntity? _first;
    private OcctPoint3d _firstPick;

    public override bool AllowsPreselectionDuringDrawing => true;

    protected CadEntity FirstEntity =>
        _first ?? throw new InvalidOperationException("First entity is not selected.");

    protected CadLineEntity FirstLine =>
        FirstEntity as CadLineEntity ??
        throw new InvalidOperationException("First entity is not a line.");

    protected OcctPoint3d FirstPick => _firstPick;

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                $"{Id}.curves",
                IsSupportedEntity));
        Context.Selection.Clear();
        _first = null;
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
            Context.Preview.Clear();
            SetPromptLocalized(
                $"Cad.Prompt.{Id}.Invalid",
                $"{DisplayName}: selected curves and parameters do not define a valid corner.");
            return true;
        }

        Context.Preview.Clear();
        Context.Workspace.ReplaceEntities(
            [_first, entity],
            replacements,
            DisplayName);
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
        Context.Preview.Clear();
        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.First",
            $"{DisplayName}: select first curve and keep side [Esc cancel]");
        return true;
    }

    protected override void OnCanceled()
    {
        _first = null;
    }

    protected void RefreshPreview()
    {
        if (_first is null ||
            Context.Workspace.Preselection.Current is not
            {
                Entity: var second,
                Point: var hit
            } ||
            !IsSupportedEntity(second) ||
            ReferenceEquals(_first, second) ||
            !TryBuild(second, hit, out var replacements))
        {
            Context.Preview.Clear();
            return;
        }

        Context.Preview.Show(replacements);
    }

    protected virtual bool IsSupportedEntity(CadEntity entity) =>
        entity is CadLineEntity;

    protected abstract bool TryBuild(
        CadEntity second,
        OcctPoint3d secondPick,
        out CadEntity[] replacements);
}
