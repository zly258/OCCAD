using System.Globalization;

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
    private readonly CadWorkspace _workspace;
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _history = [];

    public CadCommandManager(CadWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        RegisterDefaults();
    }

    public IReadOnlyList<string> History => _history;

    public IEnumerable<string> Complete(string prefix)
    {
        var normalized = prefix?.Trim() ?? string.Empty;
        return _aliases.Keys
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
                var acceptsCurrentStep =
                    tool.CanCommitCurrentStage &&
                    !tool.CurrentStep.RequiresPointer;
                var success = _workspace.Tools.SubmitCurrent();
                return success
                    ? Result(
                        CadCommandResultKind.InputApplied,
                        input,
                        message: acceptsCurrentStep ? "Accept" : "Finish")
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

        if (!_aliases.TryGetValue(input, out var actionId))
            return Result(CadCommandResultKind.Failed, input, message: $"Unknown command: {input}", messageKey: "Cad.Command.Unknown", messageArguments: [input]);

        return _workspace.Actions.Execute(actionId)
            ? Result(CadCommandResultKind.Executed, input, actionId)
            : Result(CadCommandResultKind.Failed, input, actionId, $"Command is not available: {input}", "Cad.Command.Unavailable", input);
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
            result = Result(success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed, input, message: success ? "Finish" : "The current tool cannot finish yet.");
            return true;
        }

        if (EqualsAny(input, "U", "BACK", "STEPBACK"))
        {
            var success = _workspace.Tools.StepBackCurrent();
            result = Result(success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed, input, message: success ? "Step back" : "There is no previous stage.");
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

        if (TryPoint(tool, input, out result))
            return true;

        var equals = input.IndexOf('=');
        if (equals > 0 && equals < input.Length - 1)
        {
            var id = input[..equals].Trim();
            var value = input[(equals + 1)..].Trim();
            var success = tool.TrySetParameter(id, value);
            result = Result(success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed, input, message: success ? null : $"Unsupported parameter: {id}", messageKey: success ? null : "Cad.Command.UnsupportedParameter", messageArguments: [id]);
            return true;
        }

        if (TryPrecision(tool, input, out result))
            return true;

        return false;
    }

    private bool TryPoint(CadTool tool, string input, out CadCommandResult result)
    {
        var relative = input.StartsWith('@');
        var valueText = relative ? input[1..].Trim() : input;
        var parts = valueText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (2 or 3))
        {
            result = default;
            return false;
        }

        var values = new double[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!TryParseDouble(parts[index], out values[index]))
            {
                result = Result(CadCommandResultKind.Failed, input, message: "Coordinate values must be finite numbers.");
                return true;
            }
        }

        var reference = tool.PrecisionReferencePoint;
        if (relative && reference is null)
        {
            result = Result(
                CadCommandResultKind.Failed,
                input,
                message: "Relative coordinates require a reference point in the current tool stage.");
            return true;
        }

        var frame = relative
            ? _workspace.WorkPlane.EffectivePlane
            : _workspace.WorkPlane.UserPlane;
        var origin = relative
            ? reference!.Value
            : frame.Origin;
        var point =
            origin +
            frame.XAxis * values[0] +
            frame.YAxis * values[1];
        if (parts.Length == 3)
            point += frame.Normal * values[2];

        var success = point.IsFinite && _workspace.Tools.CommitPoint(point);
        result = Result(
            success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed,
            input,
            message: success ? null : "The current tool stage does not accept point input.");
        return true;
    }

    private bool TryPrecision(CadTool tool, string input, out CadCommandResult result)
    {
        var parts = input.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
        else if (prefix.Equals("A", StringComparison.OrdinalIgnoreCase) || prefix.Equals("ANGLE", StringComparison.OrdinalIgnoreCase))
            precision = new CadPrecisionInput(AngleDegrees: value);
        else if (prefix.Equals("F", StringComparison.OrdinalIgnoreCase) || prefix.Equals("FACTOR", StringComparison.OrdinalIgnoreCase))
            precision = new CadPrecisionInput(Factor: value);
        else if (prefix.Length == 0 || prefix.Equals("L", StringComparison.OrdinalIgnoreCase) || prefix.Equals("LENGTH", StringComparison.OrdinalIgnoreCase))
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
            result = Result(CadCommandResultKind.Failed, input, message: "This input is not valid for the current tool stage.");
            return true;
        }

        try
        {
            var success = _workspace.Precision.Apply(tool, precision);
            result = Result(success ? CadCommandResultKind.InputApplied : CadCommandResultKind.Failed, input, message: success ? null : "Precision input was rejected.");
        }
        catch (Exception exception)
        {
            result = Result(CadCommandResultKind.Failed, input, message: exception.Message);
        }
        return true;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        var parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                     double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return parsed && double.IsFinite(value);
    }

    private void RegisterDefaults()
    {
        Alias("DIMANGULAR", "annotation.angledimension", "DAN");
        Alias("DIMRADIUS", "annotation.radiusdimension", "DRA");
        Alias("DIMDIAMETER", "annotation.diameterdimension", "DDI");
        Alias("DIMLINEAR", "annotation.lengthdimension", "DLI", "DIM");
        Alias("DISTANCE", "measure.distance", "DIST", "DI");
        Alias("TEXT", "draw.text", "T");
        Alias("POINT", "draw.point", "PO");
        Alias("LINE", "draw.line", "L");
        Alias("POLYLINE", "draw.polyline", "PL");
        Alias("RECTANGLE", "draw.rectangle", "REC");
        Alias("POLYGON", "draw.polygon", "PG");
        Alias("REGULARPOLYGON", "draw.regularpolygon", "RPG");
        Alias("CIRCLE", "draw.circle", "C");
        Alias("ARC", "draw.arc", "A");
        Alias("ELLIPSE", "draw.ellipse", "EL");
        Alias("SPLINE", "draw.spline", "SPL");
        Alias("BOX", "solid.box", "B");
        Alias("CYLINDER", "solid.cylinder", "CYL");
        Alias("CONE", "solid.cone", "CN");
        Alias("FRUSTUM", "solid.frustum", "FR");
        Alias("SPHERE", "solid.sphere", "SPH");
        Alias("ELLIPSOID", "solid.ellipsoid", "ELL");
        Alias("HELIX", "solid.helix", "HEL");
        Alias("TORUS", "solid.torus", "TOR");
        Alias("MOVE", "modify.move", "M");
        Alias("COPY", "modify.copy", "CO");
        Alias("CIRCLEARRAY", "modify.circlearray", "ARRAYPOLAR");
        Alias("PATHARRAY", "modify.patharray", "ARRAYPATH");
        Alias("RECTARRAY", "modify.rectarray", "ARRAYRECT");
        Alias("MIRROR", "modify.mirror", "MI");
        Alias("PATHREVERSE", "modify.path.reverse", "PREVERSE");
        Alias("PATHOPEN", "modify.path.open", "POPEN");
        Alias("PATHCLOSE", "modify.path.close", "PCLOSE");
        Alias("PATHREMOVESEGMENT", "modify.path.removesegment", "PREMOVE");
        Alias("PATHSPLIT", "modify.path.splitsegment", "PSPLIT");
        Alias("OFFSET", "modify.offset", "O");
        Alias("TRIM", "modify.trim", "TR");
        Alias("EXTEND", "modify.extend", "EX");
        Alias("JOIN", "modify.join", "J");
        Alias("BREAK", "modify.break", "BR");
        Alias("FILLET", "modify.fillet", "F");
        Alias("CHAMFER", "modify.chamfer", "CHA");
        Alias("REGION", "model.region", "REG");
        Alias("EXTRUDE", "solid.extrude", "EXT");
        Alias("REVOLVE", "solid.revolve", "REV");
        Alias("SWEEP", "solid.sweep", "SW");
        Alias("LOFT", "solid.loft", "LO");
        Alias("UNION", "solid.boolean.union");
        Alias("SUBTRACT", "solid.boolean.cut", "CUT");
        Alias("INTERSECT", "solid.boolean.common");
        Alias("EDGEFILLET", "solid.edgefillet", "EF");
        Alias("EDGECHAMFER", "solid.edgechamfer", "EC");
        Alias("SHELL", "solid.shell");
        Alias("SHAPEOFFSET", "solid.shapeoffset", "SOFF");
        Alias("ROTATE", "modify.rotate", "RO");
        Alias("SCALE", "modify.scale", "SC");
        Alias("UNDO", "edit.undo", "U");
        Alias("REDO", "edit.redo");
        Alias("DELETE", "edit.delete", "ERASE");
        Alias("SELECT", "select");
        Alias("SELECTALL", "select.all");
        Alias("FIT", "view.fit", "ZE");
        Alias("ISOMETRIC", "view.isometric", "ISO");
        Alias("TOP", "view.top");
        Alias("BOTTOM", "view.bottom");
        Alias("FRONT", "view.front");
        Alias("BACK", "view.back");
        Alias("LEFT", "view.left");
        Alias("RIGHT", "view.right");
        Alias("WIREFRAME", "display.wireframe", "WF");
        Alias("SHADED", "display.shaded", "SHADE");
        Alias("HIDE", "view.hide");
        Alias("ISOLATE", "view.isolate");
        Alias("SHOWALL", "view.showall");
    }

    private void Alias(string command, string actionId, params string[] aliases)
    {
        _aliases.Add(command, actionId);
        foreach (var alias in aliases)
            _aliases.Add(alias, actionId);
    }

    private void AddHistory(string input)
    {
        _history.Add(input);
        if (_history.Count > HistoryLimit)
            _history.RemoveRange(0, _history.Count - HistoryLimit);
    }

    private static bool EqualsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Equals(candidate, StringComparison.OrdinalIgnoreCase));

    private static CadCommandResult Result(
        CadCommandResultKind kind, string input, string? actionId = null,
        string? message = null, string? messageKey = null, params object?[] messageArguments) =>
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




