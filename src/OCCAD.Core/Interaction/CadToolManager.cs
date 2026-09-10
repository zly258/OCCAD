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
        (ActiveTool is null || ActiveTool is CadDrawingTool { Stage: 0 });

    public bool TryChangeDrawingPlane(CadWorkPlanePreset preset)
    {
        if (!Enum.IsDefined(preset))
            throw new ArgumentOutOfRangeException(nameof(preset));
        if (!CanChangeDrawingPlane)
            return false;

        var tool = ActiveTool;
        var origin = _context.WorkPlane.Origin;
        var parameters = tool?.ParameterPanel?.Parameters
            .Select(parameter =>
                (parameter.Id, Value: parameter switch
                {
                    CadChoiceToolParameterDescriptor value => value.Value,
                    CadBooleanToolParameterDescriptor value => value.Value.ToString(),
                    CadIntegerToolParameterDescriptor value => value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    CadDoubleToolParameterDescriptor value => value.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    CadOptionalDoubleToolParameterDescriptor value => value.Value?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    _ => throw new NotSupportedException(
                        $"Unsupported tool parameter: {parameter.Id}")
                }))
            .ToArray() ?? [];

        if (tool is not null)
            CancelCurrent();

        _context.WorkPlane.SetPreset(preset, origin);
        _context.Snap.Clear();
        _context.Tracking.Clear();

        if (tool is null)
            return true;

        try
        {
            Activate(tool.Id);
            foreach (var parameter in parameters)
            {
                if (!ActiveTool!.TrySetParameter(parameter.Id, parameter.Value))
                {
                    throw new InvalidOperationException(
                        $"Unable to restore tool parameter: {parameter.Id}");
                }
            }
        }
        catch
        {
            // The user work plane change is persistent, but a partially recreated
            // Tool must never survive a restore failure. Cancel restores every
            // transient Tool plane/lock/filter/snap/preview state.
            CancelCurrent();
            throw;
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
        if (tool is null) return;
        DeactivateActiveTool(tool, canceled: false);
    }

    public bool CancelCurrent()
    {
        var tool = ActiveTool;
        if (tool is null)
        {
            _context.Workspace.Preview.Clear();
            _context.Workspace.Tracking.Clear();
            _context.Workspace.Snap.Clear();
            _context.Workspace.Snap.Active = false;
            _context.Workspace.Precision.ResetFactor();
            _context.Workspace.Drafting.ResetTransientLocks();
            _context.Workspace.WorkPlane.EndToolPlane();
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
        return tool?.HandlePointer(input) == true;
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

        return tool.CanFinish &&
               FinishCurrent();
    }

    public bool HandleSecondaryAction()
    {
        var tool = ActiveTool;
        if (tool is null)
            return false;

        if (SubmitCurrent())
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
            ActiveTool is { CanStepBack: true })
            return StepBackCurrent();

        if (input.Kind == OcctKeyInputKind.Pressed &&
            input.Key == OcctKey.Enter &&
            ActiveTool is not null &&
            SubmitCurrent())
            return true;

        return ActiveTool?.HandleKey(input) == true;
    }

    private bool TrySubmitCurrentStep(CadTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (!tool.CanCommitCurrentStage ||
            tool.CurrentStep.RequiresPointer)
            return false;

        return CommitCurrentStage();
    }

    private void SetActive(CadTool tool)
    {
        _transitioning = true;
        _context.Workspace.Grips.Clear();
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
        try
        {
            tool.Deactivate(canceled);
        }
        finally
        {
            ActiveTool = null;
            try
            {
                _transitioning = false;
                RestoreSelectionGrips();
            }
            finally
            {
                ToolChanged?.Invoke(
                    this,
                    new CadToolChangedEventArgs(null));
            }
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
}
