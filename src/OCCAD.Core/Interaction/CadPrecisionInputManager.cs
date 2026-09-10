namespace OCCAD;

public readonly record struct CadPrecisionInput(
    double? Length = null,
    double? AngleDegrees = null,
    double? Factor = null);

public sealed class CadPrecisionInputManager
{
    private readonly CadDraftingSettings _drafting;
    private readonly CadTrackingManager _tracking;

    public CadPrecisionInputManager(
        CadDraftingSettings drafting,
        CadTrackingManager tracking)
    {
        _drafting =
            drafting ?? throw new ArgumentNullException(nameof(drafting));
        _tracking =
            tracking ?? throw new ArgumentNullException(nameof(tracking));
    }

    public double? Factor { get; private set; }

    public bool Apply(
        CadTool? tool,
        CadPrecisionInput input)
    {
        if (tool is null ||
            tool.PrecisionInputs == CadPrecisionInputKind.None)
            return false;

        var kinds = tool.PrecisionInputs;
        var lengthAllowed =
            (kinds & CadPrecisionInputKind.Length) != 0;
        var angleAllowed =
            (kinds & CadPrecisionInputKind.Angle) != 0;
        var factorAllowed =
            (kinds & CadPrecisionInputKind.Factor) != 0;
        // Validate the whole input before changing any lock. A rejected angle or
        // factor must not leave a previously applied length behind.
        if ((input.Length is not null && !lengthAllowed) ||
            (input.AngleDegrees is not null && !angleAllowed) ||
            (input.Factor is not null && !factorAllowed)) return false;
        if (input.Length is { } proposedLength && (!double.IsFinite(proposedLength) || proposedLength <= 0.0))
            throw new ArgumentOutOfRangeException(nameof(input), "Length must be finite and greater than zero.");
        if (input.AngleDegrees is { } proposedAngle && !double.IsFinite(proposedAngle))
            throw new ArgumentOutOfRangeException(nameof(input), "Angle must be finite.");
        if (input.Factor is { } proposedFactor && (!double.IsFinite(proposedFactor) || proposedFactor <= 0.0))
            throw new ArgumentOutOfRangeException(nameof(input), "Scale factor must be finite and greater than zero.");
        var hasValue =
            input.Length is not null ||
            input.AngleDegrees is not null ||
            input.Factor is not null;

        if (!hasValue)
        {
            if (lengthAllowed) ClearLength();
            if (angleAllowed) ClearAngle();
            if (factorAllowed) Factor = null;
            _tracking.Clear();
            return tool.ApplyPrecisionInput(input);
        }

        if (input.Length is { } length)
        {
            if (!lengthAllowed) return false;
            ApplyLength(length);
        }

        if (input.AngleDegrees is { } angle)
        {
            if (!angleAllowed) return false;
            ApplyAngle(angle);
        }

        if (input.Factor is { } factor)
        {
            if (!factorAllowed) return false;
            ApplyFactor(factor);
        }

        _tracking.Clear();
        return tool.ApplyPrecisionInput(input);
    }

    public void ResetFactor() =>
        Factor = null;

    private void ApplyLength(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Length must be finite and greater than zero.");

        _drafting.LockedLength = value;
        _drafting.LengthLockEnabled = true;
    }

    private void ApplyAngle(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Angle must be finite.");

        _drafting.AxisLockEnabled = false;
        _drafting.LockedAngleDegrees = value;
        _drafting.AngleLockEnabled = true;
    }

    private void ApplyFactor(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Scale factor must be finite and greater than zero.");

        Factor = value;
    }

    private void ClearLength()
    {
        _drafting.LengthLockEnabled = false;
        _drafting.LockedLength = 0.0;
    }

    private void ClearAngle()
    {
        _drafting.AngleLockEnabled = false;
        _drafting.LockedAngleDegrees = 0.0;
    }
}
