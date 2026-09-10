using System.Globalization;
using System.Runtime.ExceptionServices;
using OcctNet;

namespace OCCAD;

public enum CadToolState
{
    Idle,
    Drawing,
    WaitForSelect
}

public enum CadToolInputKind
{
    Point,
    Selection,
    Distance,
    Angle,
    Choice,
    Confirmation
}

public readonly record struct CadToolStep(
    int Index,
    CadToolInputKind InputKind,
    CadPrecisionInputKind PrecisionInputs,
    CadToolPrompt? Prompt)
{
    public bool RequiresPointer =>
        InputKind == CadToolInputKind.Point;
}

public readonly record struct CadToolInteractionPolicy(
    bool SelectionEnabled,
    bool PreselectionEnabled,
    bool GripEnabled)
{
    public static CadToolInteractionPolicy Drawing =>
        new(false, false, false);

    public static CadToolInteractionPolicy Selection =>
        new(true, true, false);
}

public readonly record struct CadToolInteractionState(
    CadToolStep Step,
    CadToolState State,
    bool HasPreview,
    bool CanCommitCurrentStage,
    bool CanCancel,
    bool CanFinish,
    bool CanStepBack);

public abstract class CadTool
{
    protected CadToolContext Context { get; private set; } = null!;

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string LocalizationKey =>
        $"Cad.Text.{DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)}";

    private CadToolState _state = CadToolState.Idle;
    private int _stage;
    private CadEntityFilter? _previousSelectionFilter;
    private CadSelectionScope _previousSelectionScope;
    private CadSubshapeMask _previousSubshapeMask;

    public CadToolState State => _state;
    public int Stage => _stage;
    public bool IsActive { get; private set; }
    public CadToolPrompt? Prompt { get; private set; }

    /// <summary>
    /// UI-neutral parameter contract for the active tool. Frontends may render
    /// this schema in any form without owning or duplicating tool state.
    /// </summary>
    public virtual CadToolParameterSchema? ParameterSchema => null;

    public CadPrecisionInputKind PrecisionInputs =>
        Prompt?.PrecisionInputs ?? CadPrecisionInputKind.None;
    public virtual string PrecisionLengthLabel => "Length";
    public virtual CadSnapResolvePolicy SnapResolvePolicy =>
        CadSnapResolvePolicy.KeepExactPoint;
    public virtual CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Point;
    public virtual CadToolInteractionPolicy InteractionPolicy =>
        State == CadToolState.WaitForSelect
            ? CadToolInteractionPolicy.Selection
            : CadToolInteractionPolicy.Drawing;
    public virtual CadSelectionScope SelectionScope => CadSelectionScope.Entity;
    public virtual CadSubshapeMask SubshapeMask => CadSubshapeMask.All;
    public virtual string PrecisionAngleLabel => "Angle";
    public virtual string PrecisionFactorLabel => "Factor";

    public virtual OcctPoint3d? PrecisionReferencePoint =>
        IsActive && Context.WorkPlane.IsActive
            ? Context.WorkPlane.Origin
            : null;

    public CadToolStep CurrentStep =>
        new(Stage, InputKind, PrecisionInputs, Prompt);

    public bool HasPreview =>
        IsActive &&
        Context.Preview.HasTransient;
    public virtual bool CanCommitCurrentStage =>
        IsActive &&
        CanCommitCurrentStageCore &&
        (!CurrentStep.RequiresPointer || Context.Workspace.LastPointerPosition is not null);
    public bool CanCancel => IsActive;
    public bool CanFinish => IsActive && CanFinishCore;
    public bool CanStepBack => IsActive && CanStepBackCore;
    public CadToolInteractionState InteractionState =>
        new(CurrentStep, State, HasPreview, CanCommitCurrentStage, CanCancel, CanFinish, CanStepBack);

    public event EventHandler? Updated;

    internal void Activate(CadToolContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        IsActive = true;
        _state = CadToolState.Drawing;
        _stage = 0;
        _previousSelectionFilter = Context.Selection.Filter;
        _previousSelectionScope = Context.Selection.Scope;
        _previousSubshapeMask = Context.Selection.SubshapeMask;
        Context.Selection.SetScope(SelectionScope, SubshapeMask);
        Context.ActiveTool = this;
        Context.WorkPlane.BeginToolPlane(Context.WorkPlane.Origin);
        Context.Snap.Active = true;
        OnActivated();
        SynchronizeSnapState();
    }

    internal void Deactivate(bool canceled)
    {
        if (!IsActive) return;

        Exception? failure = null;
        if (canceled)
            TryCleanup(OnCanceled);
        TryCleanup(OnDeactivated);

        // Tool-local cleanup belongs here. Workspace-wide transient state is
        // reset exactly once by CadToolManager.ResetNeutralInteractionState().
        TryCleanup(Context.Preview.Clear);
        TryCleanup(() => Context.Selection.SetFilter(_previousSelectionFilter));
        TryCleanup(() => Context.Selection.SetScope(_previousSelectionScope, _previousSubshapeMask));

        Prompt = null;
        IsActive = false;
        _state = CadToolState.Idle;
        _stage = 0;
        _previousSelectionFilter = null;
        if (ReferenceEquals(Context.ActiveTool, this))
            Context.ActiveTool = null;

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        return;

        void TryCleanup(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception exception) { failure ??= exception; }
        }
    }

    internal bool CommitCurrentStage(CadResolvedPoint point) =>
        IsActive && CurrentStep.InputKind == CadToolInputKind.Point &&
        this is ICadPointInputTool input && input.TryAcceptPoint(point.Point);

    internal virtual bool CommitCurrentStage()
    {
        if (!CanCommitCurrentStage)
            return false;
        var pointer = Context.Workspace.LastPointerPosition ?? default;
        return OnCommitCurrentStage(pointer);
    }

    internal bool Finish() => CanFinish && OnFinish();
    internal bool StepBack() => CanStepBack && OnStepBack();

    internal bool ApplyPrecisionInput(CadPrecisionInput input)
    {
        var applied = OnPrecisionInputApplied(input);
        if (applied && IsActive)
            PublishUpdated();
        return applied;
    }

    public bool TrySetParameter(string id, string value)
    {
        if (!IsActive)
            return false;

        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(value);

        var parameterId = id.Trim();
        var schema = ParameterSchema;
        if (schema is null ||
            !schema.TryNormalizeValue(parameterId, value, out var normalizedValue))
        {
            return false;
        }

        return OnSetParameter(parameterId, normalizedValue);
    }

    public virtual bool HandlePointer(OcctPointerInputEventArgs input) => false;
    public virtual bool HandleKey(OcctKeyInputEventArgs input) => false;

    protected bool CancelOnRightClick(OcctPointerInputEventArgs input)
    {
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Right)
            return false;

        return Context.Workspace.Tools.HandleSecondaryAction();
    }

    protected void SetStage(int stage, string message, CadPrecisionInputKind precisionInputs = CadPrecisionInputKind.None)
    {
        if (stage < 0) throw new ArgumentOutOfRangeException(nameof(stage));
        _stage = stage;
        ResetStageTransientState();
        SetPrompt(message, precisionInputs);
    }

    protected void SetPrompt(string message, CadPrecisionInputKind precisionInputs = CadPrecisionInputKind.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Prompt = new CadToolPrompt(message.Trim(), precisionInputs);
        PublishUpdated();
    }

    protected void SetStageLocalized(
        int stage,
        string resourceKey,
        string fallback,
        CadPrecisionInputKind precisionInputs = CadPrecisionInputKind.None,
        params object?[] arguments)
    {
        if (stage < 0) throw new ArgumentOutOfRangeException(nameof(stage));
        _stage = stage;
        ResetStageTransientState();
        SetPromptLocalized(resourceKey, fallback, precisionInputs, arguments);
    }

    protected void SetPromptLocalized(
        string resourceKey,
        string fallback,
        CadPrecisionInputKind precisionInputs = CadPrecisionInputKind.None,
        params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        ArgumentNullException.ThrowIfNull(arguments);

        var normalizedFallback = fallback.Trim();
        var message = arguments.Length == 0
            ? normalizedFallback
            : string.Format(CultureInfo.InvariantCulture, normalizedFallback, arguments);

        Prompt = new CadToolPrompt(message, precisionInputs)
        {
            ResourceKey = resourceKey.Trim(),
            FormatArguments = arguments.ToArray()
        };
        PublishUpdated();
    }

    protected void SetState(CadToolState state)
    {
        if (_state == state) return;
        _state = state;
        PublishUpdated();
    }

    protected void SetWorkPlane(
        OcctPoint3d origin,
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        bool lockPlane = true)
    {
        var current = Context.WorkPlane.EffectivePlane;
        var safeOrigin = origin.IsFinite ? origin : current.Origin;
        var safeX = xAxis.TryNormalize(out var normalizedX) ? normalizedX : current.XAxis;
        var safeY = yAxis.TryNormalize(out var normalizedY) ? normalizedY : current.YAxis;
        if (!safeX.Cross(safeY).TryNormalize(out _))
        {
            safeX = current.XAxis;
            safeY = current.YAxis;
        }

        Context.WorkPlane.SetToolPlaneFixed(false);
        Context.WorkPlane.SetToolPlane(safeOrigin, safeX, safeY);
        Context.WorkPlane.SetToolPlaneFixed(lockPlane);
    }

    protected internal virtual void OnWorkPlaneChanged() { }
    protected internal virtual void RefreshPreviewFromLastPointer(CadPointerPosition pointer) { }

    protected void SetSelectionFilter(CadEntityFilter? filter) => Context.Selection.SetFilter(filter);
    protected void NotifyUpdated() => PublishUpdated();

    protected void ShowReplacementPreview(
        IReadOnlyList<CadEntity> sources,
        IReadOnlyList<CadEntity> replacements) =>
        Context.Preview.ShowReplacement(sources, replacements);

    protected void ClearReplacementPreview() => Context.Preview.Clear();

    protected void CommitReplacementPreview(Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        var engine = Context.Workspace.Engine;
        using (engine?.BeginDisplayBatch())
        {
            Context.Preview.Clear();
            if (Context.Preview.HasTransient)
                throw new InvalidOperationException("Replacement preview cleanup is incomplete.");
            try { commit(); }
            finally { engine?.Redraw(); }
        }
    }

    private void PublishUpdated()
    {
        SynchronizeSnapState();
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private void SynchronizeSnapState()
    {
        if (!IsActive) return;
        Context.Snap.Active = CurrentStep.RequiresPointer;
    }

    protected void LockStageAngle(double angleDegrees)
    {
        if (!double.IsFinite(angleDegrees))
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        Context.Workspace.Drafting.LockedAngleDegrees = angleDegrees;
        Context.Workspace.Drafting.AngleLockEnabled = true;
    }

    protected virtual bool CanCommitCurrentStageCore =>
        this is ICadPointInputTool || InputKind == CadToolInputKind.Confirmation ||
        InputKind == CadToolInputKind.Selection && Context.Workspace.Preselection.Current is not null;
    protected virtual bool CanFinishCore => false;
    protected virtual bool CanStepBackCore => false;
    protected virtual bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        InputKind == CadToolInputKind.Point && this is ICadPointInputTool pointInput
            ? pointInput.TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y, PrecisionReferencePoint).Point)
            : InputKind is CadToolInputKind.Selection or CadToolInputKind.Confirmation && HandlePointer(new(
                OcctPointerInputKind.Pressed, OcctPointerButton.Left, OcctPointerButtons.Left,
                pointer.X, pointer.Y, 0, OcctInputModifiers.None));
    protected virtual bool OnFinish() => false;
    protected virtual bool OnStepBack() => false;
    protected virtual bool OnPrecisionInputApplied(CadPrecisionInput input) => true;
    protected virtual bool OnSetParameter(string id, string value) => false;

    private void ResetStageTransientState()
    {
        ClearReplacementPreview();
        Context.Snap.TemporaryModes = null;
        Context.Snap.Clear();
        Context.Tracking.Clear();
        ResetPrecisionLocks();
    }

    private void ResetPrecisionLocks()
    {
        Context.Workspace.Drafting.LengthLockEnabled = false;
        Context.Workspace.Drafting.LockedLength = 0.0;
        Context.Workspace.Drafting.AngleLockEnabled = false;
        Context.Workspace.Drafting.LockedAngleDegrees = 0.0;
        Context.Workspace.Precision.ResetFactor();
    }

    protected virtual void OnActivated() { }
    protected virtual void OnCanceled() { }
    protected virtual void OnDeactivated() { }
}

public sealed class CadToolContext(CadWorkspace workspace)
{
    internal CadTool? ActiveTool { get; set; }

    public CadWorkspace Workspace { get; } =
        workspace ?? throw new ArgumentNullException(nameof(workspace));

    public CadDocument Document => Workspace.Document;
    public CadSelectionManager Selection => Workspace.Selection;
    public OcctEngine Engine => Workspace.Engine ?? throw new InvalidOperationException("No OCCT engine is attached.");
    public CadWorkPlane WorkPlane => Workspace.WorkPlane;
    public CadPreviewManager Preview => Workspace.Preview;
    public CadTrackingManager Tracking => Workspace.Tracking;
    public CadSnapManager Snap => Workspace.Snap;

    public CadResolvedPoint ResolvePoint(int x, int y, OcctPoint3d? constraintOrigin = null)
    {
        var resolved = Workspace.ResolvePoint(
            x,
            y,
            constraintOrigin,
            ActiveTool?.SnapResolvePolicy ?? CadSnapResolvePolicy.KeepExactPoint);

        if (ActiveTool is not CadDrawingTool || !WorkPlane.IsActive)
            return resolved;

        var frame = WorkPlane.EffectivePlane;
        var projected = CadPlaneGeometry.ProjectToPlane(
            frame.Origin,
            resolved.Point,
            frame.XAxis,
            frame.YAxis);
        if (!projected.IsFinite)
            return resolved;

        if (resolved.Snap is not null && projected.DistanceTo(resolved.Point) > 1e-8)
            Snap.Clear();

        return resolved with
        {
            Point = projected,
            Snap = projected.DistanceTo(resolved.Point) <= 1e-8 ? resolved.Snap : null
        };
    }

    public void AddEntity(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Workspace.AddEntity(entity);
    }
}
