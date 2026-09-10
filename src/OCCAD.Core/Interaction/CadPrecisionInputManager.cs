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

        // Validate the whole input before changing any lock. A rejected value
        // must not partially mutate the drafting state.
        if ((input.Length is not null && !lengthAllowed) ||
            (input.AngleDegrees is not null && !angleAllowed) ||
            (input.Factor is not null && !factorAllowed))
            return false;

        if (input.Length is { } proposedLength &&
            (!double.IsFinite(proposedLength) ||
             proposedLength <= 0.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                "Length must be finite and greater than zero.");
        }

        if (input.AngleDegrees is { } proposedAngle &&
            !double.IsFinite(proposedAngle))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                "Angle must be finite.");
        }

        if (input.Factor is { } proposedFactor &&
            (!double.IsFinite(proposedFactor) ||
             proposedFactor <= 0.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                "Scale factor must be finite and greater than zero.");
        }

        var previousAxisLock =
            _drafting.AxisLockEnabled;
        var previousLengthLock =
            _drafting.LengthLockEnabled;
        var previousLength =
            _drafting.LockedLength;
        var previousAngleLock =
            _drafting.AngleLockEnabled;
        var previousAngle =
            _drafting.LockedAngleDegrees;
        var previousFactor =
            Factor;

        try
        {
            var hasValue =
                input.Length is not null ||
                input.AngleDegrees is not null ||
                input.Factor is not null;

            if (!hasValue)
            {
                if (lengthAllowed)
                    ClearLength();
                if (angleAllowed)
                    ClearAngle();
                if (factorAllowed)
                    Factor = null;
            }
            else
            {
                if (input.Length is { } length)
                    ApplyLength(length);
                if (input.AngleDegrees is { } angle)
                    ApplyAngle(angle);
                if (input.Factor is { } factor)
                    ApplyFactor(factor);
            }

            _tracking.Clear();
            if (tool.ApplyPrecisionInput(input))
                return true;

            RestorePreviousState();
            return false;
        }
        catch
        {
            RestorePreviousState();
            throw;
        }

        void RestorePreviousState()
        {
            _drafting.AxisLockEnabled =
                previousAxisLock;
            _drafting.LockedLength =
                previousLength;
            _drafting.LengthLockEnabled =
                previousLengthLock;
            _drafting.LockedAngleDegrees =
                previousAngle;
            _drafting.AngleLockEnabled =
                previousAngleLock;
            Factor =
                previousFactor;
            _tracking.Clear();
        }
    }

    public bool ClearLock(
        CadTool? tool,
        CadPrecisionInputKind kind)
    {
        if (tool is null ||
            kind is not (CadPrecisionInputKind.Length or
                CadPrecisionInputKind.Angle or
                CadPrecisionInputKind.Factor) ||
            (tool.PrecisionInputs & kind) == 0)
            return false;

        var previousAxisLock =
            _drafting.AxisLockEnabled;
        var previousLengthLock =
            _drafting.LengthLockEnabled;
        var previousLength =
            _drafting.LockedLength;
        var previousAngleLock =
            _drafting.AngleLockEnabled;
        var previousAngle =
            _drafting.LockedAngleDegrees;
        var previousFactor =
            Factor;

        try
        {
            switch (kind)
            {
                case CadPrecisionInputKind.Length:
                    ClearLength();
                    break;
                case CadPrecisionInputKind.Angle:
                    ClearAngle();
                    break;
                case CadPrecisionInputKind.Factor:
                    Factor = null;
                    break;
            }

            _tracking.Clear();
            if (tool.ApplyPrecisionInput(default))
                return true;

            RestorePreviousState();
            return false;
        }
        catch
        {
            RestorePreviousState();
            throw;
        }

        void RestorePreviousState()
        {
            _drafting.AxisLockEnabled =
                previousAxisLock;
            _drafting.LockedLength =
                previousLength;
            _drafting.LengthLockEnabled =
                previousLengthLock;
            _drafting.LockedAngleDegrees =
                previousAngle;
            _drafting.AngleLockEnabled =
                previousAngleLock;
            Factor =
                previousFactor;
            _tracking.Clear();
        }
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
