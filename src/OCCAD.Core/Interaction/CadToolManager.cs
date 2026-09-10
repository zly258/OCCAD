using System.Runtime.ExceptionServices;
using OcctNet;

namespace OCCAD;

public enum CadInteractionMode
{
    Normal,
    Drawing
}

public sealed class CadToolChangedEventArgs(CadTool? tool) : EventArgs
{
    public CadTool? Tool { get; } = tool;
}

public sealed class CadToolManager
{
    private readonly CadToolRegistry _registry;
    private readonly CadToolContext _context;
    private bool _transitioning;

    public CadToolManager(CadWorkspace workspace, CadToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _context = new CadToolContext(workspace);
    }

    public CadTool? ActiveTool { get; private set; }

    internal bool OwnsInteraction =>
        _transitioning ||
        ActiveTool is not null;

    public CadInteractionMode Mode =>
        ActiveTool is null ? CadInteractionMode.Normal : CadInteractionMode.Drawing;

    public event EventHandler<CadToolChangedEventArgs>? ToolChanged;
    public event EventHandler<CadToolChangedEventArgs>? ToolUpdated;

    public void Register<TTool>(string id)
        where TTool : CadTool, new()
    {
        _registry.Register<TTool>(id);
    }

    public bool IsRegistered(string id) => _registry.Contains(id);

    public bool Activate(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_registry.TryCreate(id, out var tool) || tool is null)
            return false;

        CancelCurrent();
        SetActive(tool);
        return true;
    }

    public bool CanChangeDrawingPlane =>
        !_context.WorkPlane.UserPlaneLocked &&
        !_context.WorkPlane.ToolPlaneFixed &&
        !_context.WorkPlane.GripPlaneFixed;

    public bool TryChangeDrawingPlane(CadWorkPlanePreset preset)
    {
        if (!Enum.IsDefined(preset))
            throw new ArgumentOutOfRangeException(nameof(preset));
        if (!CanChangeDrawingPlane)
            return false;

        var tool = ActiveTool;
        var origin = tool?.PrecisionReferencePoint ?? _context.WorkPlane.Origin;

        _context.WorkPlane.SetPreset(preset, origin);
        _context.Snap.Clear();
        _context.Tracking.Clear();

        if (tool is not null)
        {
            tool.OnWorkPlaneChanged();
            if (_context.Workspace.LastPointerPosition is { } lastPtr)
                tool.RefreshPreviewFromLastPointer(lastPtr);
        }

        return true;
    }

    public void BeginGripEdit(CadGripPoint grip)
    {
        CancelCurrent();
        SetActive(new GripEditTool(grip));
    }

    public void CompleteCurrent()
    {
        var tool = ActiveTool;
        if (tool is null)
            return;

        DeactivateActiveTool(tool, canceled: false);
    }

    public bool CancelCurrent()
    {
        var tool = ActiveTool;
        if (tool is null)
        {
            ResetNeutralInteractionState();
            return false;
        }

        DeactivateActiveTool(tool, canceled: true);
        return true;
    }

    public bool HandlePointer(OcctPointerInputEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _context.Workspace.ObservePointer(input.X, input.Y);

        var tool = ActiveTool;
        if (tool is null)
            return false;

        try
        {
            if (input.Kind == OcctPointerInputKind.Pressed && input.Button == OcctPointerButton.Left &&
                !(tool.State == CadToolState.WaitForSelect && tool.InteractionPolicy.SelectionEnabled))
                return SubmitCurrent(pointer: input);
            return tool.HandlePointer(input);
        }
        catch (Exception exception) when (IsRecoverablePointerFailure(exception))
        {
            // Invalid or temporarily unsolvable pointer geometry must not tear
            // down the active CAD command. Transient cleanup is best-effort as
            // well: one native feedback channel failing to clear must not stop
            // the other channels from being reset or convert a recoverable input
            // sample into a false command failure.
            ClearPointerFeedbackAfterRecoverableFailure(exception);
            return true;
        }
    }

    public bool CommitPoint(OcctPoint3d point) => SubmitCurrent(new CadResolvedPoint(point, null));

    public bool SubmitPointText(string? text) =>
        _context.Workspace.Precision.TryResolvePoint(text, _context.Workspace, out var point) &&
        SubmitCurrent(point);

    public bool CommitCurrentStage() => SubmitCurrent();

    public bool FinishCurrent()
    {
        var tool = ActiveTool;
        return tool?.Finish() == true;
    }

    public bool BeginOffsetInput()
    {
        if (ActiveTool?.CurrentStep.InputKind != CadToolInputKind.Point) return false;
        _context.Workspace.Precision.BeginOffset();
        PublishToolUpdated(ActiveTool);
        return true;
    }

    public bool StepBackCurrent()
    {
        if (_context.Workspace.Precision.PointMode == CadPointInputMode.Offset)
        {
            _context.Workspace.Precision.ResetPointInput();
            PublishToolUpdated(ActiveTool);
            return true;
        }
        var tool = ActiveTool;
        return tool?.StepBack() == true;
    }

    public bool SubmitCurrent(CadResolvedPoint? point = null, OcctPointerInputEventArgs? pointer = null)
    {
        var tool = ActiveTool;
        if (tool is null)
            return false;

        var precision = _context.Workspace.Precision;
        if (point is { } resolved)
        {
            if (!resolved.Point.IsFinite || tool.CurrentStep.InputKind != CadToolInputKind.Point) return false;
            if (precision.CaptureOffsetOrigin(resolved.Point))
            {
                PublishToolUpdated(tool);
                return true;
            }
            var accepted = tool.CommitCurrentStage(resolved);
            if (accepted)
            {
                var wasOffset = precision.PointMode == CadPointInputMode.Offset;
                precision.ResetPointInput();
                if (wasOffset && ReferenceEquals(ActiveTool, tool))
                    PublishToolUpdated(tool);
            }
            return accepted;
        }
        if (precision.AwaitingOffsetOrigin && _context.Workspace.LastPointerPosition is { } position)
            return SubmitCurrent(_context.ResolvePoint(position.X, position.Y));
        if (tool.CanCommitCurrentStage)
        {
            var accepted = tool.CommitCurrentStage();
            if (accepted)
            {
                var wasOffset = precision.PointMode == CadPointInputMode.Offset;
                precision.ResetPointInput();
                if (wasOffset && ReferenceEquals(ActiveTool, tool))
                    PublishToolUpdated(tool);
            }
            return accepted;
        }
        if (pointer is not null) return tool.HandlePointer(pointer);
        return tool.CanFinish && FinishCurrent();
    }

    public bool HandleSecondaryAction()
    {
        var tool = ActiveTool;
        if (tool is null)
            return false;

        // During command-first selection, right-click has the same meaning as
        // Enter when a valid selection exists. With no valid selection it
        // cancels the command. Once geometry input has started, continuous
        // tools may finish; all other tools cancel rather than implicitly
        // committing another pointer sample.
        if (tool.State == CadToolState.WaitForSelect)
            return tool.CanCommitCurrentStage
                ? CommitCurrentStage()
                : CancelCurrent();

        if (tool.CanFinish && FinishCurrent())
            return true;

        return CancelCurrent();
    }

    public bool HandleKey(OcctKeyInputEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Kind == OcctKeyInputKind.Pressed && input.Key == OcctKey.Escape)
        {
            CancelCurrent();
            return true;
        }

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key == OcctKey.Backspace &&
            (ActiveTool is { CanStepBack: true } || _context.Workspace.Precision.PointMode == CadPointInputMode.Offset))
            return StepBackCurrent();

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key is OcctKey.Enter or OcctKey.Space &&
            ActiveTool is not null &&
            SubmitCurrent())
            return true;

        if (input.Kind == OcctKeyInputKind.Pressed && input.Key == OcctKey.O &&
            input.Modifiers == OcctInputModifiers.None && ActiveTool?.CurrentStep.InputKind == CadToolInputKind.Point)
        {
            return BeginOffsetInput();
        }
        return ActiveTool?.HandleKey(input) == true;
    }

    private void SetActive(CadTool tool)
    {
        _transitioning = true;

        var workspace = _context.Workspace;
        workspace.Transients.BeginToolSession();
        try
        {
            workspace.Preselection.Clear();
            workspace.Subobjects.Clear();
            workspace.Grips.Clear();
            tool.Activate(_context);
        }
        catch (Exception activationFailure)
        {
            var cleanupFailures = new List<Exception>();
            Cleanup(() => tool.Deactivate(canceled: true));
            Cleanup(() => ResetNeutralInteractionState(tool.Id));

            _transitioning = false;
            Cleanup(RestoreSelectionGrips);

            if (cleanupFailures.Count > 0)
            {
                throw new AggregateException(
                    "Tool activation and cleanup both failed.",
                    new[] { activationFailure }.Concat(cleanupFailures));
            }

            ExceptionDispatchInfo.Capture(activationFailure).Throw();
            return;

            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception exception) { cleanupFailures.Add(exception); }
            }
        }

        ActiveTool = tool;
        tool.Updated += ActiveToolUpdated;
        _transitioning = false;
        PublishToolChanged(tool);
    }

    private void DeactivateActiveTool(CadTool tool, bool canceled)
    {
        _transitioning = true;
        tool.Updated -= ActiveToolUpdated;

        var failures = new List<Exception>();
        Cleanup(() => tool.Deactivate(canceled));

        ActiveTool = null;
        Cleanup(() => ResetNeutralInteractionState(tool.Id));
        _transitioning = false;
        Cleanup(RestoreSelectionGrips);

        // Tool state is already authoritative and neutral here. UI observers are
        // notifications only and cannot veto or reverse a completed transition.
        PublishToolChanged(null);

        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Tool deactivation cleanup failed.", failures);
        return;

        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }
    }

    private void ResetNeutralInteractionState(string? toolId = null)
    {
        var workspace = _context.Workspace;

        List<Exception> failures = [];
        Cleanup(() => workspace.Transients.ClearOwner(workspace.Transients.CurrentToolOwner));
        Cleanup(() => workspace.Snap.Active = false);
        Cleanup(() => workspace.Snap.TemporaryModes = null);
        Cleanup(workspace.Preselection.Clear);
        Cleanup(workspace.Precision.ResetFactor);
        Cleanup(workspace.Precision.ResetPointInput);
        Cleanup(workspace.Drafting.ResetTransientLocks);
        Cleanup(workspace.WorkPlane.EndToolPlane);
        Cleanup(workspace.ClearPointerObservation);

        if (workspace.Engine is { IsInitialized: true } engine)
        {
            Cleanup(() => engine.SetAutomaticHighlight(true));
            Cleanup(engine.Redraw);
        }

        System.Diagnostics.Debug.Assert(NeutralStateViolations.Count == 0,
            $"Transient state leaked by {toolId ?? "idle cleanup"}: {string.Join(", ", NeutralStateViolations)}");
        if (failures.Count > 0)
            throw new AggregateException("Tool neutral-state cleanup failed.", failures);

        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(exception); }
        }
    }

    public IReadOnlyList<string> NeutralStateViolations
    {
        get
        {
            var workspace = _context.Workspace;
            List<string> violations = [];
            if (ActiveTool is not null) violations.Add("ActiveTool");
            if (workspace.Transients.HasToolTransient) violations.Add("TransientScene");
            if (workspace.Grips.HasDragTransient) violations.Add("GripDrag");
            if (workspace.Preview.HasTransient) violations.Add("Preview");
            if (workspace.Snap.HasTransient || workspace.Snap.Active || workspace.Snap.TemporaryModes is not null) violations.Add("Snap");
            if (workspace.Tracking.HasTransient) violations.Add("Tracking");
            if (workspace.WorkPlane.IsActive) violations.Add("WorkPlane");
            if (workspace.Preselection.Current is not null) violations.Add("Preselection");
            if (workspace.LastPointerPosition is not null || workspace.LastResolvedPoint is not null) violations.Add("Pointer");
            return violations;
        }
    }

    private void ClearPointerFeedbackAfterRecoverableFailure(Exception pointerFailure)
    {
        var failures = new List<Exception>();
        Cleanup(_context.Preview.Clear);
        Cleanup(_context.Snap.Clear);
        Cleanup(_context.Tracking.Clear);

        if (failures.Count == 0)
            return;

        System.Diagnostics.Debug.WriteLine(
            $"Recoverable pointer failure '{pointerFailure.Message}' also had {failures.Count} transient cleanup failure(s): " +
            string.Join(" | ", failures.Select(static failure => failure.Message)));

        return;

        void Cleanup(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception) when (IsRecoverableCleanupFailure(exception))
            {
                failures.Add(exception);
            }
        }
    }

    private void ActiveToolUpdated(object? sender, EventArgs e)
    {
        if (sender is not CadTool tool || !ReferenceEquals(tool, ActiveTool))
            return;

        PublishToolUpdated(tool);
    }

    private void RestoreSelectionGrips() =>
        _context.Workspace.RefreshSelectionGrips();

    private void PublishToolChanged(CadTool? tool) =>
        PublishObservers(ToolChanged, new CadToolChangedEventArgs(tool), "ToolChanged");

    private void PublishToolUpdated(CadTool? tool) =>
        PublishObservers(ToolUpdated, new CadToolChangedEventArgs(tool), "ToolUpdated");

    private void PublishObservers(
        EventHandler<CadToolChangedEventArgs>? handlers,
        CadToolChangedEventArgs args,
        string eventName)
    {
        if (handlers is null)
            return;

        foreach (EventHandler<CadToolChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception) when (IsRecoverableObserverFailure(exception))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"{eventName} observer failed after tool state changed: {exception}");
            }
        }
    }

    private static bool IsRecoverableObserverFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static bool IsRecoverableCleanupFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static bool IsRecoverablePointerFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or ArithmeticException;
}
