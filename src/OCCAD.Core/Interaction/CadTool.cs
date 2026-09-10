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

public readonly record struct CadToolInteractionState(
    int Stage,
    CadToolState State,
    CadPrecisionInputKind PrecisionInputs,
    bool HasPreview,
    bool CanCommitCurrentStage,
    bool CanCancel);

public abstract class CadTool
{
    protected CadToolContext Context { get; private set; } = null!;

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string LocalizationKey =>
        $"Cad.Text.{DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)}";

    private CadToolState _state = CadToolState.Idle;
    private int _stage;
    private CadSelectionFilter? _previousSelectionFilter;

    public CadToolState State => _state;
    public int Stage => _stage;
    public bool IsActive { get; private set; }
    public CadToolPrompt? Prompt { get; private set; }
    public virtual CadToolPanelDescriptor? ParameterPanel => null;
    public CadPrecisionInputKind PrecisionInputs =>
        Prompt?.PrecisionInputs ?? CadPrecisionInputKind.None;
    public virtual string PrecisionLengthLabel => "Length";
    public virtual bool AllowsPreselectionDuringDrawing => false;
    public virtual string PrecisionAngleLabel => "Angle";
    public virtual string PrecisionFactorLabel => "Factor";

    public virtual OcctPoint3d? PrecisionReferencePoint =>
        IsActive && Context.WorkPlane.IsActive
            ? Context.WorkPlane.Origin
            : null;

    public bool HasPreview =>
        IsActive && Context.Preview.IsVisible;
    public virtual bool CanCommitCurrentStage =>
        IsActive &&
        CanCommitCurrentStageCore &&
        Context.Workspace.LastPointerPosition is not null;
    public bool CanCancel => IsActive;
    public bool CanFinish => IsActive && CanFinishCore;
    public bool CanStepBack => IsActive && CanStepBackCore;
    public CadToolInteractionState InteractionState =>
        new(
            Stage,
            State,
            PrecisionInputs,
            HasPreview,
            CanCommitCurrentStage,
            CanCancel);

    public event EventHandler? Updated;

    internal void Activate(CadToolContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        IsActive = true;
        _state = CadToolState.Drawing;
        _stage = 0;
        _previousSelectionFilter = Context.Selection.Filter;
        Context.WorkPlane.Activate(Context.WorkPlane.Origin);
        Context.Snap.Active = true;
        OnActivated();
    }

    internal void Deactivate(bool canceled)
    {
        if (!IsActive) return;

        Exception? failure = null;
        if (canceled)
            TryCleanup(OnCanceled);
        TryCleanup(OnDeactivated);
        TryCleanup(Context.Preview.Clear);
        TryCleanup(Context.Tracking.Clear);
        TryCleanup(Context.Snap.Clear);
        TryCleanup(Context.Workspace.Precision.ResetFactor);
        TryCleanup(() => Context.Selection.SetFilter(_previousSelectionFilter));
        TryCleanup(() => Context.Snap.Active = false);
        TryCleanup(Context.WorkPlane.Deactivate);

        Prompt = null;
        IsActive = false;
        _state = CadToolState.Idle;
        _stage = 0;
        _previousSelectionFilter = null;

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        return;

        void TryCleanup(Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }
    }

    internal virtual bool CommitCurrentStage()
    {
        if (!CanCommitCurrentStage ||
            Context.Workspace.LastPointerPosition is not { } pointer)
            return false;

        return OnCommitCurrentStage(pointer);
    }

    internal bool Finish()
    {
        if (!CanFinish)
            return false;

        return OnFinish();
    }

    internal bool StepBack()
    {
        if (!CanStepBack)
            return false;

        return OnStepBack();
    }

    internal bool ApplyPrecisionInput(CadPrecisionInput input) =>
        OnPrecisionInputApplied(input);

    public bool TrySetParameter(string id, string value)
    {
        if (!IsActive)
            return false;
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(value);

        return OnSetParameter(id.Trim(), value.Trim());
    }

    public virtual bool HandlePointer(OcctPointerInputEventArgs input) => false;

    public virtual bool HandleKey(OcctKeyInputEventArgs input) => false;

    protected void SetStage(
        int stage,
        string message,
        CadPrecisionInputKind precisionInputs = CadPrecisionInputKind.None)
    {
        if (stage < 0)
            throw new ArgumentOutOfRangeException(nameof(stage));

        _stage = stage;
        ResetStageTransientState();
        SetPrompt(message, precisionInputs);
    }

    protected void SetPrompt(
        string message,
        CadPrecisionInputKind precisionInputs = CadPrecisionInputKind.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var normalized = message.Trim();
        Prompt = new CadToolPrompt(normalized, precisionInputs);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    protected void SetStageLocalized(
        int stage,
        string resourceKey,
        string fallback,
        CadPrecisionInputKind precisionInputs = CadPrecisionInputKind.None,
        params object?[] arguments)
    {
        if (stage < 0)
            throw new ArgumentOutOfRangeException(nameof(stage));

        _stage = stage;
        ResetStageTransientState();
        SetPromptLocalized(
            resourceKey,
            fallback,
            precisionInputs,
            arguments);
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
            : string.Format(
                CultureInfo.InvariantCulture,
                normalizedFallback,
                arguments);

        Prompt = new CadToolPrompt(message, precisionInputs)
        {
            ResourceKey = resourceKey.Trim(),
            FormatArguments = arguments.ToArray()
        };
        Updated?.Invoke(this, EventArgs.Empty);
    }

    protected void SetState(CadToolState state)
    {
        if (_state == state) return;
        _state = state;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    protected void SetWorkPlane(
        OcctPoint3d origin,
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        bool lockPlane = true)
    {
        Context.WorkPlane.SetPlaneLocked(false);
        Context.WorkPlane.SetCustom(origin, xAxis, yAxis);
        Context.WorkPlane.SetPlaneLocked(lockPlane);
    }

    protected void SetSelectionFilter(CadSelectionFilter? filter) =>
        Context.Selection.SetFilter(filter);

    protected void NotifyUpdated() =>
        Updated?.Invoke(this, EventArgs.Empty);

    protected void LockStageAngle(double angleDegrees)
    {
        if (!double.IsFinite(angleDegrees))
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));

        Context.WorkPlane.LockedAngleDegrees = angleDegrees;
        Context.WorkPlane.AngleLockEnabled = true;
    }

    protected virtual bool CanCommitCurrentStageCore => false;
    protected virtual bool CanFinishCore => false;
    protected virtual bool CanStepBackCore => false;

    protected virtual bool OnCommitCurrentStage(CadPointerPosition pointer) => false;
    protected virtual bool OnFinish() => false;
    protected virtual bool OnStepBack() => false;
    protected virtual bool OnPrecisionInputApplied(CadPrecisionInput input) => true;
    protected virtual bool OnSetParameter(string id, string value) => false;

    private void ResetStageTransientState()
    {
        Context.Snap.TemporaryModes = null;
        Context.Snap.Clear();
        Context.Tracking.Clear();
        ResetPrecisionLocks();
    }

    private void ResetPrecisionLocks()
    {
        Context.WorkPlane.LengthLockEnabled = false;
        Context.WorkPlane.LockedLength = 0.0;
        Context.WorkPlane.AngleLockEnabled = false;
        Context.WorkPlane.LockedAngleDegrees = 0.0;
        Context.Workspace.Precision.ResetFactor();
    }

    protected virtual void OnActivated() { }
    protected virtual void OnCanceled() { }
    protected virtual void OnDeactivated() { }
}

public sealed class CadToolContext(CadWorkspace workspace)
{
    public CadWorkspace Workspace { get; } =
        workspace ?? throw new ArgumentNullException(nameof(workspace));

    public CadDocument Document => Workspace.Document;
    public CadSelectionManager Selection => Workspace.Selection;
    public OcctEngine Engine =>
        Workspace.Engine ?? throw new InvalidOperationException("No OCCT engine is attached.");
    public CadWorkPlane WorkPlane => Workspace.WorkPlane;
    public CadPreviewManager Preview => Workspace.Preview;
    public CadTrackingManager Tracking => Workspace.Tracking;
    public CadSnapManager Snap => Workspace.Snap;

    public CadResolvedPoint ResolvePoint(
        int x,
        int y,
        OcctPoint3d? constraintOrigin = null) =>
        Workspace.ResolvePoint(x, y, constraintOrigin);

    public void AddEntity(CadEntity entity) => Workspace.AddEntity(entity);
}
