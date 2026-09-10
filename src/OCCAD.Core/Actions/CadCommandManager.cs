using System.Runtime.CompilerServices;

namespace OCCAD;

public enum CadCommandResultKind
{
    Executed,
    InputApplied,
    Repeated,
    Canceled,
    Failed
}

public readonly record struct CadCommandResult(
    CadCommandResultKind Kind,
    string Input,
    string? ActionId = null,
    string? Message = null)
{
    public bool Success => Kind != CadCommandResultKind.Failed;
    public string? MessageKey { get; init; }
    public IReadOnlyList<object?> MessageArguments { get; init; } = [];
}

public sealed class CadCommandManager
{
    private const int HistoryLimit = 100;
    private static readonly ConditionalWeakTable<CadWorkspace, CadCommandManager>
        WorkspaceManagers = new();

    private readonly CadWorkspace _workspace;
    private readonly List<string> _history = [];

    private CadCommandManager(CadWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <summary>
    /// Returns the shared command session for a workspace. UI, automation and
    /// other front ends should use this entry point when command history and
    /// repeat/completion state must be consistent across surfaces.
    /// </summary>
    public static CadCommandManager ForWorkspace(CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return WorkspaceManagers.GetValue(
            workspace,
            static value => new CadCommandManager(value));
    }

    public IReadOnlyList<string> History => _history;

    public IEnumerable<string> Complete(string prefix)
    {
        var normalized = prefix?.Trim() ?? string.Empty;
        return _workspace.Actions.Commands.SelectMany(command => command.Aliases.Append(command.Id)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(value => value.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
    }

    public CadCommandResult Execute(string? text)
    {
        var input = text?.Trim() ?? string.Empty;
        if (input.Length == 0)
        {
            if (_workspace.Tools.ActiveTool is { } tool)
            {
                var acceptsCurrentStep = tool.CanCommitCurrentStage;
                var canFinish = tool.CanFinish;
                var success = _workspace.Tools.SubmitCurrent();
                return success
                    ? Result(
                        CadCommandResultKind.InputApplied,
                        input,
                        message: acceptsCurrentStep
                            ? "Accept"
                            : canFinish
                                ? "Finish"
                                : null)
                    : Result(
                        CadCommandResultKind.Failed,
                        input,
                        message: "The current tool stage requires input.");
            }

            return _workspace.Actions.ExecuteLast()
                ? Result(CadCommandResultKind.Repeated, input)
                : Result(CadCommandResultKind.Failed, input, message: "No command to repeat.");
        }

        AddHistory(input);

        if (_workspace.Tools.ActiveTool is { } activeTool &&
            TryExecuteToolInput(activeTool, input, out var toolResult))
            return toolResult;

        if (_workspace.Actions.ResolveCommand(input) is not { } command)
            return Result(
                CadCommandResultKind.Failed,
                input,
                message: $"Unknown command: {input}",
                messageKey: "Cad.Command.Unknown",
                messageArguments: [input]);

        var actionId = command.Id;
        if (_workspace.Actions.Find(actionId) is null)
            return Result(
                CadCommandResultKind.Failed,
                input,
                actionId,
                $"Command is not available: {input}",
                "Cad.Command.Unavailable",
                input);

        return _workspace.Actions.Execute(actionId)
            ? Result(CadCommandResultKind.Executed, input, actionId)
            : Result(
                CadCommandResultKind.Failed,
                input,
                actionId,
                $"Command is not available: {input}",
                "Cad.Command.Unavailable",
                input);
    }

    private bool TryExecuteToolInput(CadTool tool, string input, out CadCommandResult result)
    {
        if (EqualsAny(input, "ESC", "CANCEL"))
        {
            _workspace.Tools.CancelCurrent();
            result = Result(CadCommandResultKind.Canceled, input, message: "Cancel");
            return true;
        }

        if (EqualsAny(input, "FINISH", "DONE"))
        {
            var success = _workspace.Tools.FinishCurrent();
            result = Result(
                success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed,
                input,
                message: success ? "Finish" : "The current tool cannot finish yet.");
            return true;
        }

        if (EqualsAny(input, "U", "BACK", "STEPBACK"))
        {
            var success = _workspace.Tools.StepBackCurrent();
            result = Result(
                success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed,
                input,
                message: success ? "Step back" : "There is no previous stage.");
            return true;
        }

        if (tool is ICadCommandOptionTool optionTool &&
            optionTool.TryExecuteOption(input, out var optionSuccess, out var optionMessage))
        {
            result = Result(
                optionSuccess ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed,
                input,
                message: optionMessage);
            return true;
        }

        if (input.Equals("O", StringComparison.OrdinalIgnoreCase) && tool.CurrentStep.InputKind == CadToolInputKind.Point)
        {
            _workspace.Tools.BeginOffsetInput();
            result = Result(CadCommandResultKind.InputApplied, input,
                message: "Pick offset base point, then enter the relative displacement.",
                messageKey: "Cad.Command.OffsetOrigin");
            return true;
        }

        if (TryPoint(tool, input, out result))
            return true;

        var equals = input.IndexOf('=');
        if (equals > 0 && equals < input.Length - 1)
        {
            var id = input[..equals].Trim();
            var value = input[(equals + 1)..].Trim();
            var success = tool.TrySetParameter(id, value);
            result = Result(
                success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed,
                input,
                message: success ? null : $"Unsupported parameter: {id}",
                messageKey: success ? null : "Cad.Command.UnsupportedParameter",
                messageArguments: [id]);
            return true;
        }

        if (TryPrecision(tool, input, out result))
            return true;

        return false;
    }

    private bool TryPoint(CadTool tool, string input, out CadCommandResult result)
    {
        if (tool.CurrentStep.InputKind != CadToolInputKind.Point ||
            !LooksLikePointInput(input))
        {
            result = default;
            return false;
        }

        var success = _workspace.Tools.SubmitPointText(input);
        result = Result(
            success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed,
            input,
            message: success ? null : "The current tool stage does not accept point input.");
        return true;
    }

    private static bool LooksLikePointInput(string input) =>
        input.Contains(',', StringComparison.Ordinal) ||
        input.Contains('<', StringComparison.Ordinal) ||
        input.StartsWith('@');

    private bool TryPrecision(CadTool tool, string input, out CadCommandResult result)
    {
        var parts = input.Split(
            [' ', '\t'],
            2,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var prefix = parts.Length == 2 ? parts[0] : string.Empty;
        var valueText = parts.Length == 2 ? parts[1] : input;
        if (!TryParseDouble(valueText, out var value))
        {
            result = default;
            return false;
        }

        var allowed = tool.PrecisionInputs;
        CadPrecisionInput precision;
        if (prefix.Length == 0 && allowed == CadPrecisionInputKind.Angle)
            precision = new CadPrecisionInput(AngleDegrees: value);
        else if (prefix.Length == 0 && allowed == CadPrecisionInputKind.Factor)
            precision = new CadPrecisionInput(Factor: value);
        else if (prefix.Equals("A", StringComparison.OrdinalIgnoreCase) ||
                 prefix.Equals("ANGLE", StringComparison.OrdinalIgnoreCase))
            precision = new CadPrecisionInput(AngleDegrees: value);
        else if (prefix.Equals("F", StringComparison.OrdinalIgnoreCase) ||
                 prefix.Equals("FACTOR", StringComparison.OrdinalIgnoreCase))
            precision = new CadPrecisionInput(Factor: value);
        else if (prefix.Length == 0 ||
                 prefix.Equals("L", StringComparison.OrdinalIgnoreCase) ||
                 prefix.Equals("LENGTH", StringComparison.OrdinalIgnoreCase))
            precision = new CadPrecisionInput(Length: value);
        else
        {
            result = default;
            return false;
        }

        var requested = precision.AngleDegrees is not null
            ? CadPrecisionInputKind.Angle
            : precision.Factor is not null
                ? CadPrecisionInputKind.Factor
                : CadPrecisionInputKind.Length;
        if ((allowed & requested) == 0)
        {
            result = Result(
                CadCommandResultKind.Failed,
                input,
                message: "This input is not valid for the current tool stage.");
            return true;
        }

        try
        {
            var success = _workspace.Precision.Apply(tool, precision);
            result = Result(
                success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed,
                input,
                message: success ? null : "Precision input was rejected.");
        }
        catch (Exception exception)
        {
            result = Result(CadCommandResultKind.Failed, input, message: exception.Message);
        }
        return true;
    }

    private static bool TryParseDouble(string text, out double value) =>
        CadValueTextConverter.TryParseFiniteDouble(text, out value);

    private void AddHistory(string input)
    {
        _history.Add(input);
        if (_history.Count > HistoryLimit)
            _history.RemoveRange(0, _history.Count - HistoryLimit);
    }

    private static bool EqualsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Equals(candidate, StringComparison.OrdinalIgnoreCase));

    private static CadCommandResult Result(
        CadCommandResultKind kind,
        string input,
        string? actionId = null,
        string? message = null,
        string? messageKey = null,
        params object?[] messageArguments) =>
        new(kind, input, actionId, message)
        {
            MessageKey = messageKey ?? message switch
            {
                "Finish" => "Cad.Text.Finish",
                "Accept" => "Cad.Text.Accept",
                "Cancel" => "Cad.Text.Cancel",
                "Close" => "Cad.Command.Close",
                "Step back" => "Cad.Command.StepBack",
                "The current tool stage requires input." => "Cad.Command.InputRequired",
                "No command to repeat." => "Cad.Command.NoRepeat",
                "The current tool cannot finish yet." => "Cad.Command.CannotFinish",
                "There is no previous stage." => "Cad.Command.NoPreviousStage",
                "Coordinate values must be finite numbers." => "Cad.Command.InvalidCoordinates",
                "Relative coordinates require a reference point in the current tool stage." => "Cad.Command.RelativeReferenceRequired",
                "The current tool stage does not accept point input." => "Cad.Command.PointRejected",
                "This input is not valid for the current tool stage." => "Cad.Command.InvalidStageInput",
                "Precision input was rejected." => "Cad.Command.PrecisionRejected",
                "Polyline requires at least three points before Close." => "Cad.Command.PolylineCloseRequiresThree",
                _ => null
            },
            MessageArguments = messageArguments
        };
}
