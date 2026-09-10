using OcctNet;

namespace OCCAD;

public abstract class CadSelectionTransformToolBase : CadTool
{
    private CadEntity[] _entities = [];

    protected IReadOnlyList<CadEntity> Entities => _entities;
    protected virtual bool AutoCommitValidSelection => false;
    protected virtual bool SuppressSourcesDuringPreview => false;

    public override bool CanCommitCurrentStage => State == CadToolState.WaitForSelect
        ? IsActive && IsSelectionValid(CurrentSelection())
        : base.CanCommitCurrentStage;

    internal override bool CommitCurrentStage()
    {
        if (State != CadToolState.WaitForSelect)
            return base.CommitCurrentStage();

        var selected = CurrentSelection();
        if (!IsActive || !IsSelectionValid(selected))
            return false;

        BeginTransform(selected);
        return true;
    }

    protected override void OnActivated()
    {
        ResetTransformState();
        _entities = [];
        Context.Selection.Changed += SelectionChanged;

        var selected = CurrentSelection();
        if (!IsSelectionValid(selected))
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

        if (!CommitCurrentStage())
            UpdateSelectionPrompt(0);
        return true;
    }

    protected override void OnCanceled()
    {
        ClearTransformPreview();
        ResetTransformState();
    }

    protected override void OnDeactivated()
    {
        ClearReplacementPreview();
        Context.Selection.Changed -= SelectionChanged;
        _entities = [];
        ResetTransformState();
    }

    protected abstract void OnTransformStarted();

    protected virtual bool IsSelectionValid(CadEntity[] entities) =>
        entities.Length > 0;

    protected virtual void ResetTransformState()
    {
    }

    protected void RestartSelection(bool clearSelection = true)
    {
        ClearTransformPreview();
        _entities = [];
        if (clearSelection)
            Context.Selection.Clear();
        BeginSelection();
    }

    protected void ShowEntityPreview(Action<CadEntity> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var previews = _entities
            .Select(static entity => entity.Duplicate())
            .ToArray();
        foreach (var entity in previews)
            transform(entity);

        if (SuppressSourcesDuringPreview)
            ShowReplacementPreview(_entities, previews);
        else
            Context.Preview.Show(previews);
    }

    protected void ClearTransformPreview() =>
        ClearReplacementPreview();

    /// <summary>
    /// Legacy transform commit path for commands whose model operation owns its
    /// complete transaction. New interactive transforms should use the overload
    /// that receives the tool-completion callback so model mutation, cleanup and
    /// history installation share one atomic boundary.
    /// </summary>
    protected void CommitTransform(Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        var engine = Context.Engine;

        using (engine.BeginDisplayBatch())
        {
            ClearTransformPreview();
            commit();
            Context.Workspace.Tools.CompleteCurrent();
        }

        engine.Redraw();
    }

    protected void CommitTransform(Action<Action> commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        var engine = Context.Engine;

        using (engine.BeginDisplayBatch())
        {
            ClearTransformPreview();
            commit(Context.Workspace.Tools.CompleteCurrent);
        }

        engine.Redraw();
    }

    private void BeginSelection()
    {
        SetState(CadToolState.WaitForSelect);
        Context.Tracking.Clear();
        Context.Snap.Active = false;
        Context.WorkPlane.EndToolPlane();
        UpdateSelectionPrompt(Context.Selection.Selected.Count);
    }

    private void BeginTransform(CadEntity[] entities)
    {
        _entities = entities;
        SetState(CadToolState.Drawing);

        if (!Context.WorkPlane.IsActive)
            Context.WorkPlane.BeginToolPlane(Context.WorkPlane.Origin);
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
        if (State != CadToolState.WaitForSelect)
            return;

        var selected = CurrentSelection();
        if (AutoCommitValidSelection &&
            IsSelectionValid(selected))
        {
            BeginTransform(selected);
            return;
        }

        UpdateSelectionPrompt(selected.Length);
    }

    private void UpdateSelectionPrompt(int count)
    {
        if (count == 0)
        {
            SetStageLocalized(
                0,
                $"Cad.Prompt.{Id}.Select",
                $"{DisplayName}: select objects [Enter/right-click accept, Esc cancel]");
            return;
        }

        SetStageLocalized(
            0,
            $"Cad.Prompt.{Id}.Selected",
            $"{DisplayName}: {{0}} selected [Enter/right-click accept, Esc cancel]",
            CadPrecisionInputKind.None,
            count);
    }
}
