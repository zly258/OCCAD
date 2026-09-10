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
        !_context.WorkPlane.ToolPlaneFixed;

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
            return tool.HandlePointer(input);
        }
        catch (Exception exception) when (IsRecoverablePointerFailure(exception))
        {
            // Invalid or temporarily unsolvable pointer geometry must not tear
            // down the active CAD command. Drop only transient feedback so the
            // next valid pointer sample can continue the same tool.
            _context.Preview.Clear();
            _context.Snap.Clear();
            _context.Tracking.Clear();
            return true;
        }
    }

    public bool CommitPoint(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return false;

        var tool = ActiveTool;
        if (tool is not ICadPointInputTool pointInput ||
            tool.CurrentStep.InputKind != CadToolInputKind.Point)
            return false;

        return pointInput.TryAcceptPoint(point);
    }

    public bool CommitCurrentStage()
    {
        var tool = ActiveTool;
        return tool?.CommitCurrentStage() == true;
    }

    public bool FinishCurrent()
    {
        var tool = ActiveTool;
        return tool?.Finish() == true;
    }

    public bool StepBackCurrent()
    {
        var tool = ActiveTool;
        return tool?.StepBack() == true;
    }

    public bool SubmitCurrent()
    {
        var tool = ActiveTool;
        if (tool is null)
            return false;

        if (TrySubmitCurrentStep(tool))
            return true;

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
            ActiveTool is { CanStepBack: true })
            return StepBackCurrent();

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key is OcctKey.Enter or OcctKey.Space &&
            ActiveTool is not null &&
            SubmitCurrent())
            return true;

        return ActiveTool?.HandleKey(input) == true;
    }

    private bool TrySubmitCurrentStep(CadTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // A point stage may still be committed without another mouse click
        // when the pointer has already established the direction/location and
        // precision locks (length/angle/factor) have produced the exact
        // preview. CadTool.CanCommitCurrentStage already verifies that a
        // pointer sample exists for pointer-driven stages, so do not reject
        // those stages here. This keeps mouse, Enter/Space and ToolPanel
        // Accept on the same Tool state-machine path.
        if (!tool.CanCommitCurrentStage)
            return false;

        return CommitCurrentStage();
    }

    private void SetActive(CadTool tool)
    {
        _transitioning = true;

        var workspace = _context.Workspace;
        workspace.Preselection.Clear();
        workspace.Subobjects.Clear();
        workspace.Grips.Clear();

        try
        {
            tool.Activate(_context);
        }
        catch (Exception activationFailure)
        {
            Exception? cleanupFailure = null;
            try
            {
                tool.Deactivate(canceled: true);
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }

            try
            {
                ResetNeutralInteractionState();
            }
            catch (Exception exception)
            {
                cleanupFailure ??= exception;
            }

            _transitioning = false;
            RestoreSelectionGrips();

            if (cleanupFailure is not null)
            {
                throw new AggregateException(
                    "Tool activation and cleanup both failed.",
                    activationFailure,
                    cleanupFailure);
            }

            ExceptionDispatchInfo.Capture(activationFailure).Throw();
            return;
        }

        ActiveTool = tool;
        tool.Updated += ActiveToolUpdated;
        _transitioning = false;
        ToolChanged?.Invoke(this, new CadToolChangedEventArgs(tool));
    }

    private void DeactivateActiveTool(CadTool tool, bool canceled)
    {
        _transitioning = true;
        tool.Updated -= ActiveToolUpdated;

        Exception? failure = null;
        try
        {
            tool.Deactivate(canceled);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            ActiveTool = null;
            try
            {
                ResetNeutralInteractionState();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }

            _transitioning = false;
            RestoreSelectionGrips();
            ToolChanged?.Invoke(this, new CadToolChangedEventArgs(null));
        }

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void ResetNeutralInteractionState()
    {
        var workspace = _context.Workspace;

        workspace.Preview.Clear();
        workspace.Tracking.Clear();
        workspace.Snap.Clear();
        workspace.Snap.Active = false;
        workspace.Preselection.Clear();
        workspace.Precision.ResetFactor();
        workspace.Drafting.ResetTransientLocks();
        workspace.WorkPlane.EndToolPlane();
        workspace.ClearPointerObservation();

        if (workspace.Engine is { IsInitialized: true } engine)
        {
            engine.SetAutomaticHighlight(true);
            engine.Redraw();
        }
    }

    private void ActiveToolUpdated(object? sender, EventArgs e)
    {
        if (sender is not CadTool tool || !ReferenceEquals(tool, ActiveTool))
            return;

        ToolUpdated?.Invoke(this, new CadToolChangedEventArgs(tool));
    }

    private void RestoreSelectionGrips() =>
        _context.Workspace.RefreshSelectionGrips();

    private static bool IsRecoverablePointerFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or ArithmeticException;
}
