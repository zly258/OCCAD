using OcctNet;

namespace OCCAD;

public abstract class CadSelectionTransformToolBase : CadTool
{
    private CadEntity[] _entities = [];

    protected IReadOnlyList<CadEntity> Entities => _entities;

    public override bool CanCommitCurrentStage => State == CadToolState.WaitForSelect
        ? IsActive && CurrentSelection().Length > 0
        : base.CanCommitCurrentStage;

    internal override bool CommitCurrentStage()
    {
        if (State != CadToolState.WaitForSelect) return base.CommitCurrentStage();
        var selected = CurrentSelection();
        if (!IsActive || selected.Length == 0) return false;
        BeginTransform(selected);
        return true;
    }

    protected override void OnActivated()
    {
        ResetTransformState();
        _entities = [];
        Context.Selection.Changed += SelectionChanged;

        var selected = CurrentSelection();
        if (selected.Length == 0)
            BeginSelection();
        else
            BeginTransform(selected);
    }

    public override bool HandleKey(OcctKeyInputEventArgs input)
    {
        if (State != CadToolState.WaitForSelect ||
            input.Kind != OcctKeyInputKind.Pressed ||
            input.Key != OcctKey.Enter)
            return false;

        if (!CommitCurrentStage()) UpdateSelectionPrompt(0);
        return true;
    }

    protected bool CancelOnRightClick(OcctPointerInputEventArgs input)
    {
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Right)
            return false;

        Context.Workspace.Tools.CancelCurrent();
        return true;
    }

    protected override void OnCanceled() =>
        ResetTransformState();

    protected override void OnDeactivated()
    {
        Context.Selection.Changed -= SelectionChanged;
        _entities = [];
        ResetTransformState();
    }

    protected abstract void OnTransformStarted();

    protected virtual void ResetTransformState()
    {
    }

    protected void ShowEntityPreview(Action<CadEntity> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var previews = _entities
            .Select(static entity => entity.Duplicate())
            .ToArray();
        foreach (var entity in previews)
            transform(entity);
        Context.Preview.Show(previews);
    }

    private void BeginSelection()
    {
        SetState(CadToolState.WaitForSelect);
        Context.Tracking.Clear();
        Context.Snap.Active = false;
        Context.WorkPlane.Deactivate();
        UpdateSelectionPrompt(Context.Selection.Selected.Count);
    }

    private void BeginTransform(CadEntity[] entities)
    {
        _entities = entities;
        SetState(CadToolState.Drawing);

        if (!Context.WorkPlane.IsActive)
            Context.WorkPlane.Activate(Context.WorkPlane.Origin);
        Context.Snap.Active = true;

        OnTransformStarted();
    }

    private CadEntity[] CurrentSelection() =>
        Context.Selection.Selected
            .Where(Context.Document.IsEntitySelectable)
            .ToArray();

    private void SelectionChanged(
        object? sender,
        CadSelectionChangedEventArgs args)
    {
        if (State == CadToolState.WaitForSelect)
            UpdateSelectionPrompt(args.Entities.Count);
    }

    private void UpdateSelectionPrompt(int count)
    {
        if (count == 0)
        {
            SetStageLocalized(
                0,
                $"Cad.Prompt.{Id}.Select",
                $"{DisplayName}: select objects [Enter accept, Esc cancel]");
            return;
        }

        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.Selected",
            $"{DisplayName}: {{0}} selected [Enter accept, Esc cancel]",
            CadPrecisionInputKind.None,
            count);
    }
}
