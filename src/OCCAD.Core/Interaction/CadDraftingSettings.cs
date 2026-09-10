using OcctNet;

namespace OCCAD;

public enum CadTrackingKind
{
    Orthogonal,
    Polar
}

public readonly record struct CadTrackingResult(
    OcctPoint3d Reference,
    OcctPoint3d Point,
    CadTrackingKind Kind,
    double AngleDegrees);

/// <summary>
/// Drafting constraints and tracking policy. This state is intentionally
/// independent from the geometric work-plane frame.
/// </summary>
public sealed class CadDraftingSettings
{
    private bool _axisLockEnabled;
    private bool _lengthLockEnabled;
    private double _lockedLength;
    private bool _angleLockEnabled;
    private double _lockedAngleDegrees;
    private bool _orthogonalTrackingEnabled = true;
    private bool _polarTrackingEnabled;
    private double _polarIncrementDegrees = 45.0;
    private double _trackingToleranceDegrees = 2.0;

    public bool AxisLockEnabled
    {
        get => _axisLockEnabled;
        set => Set(ref _axisLockEnabled, value);
    }

    public bool LengthLockEnabled
    {
        get => _lengthLockEnabled;
        set => Set(ref _lengthLockEnabled, value);
    }

    public double LockedLength
    {
        get => _lockedLength;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Locked length must be finite and non-negative.");
            Set(ref _lockedLength, value);
        }
    }

    public bool AngleLockEnabled
    {
        get => _angleLockEnabled;
        set => Set(ref _angleLockEnabled, value);
    }

    public double LockedAngleDegrees
    {
        get => _lockedAngleDegrees;
        set
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Locked angle must be finite.");
            Set(ref _lockedAngleDegrees, value);
        }
    }

    public bool OrthogonalTrackingEnabled
    {
        get => _orthogonalTrackingEnabled;
        set => Set(ref _orthogonalTrackingEnabled, value);
    }

    public bool PolarTrackingEnabled
    {
        get => _polarTrackingEnabled;
        set => Set(ref _polarTrackingEnabled, value);
    }

    public double PolarIncrementDegrees
    {
        get => _polarIncrementDegrees;
        set
        {
            if (!double.IsFinite(value) ||
                value <= 0.0 ||
                value > 180.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Polar increment must be between 0 and 180 degrees.");
            }

            Set(ref _polarIncrementDegrees, value);
        }
    }

    public double TrackingToleranceDegrees
    {
        get => _trackingToleranceDegrees;
        set
        {
            if (!double.IsFinite(value) ||
                value <= 0.0 ||
                value > 45.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Tracking tolerance must be between 0 and 45 degrees.");
            }

            Set(ref _trackingToleranceDegrees, value);
        }
    }

    public event EventHandler? Changed;

    public CadTrackingResult? Track(
        CadWorkPlane workPlane,
        OcctPoint3d reference,
        OcctPoint3d point)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!reference.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(reference));
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(point));

        if (!workPlane.IsActive ||
            (!OrthogonalTrackingEnabled &&
             !PolarTrackingEnabled) ||
            AxisLockEnabled ||
            AngleLockEnabled)
        {
            return null;
        }

        var a = workPlane.WorldToLocal(reference);
        var b = workPlane.WorldToLocal(point);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 1e-12)
            return null;

        var angleDegrees =
            Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var bestDelta = double.PositiveInfinity;
        var bestAngle = 0.0;
        var bestKind = CadTrackingKind.Orthogonal;

        if (OrthogonalTrackingEnabled)
        {
            Consider(
                CadTrackingKind.Orthogonal,
                Math.Round(angleDegrees / 90.0) * 90.0);
        }

        if (PolarTrackingEnabled)
        {
            Consider(
                CadTrackingKind.Polar,
                Math.Round(
                    angleDegrees /
                    PolarIncrementDegrees) *
                PolarIncrementDegrees);
        }

        if (bestDelta > TrackingToleranceDegrees)
            return null;

        var radians = bestAngle * Math.PI / 180.0;
        var directionX = Math.Cos(radians);
        var directionY = Math.Sin(radians);
        var projectedLength =
            dx * directionX + dy * directionY;
        var tracked = workPlane.LocalToWorld(
            new CadPlanePoint(
                a.X + directionX * projectedLength,
                a.Y + directionY * projectedLength));

        return new CadTrackingResult(
            reference,
            tracked,
            bestKind,
            NormalizeAngleDegrees(bestAngle));

        void Consider(
            CadTrackingKind kind,
            double candidateAngle)
        {
            var delta = Math.Abs(
                AngleDeltaDegrees(
                    angleDegrees,
                    candidateAngle));
            if (delta >= bestDelta)
                return;

            bestDelta = delta;
            bestAngle = candidateAngle;
            bestKind = kind;
        }
    }

    public OcctPoint3d Constrain(
        CadWorkPlane workPlane,
        OcctPoint3d reference,
        OcctPoint3d point)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!reference.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(reference));
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(point));

        if (!AxisLockEnabled &&
            !LengthLockEnabled &&
            !AngleLockEnabled)
        {
            return point;
        }

        var a = workPlane.WorldToLocal(reference);
        var b = workPlane.WorldToLocal(point);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;

        if (AxisLockEnabled &&
            !AngleLockEnabled)
        {
            if (Math.Abs(dx) >= Math.Abs(dy))
                dy = 0.0;
            else
                dx = 0.0;
        }

        var angle = Math.Atan2(dy, dx);
        if (AngleLockEnabled)
        {
            angle =
                LockedAngleDegrees *
                Math.PI / 180.0;
            var directionX = Math.Cos(angle);
            var directionY = Math.Sin(angle);
            var projectedLength =
                dx * directionX + dy * directionY;
            dx = directionX * projectedLength;
            dy = directionY * projectedLength;
        }

        if (LengthLockEnabled)
        {
            if (!double.IsFinite(LockedLength) ||
                LockedLength <= 0.0)
            {
                throw new InvalidOperationException(
                    "Locked length must be finite and greater than zero.");
            }

            var current = Math.Sqrt(dx * dx + dy * dy);
            if (current <= 1e-12)
            {
                dx = Math.Cos(angle) * LockedLength;
                dy = Math.Sin(angle) * LockedLength;
            }
            else
            {
                var factor = LockedLength / current;
                dx *= factor;
                dy *= factor;
            }
        }

        return workPlane.LocalToWorld(
            new CadPlanePoint(
                a.X + dx,
                a.Y + dy));
    }

    public void ResetTransientLocks()
    {
        var changed =
            _lengthLockEnabled ||
            _angleLockEnabled ||
            _lockedLength != 0.0 ||
            _lockedAngleDegrees != 0.0;

        _lengthLockEnabled = false;
        _angleLockEnabled = false;
        _lockedLength = 0.0;
        _lockedAngleDegrees = 0.0;

        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ResetLocks()
    {
        var changed = _axisLockEnabled;
        _axisLockEnabled = false;

        var transientChanged =
            _lengthLockEnabled ||
            _angleLockEnabled ||
            _lockedLength != 0.0 ||
            _lockedAngleDegrees != 0.0;

        _lengthLockEnabled = false;
        _angleLockEnabled = false;
        _lockedLength = 0.0;
        _lockedAngleDegrees = 0.0;

        if (changed || transientChanged)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static double AngleDeltaDegrees(
        double left,
        double right)
    {
        var delta =
            NormalizeAngleDegrees(left - right);
        return delta > 180.0
            ? delta - 360.0
            : delta;
    }

    private static double NormalizeAngleDegrees(
        double angle)
    {
        var normalized = angle % 360.0;
        return normalized < 0.0
            ? normalized + 360.0
            : normalized;
    }
}
