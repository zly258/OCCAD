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

    public CadToolManager(
        CadWorkspace workspace,
        CadToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _registry = registry ??
            throw new ArgumentNullException(nameof(registry));
        _context = new CadToolContext(workspace);
    }

    public CadTool? ActiveTool { get; private set; }

    internal bool OwnsInteraction =>
        _transitioning ||
        ActiveTool is not null;

    public CadInteractionMode Mode =>
        ActiveTool is null
            ? CadInteractionMode.Normal
            : CadInteractionMode.Drawing;

    public event EventHandler<CadToolChangedEventArgs>? ToolChanged;
    public event EventHandler<CadToolChangedEventArgs>? ToolUpdated;

    public void Register<TTool>(string id)
        where TTool : CadTool, new()
    {
        _registry.Register<TTool>(id);
    }

    public bool IsRegistered(string id) =>
        _registry.Contains(id);

    public bool Activate(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_registry.TryCreate(id, out var tool) ||
            tool is null)
            return false;

        CancelCurrent();
        SetActive(tool);
        return true;
    }

    public bool CanChangeDrawingPlane =>
        _context.WorkPlane.CanChangeEffectivePlane;

    public bool TryChangeDrawingPlane(CadWorkPlanePreset preset)
    {
        if (!Enum.IsDefined(preset))
            throw new ArgumentOutOfRangeException(nameof(preset));
        if (preset == CadWorkPlanePreset.Custom ||
            !CanChangeDrawingPlane)
            return false;

        var tool = ActiveTool;
        var origin =
            tool?.PrecisionReferencePoint ??
            _context.WorkPlane.Origin;

        _context.WorkPlane.SetPreset(
            preset,
            origin);
        _context.Snap.Clear();
        _context.Tracking.Clear();

        if (tool is not null)
        {
            tool.OnWorkPlaneChanged();
            if (_context.Workspace.LastPointerPosition is { } lastPointer)
                tool.RefreshPreviewFromLastPointer(lastPointer);
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

        DeactivateActiveTool(
            tool,
            canceled: false);
    }

    public bool CancelCurrent()
    {
        var tool = ActiveTool;
        if (tool is null)
        {
            ResetNeutralInteractionState();
            return false;
        }

        DeactivateActiveTool(
            tool,
            canceled: true);
        return true;
    }

    public bool HandlePointer(OcctPointerInputEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _context.Workspace.ObservePointer(
            input.X,
            input.Y);

        var tool = ActiveTool;
        if (tool is null)
            return false;

        try
        {
            if (input.Kind == OcctPointerInputKind.Pressed &&
                input.Button == OcctPointerButton.Left &&
                !(tool.State == CadToolState.WaitForSelect &&
                  tool.InteractionPolicy.SelectionEnabled))
                return SubmitCurrent(pointer: input);

            return tool.HandlePointer(input);
        }
        catch (Exception exception)
            when (IsRecoverablePointerFailure(exception))
        {
            // Invalid or temporarily unsolvable pointer geometry must not tear
            // down the command. Drop only pointer-derived transient feedback;
            // the next valid sample can continue the same tool.
            ClearPointerFeedback();
            return true;
        }
    }

    public bool CommitPoint(OcctPoint3d point) =>
        SubmitCurrent(
            new CadResolvedPoint(
                point,
                null));

    public bool SubmitPointText(string? text) =>
        _context.Workspace.Precision.TryResolvePoint(
            text,
            _context.Workspace,
            out var point) &&
        SubmitCurrent(point);

    public bool CommitCurrentStage() =>
        SubmitCurrent();

    public bool FinishCurrent()
    {
        var tool = ActiveTool;
        return tool?.Finish() == true;
    }

    public bool BeginOffsetInput()
    {
        if (ActiveTool?.CurrentStep.InputKind !=
            CadToolInputKind.Point)
            return false;

        _context.Workspace.Precision.BeginOffset();
        ToolUpdated?.Invoke(
            this,
            new(ActiveTool));
        return true;
    }

    public bool StepBackCurrent()
    {
        if (_context.Workspace.Precision.PointMode ==
            CadPointInputMode.Offset)
        {
            _context.Workspace.Precision.ResetPointInput();
            ToolUpdated?.Invoke(
                this,
                new(ActiveTool));
            return true;
        }

        var tool = ActiveTool;
        return tool?.StepBack() == true;
    }

    public bool SubmitCurrent(
        CadResolvedPoint? point = null,
        OcctPointerInputEventArgs? pointer = null)
    {
        var tool = ActiveTool;
        if (tool is null)
            return false;

        var precision = _context.Workspace.Precision;
        if (point is { } resolved)
        {
            if (!resolved.Point.IsFinite ||
                tool.CurrentStep.InputKind !=
                CadToolInputKind.Point)
                return false;

            if (precision.CaptureOffsetOrigin(resolved.Point))
            {
                ToolUpdated?.Invoke(
                    this,
                    new(tool));
                return true;
            }

            var accepted =
                tool.CommitCurrentStage(resolved);
            if (accepted)
            {
                var wasOffset =
                    precision.PointMode ==
                    CadPointInputMode.Offset;
                precision.ResetPointInput();
                if (wasOffset &&
                    ReferenceEquals(ActiveTool, tool))
                {
                    ToolUpdated?.Invoke(
                        this,
                        new(tool));
                }
            }

            return accepted;
        }

        if (precision.AwaitingOffsetOrigin &&
            _context.Workspace.LastPointerPosition is { } position)
        {
            return SubmitCurrent(
                _context.ResolvePoint(
                    position.X,
                    position.Y));
        }

        if (tool.CanCommitCurrentStage)
        {
            var accepted = tool.CommitCurrentStage();
            if (accepted)
            {
                var wasOffset =
                    precision.PointMode ==
                    CadPointInputMode.Offset;
                precision.ResetPointInput();
                if (wasOffset &&
                    ReferenceEquals(ActiveTool, tool))
                {
                    ToolUpdated?.Invoke(
                        this,
                        new(tool));
                }
            }

            return accepted;
        }

        if (pointer is not null)
            return tool.HandlePointer(pointer);

        return tool.CanFinish &&
               FinishCurrent();
    }

    public bool HandleSecondaryAction()
    {
        var tool = ActiveTool;
        if (tool is null)
            return false;

        // During command-first selection, right-click has the same meaning as
        // Enter when a valid selection exists. With no valid selection it
        // cancels. Once geometry input has started, continuous tools may finish;
        // all other tools cancel rather than implicitly committing a point.
        if (tool.State == CadToolState.WaitForSelect)
        {
            return tool.CanCommitCurrentStage
                ? CommitCurrentStage()
                : CancelCurrent();
        }

        if (tool.CanFinish &&
            FinishCurrent())
            return true;

        return CancelCurrent();
    }

    public bool HandleKey(OcctKeyInputEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key == OcctKey.Escape)
        {
            CancelCurrent();
            return true;
        }

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key == OcctKey.Backspace &&
            (ActiveTool is { CanStepBack: true } ||
             _context.Workspace.Precision.PointMode ==
             CadPointInputMode.Offset))
            return StepBackCurrent();

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key is OcctKey.Enter or OcctKey.Space &&
            ActiveTool is not null &&
            SubmitCurrent())
            return true;

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key == OcctKey.O &&
            input.Modifiers == OcctInputModifiers.None &&
            ActiveTool?.CurrentStep.InputKind ==
            CadToolInputKind.Point)
            return BeginOffsetInput();

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
            TryCleanup(
                () => tool.Deactivate(canceled: true),
                cleanupFailures);
            TryCleanup(
                () => ResetNeutralInteractionState(tool.Id),
                cleanupFailures);

            _transitioning = false;
            TryCleanup(
                RestoreSelectionGrips,
                cleanupFailures);

            if (cleanupFailures.Count > 0)
            {
                throw new AggregateException(
                    "Tool activation failed and neutral-state recovery was incomplete.",
                    new[] { activationFailure }
                        .Concat(cleanupFailures));
            }

            ExceptionDispatchInfo
                .Capture(activationFailure)
                .Throw();
            return;
        }

        ActiveTool = tool;
        tool.Updated += ActiveToolUpdated;
        _transitioning = false;
        ToolChanged?.Invoke(
            this,
            new CadToolChangedEventArgs(tool));
    }

    private void DeactivateActiveTool(
        CadTool tool,
        bool canceled)
    {
        _transitioning = true;
        tool.Updated -= ActiveToolUpdated;

        var failures = new List<Exception>();
        TryCleanup(
            () => tool.Deactivate(canceled),
            failures);

        ActiveTool = null;
        TryCleanup(
            () => ResetNeutralInteractionState(tool.Id),
            failures);

        _transitioning = false;
        TryCleanup(
            RestoreSelectionGrips,
            failures);
        TryCleanup(
            () => ToolChanged?.Invoke(
                this,
                new CadToolChangedEventArgs(null)),
            failures);

        ThrowCleanupFailures(
            "Tool deactivation cleanup failed.",
            failures);
    }

    private void ClearPointerFeedback()
    {
        var failures = new List<Exception>();
        TryCleanup(_context.Preview.Clear, failures);
        TryCleanup(_context.Snap.Clear, failures);
        TryCleanup(_context.Tracking.Clear, failures);

        ThrowCleanupFailures(
            "Pointer feedback cleanup failed.",
            failures);
    }

    private void ResetNeutralInteractionState(
        string? toolId = null)
    {
        var workspace = _context.Workspace;
        var failures = new List<Exception>();

        // The transient scene is the lifecycle authority. Clear every
        // tool-lifetime channel, not only the currently stamped owner, so a
        // failed or partially transitioned tool cannot strand presentation.
        TryCleanup(
            workspace.Transients.ClearToolState,
            failures);

        // Retry critical channels explicitly. Native cleanup can fail
        // transiently; these idempotent retries are what eliminate preview and
        // drag-marker leftovers at command completion/cancel.
        TryCleanup(workspace.Preview.Clear, failures);
        TryCleanup(workspace.Grips.ClearDragMarker, failures);
        TryCleanup(() => workspace.Snap.Active = false, failures);
        TryCleanup(() => workspace.Snap.TemporaryModes = null, failures);
        TryCleanup(workspace.Preselection.Clear, failures);
        TryCleanup(workspace.Precision.ResetFactor, failures);
        TryCleanup(workspace.Precision.ResetPointInput, failures);
        TryCleanup(workspace.Drafting.ResetTransientLocks, failures);
        TryCleanup(workspace.WorkPlane.EndToolPlane, failures);
        TryCleanup(workspace.ClearPointerObservation, failures);

        if (workspace.Engine is { IsInitialized: true } engine)
        {
            TryCleanup(
                () => engine.SetAutomaticHighlight(true),
                failures);
            TryCleanup(engine.Redraw, failures);
        }

        var violations = NeutralStateViolations;
        if (violations.Count > 0)
        {
            failures.Add(
                new InvalidOperationException(
                    $"Transient state leaked by {toolId ?? "idle cleanup"}: {string.Join(", ", violations)}"));
        }

        System.Diagnostics.Debug.Assert(
            violations.Count == 0,
            $"Transient state leaked by {toolId ?? "idle cleanup"}: {string.Join(", ", violations)}");

        ThrowCleanupFailures(
            "Tool neutral-state cleanup failed.",
            failures);
    }

    public IReadOnlyList<string> NeutralStateViolations
    {
        get
        {
            var workspace = _context.Workspace;
            List<string> violations = [];

            if (ActiveTool is not null)
                violations.Add("ActiveTool");
            if (workspace.Transients.HasToolTransient)
                violations.Add("TransientScene");
            if (workspace.Grips.HasDragTransient)
                violations.Add("GripDrag");
            if (workspace.Preview.HasTransient)
                violations.Add("Preview");
            if (workspace.Snap.HasTransient ||
                workspace.Snap.Active ||
                workspace.Snap.TemporaryModes is not null)
                violations.Add("Snap");
            if (workspace.Tracking.HasTransient)
                violations.Add("Tracking");
            if (workspace.WorkPlane.IsActive)
                violations.Add("WorkPlane");
            if (workspace.Preselection.Current is not null)
                violations.Add("Preselection");
            if (workspace.LastPointerPosition is not null ||
                workspace.LastResolvedPoint is not null)
                violations.Add("Pointer");

            return violations;
        }
    }

    private void ActiveToolUpdated(
        object? sender,
        EventArgs args)
    {
        if (sender is not CadTool tool ||
            !ReferenceEquals(tool, ActiveTool))
            return;

        ToolUpdated?.Invoke(
            this,
            new CadToolChangedEventArgs(tool));
    }

    private void RestoreSelectionGrips() =>
        _context.Workspace.RefreshSelectionGrips();

    private static void TryCleanup(
        Action cleanup,
        ICollection<Exception> failures)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void ThrowCleanupFailures(
        string message,
        IReadOnlyCollection<Exception> failures)
    {
        if (failures.Count == 0)
            return;

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo
                .Capture(failures.First())
                .Throw();
            return;
        }

        throw new AggregateException(
            message,
            failures);
    }

    private static bool IsRecoverablePointerFailure(
        Exception exception) =>
        exception is ArgumentException or
        InvalidOperationException or
        ArithmeticException;
}
