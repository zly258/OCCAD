using OcctNet;

namespace OCCAD;

public abstract class CadSelectionTransformToolBase : CadTool
{
    private CadEntity[] _entities = [];
    private readonly List<CadEntity> _suppressedSources = [];

    protected IReadOnlyList<CadEntity> Entities => _entities;
    protected virtual bool AutoCommitValidSelection => false;
    protected virtual bool SuppressSourcesDuringPreview => false;

    public override bool CanCommitCurrentStage => State == CadToolState.WaitForSelect
        ? IsActive && IsSelectionValid(CurrentSelection())
        : base.CanCommitCurrentStage;

    internal override bool CommitCurrentStage()
    {
        if (State != CadToolState.WaitForSelect) return base.CommitCurrentStage();
        var selected = CurrentSelection();
        if (!IsActive || !IsSelectionValid(selected)) return false;
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
        Context.Preview.Clear();
        RestoreSourcePresentations();
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

        Context.Preview.Show(previews);
        if (SuppressSourcesDuringPreview)
            SuppressSourcePresentations();
    }

    protected void ClearTransformPreview()
    {
        Context.Preview.Clear();
        RestoreSourcePresentations();
    }

    private void SuppressSourcePresentations()
    {
        if (_suppressedSources.Count > 0 ||
            Context.Workspace.Engine is not
            { IsInitialized: true } engine)
            return;

        using var batch =
            engine.BeginDisplayBatch();

        foreach (var entity in _entities)
        {
            if (entity.ViewerObject is not
                { } source ||
                !engine.ContainsObject(source.Id) ||
                !Context.Document
                    .ResolveAppearance(entity)
                    .Visible)
                continue;

            engine.SetObjectVisible(
                source,
                false);
            _suppressedSources.Add(entity);
        }
    }

    private void RestoreSourcePresentations()
    {
        if (_suppressedSources.Count == 0)
            return;

        var values =
            _suppressedSources.ToArray();
        _suppressedSources.Clear();

        if (Context.Workspace.Engine is not
            { IsInitialized: true } engine)
            return;

        using var batch =
            engine.BeginDisplayBatch();

        foreach (var entity in values)
        {
            if (entity.ViewerObject is not
                { } source ||
                !engine.ContainsObject(source.Id))
                continue;

            engine.SetObjectVisible(
                source,
                Context.Document
                    .ResolveAppearance(entity)
                    .Visible);
        }
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
