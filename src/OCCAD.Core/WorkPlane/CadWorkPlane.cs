using OcctNet;

namespace OCCAD;

public enum CadWorkPlanePreset { XY, YZ, XZ, Custom }
public readonly record struct CadPlanePoint(double X, double Y);

public readonly record struct CadPlaneFrame(
    OcctPoint3d Origin,
    OcctVector3d XAxis,
    OcctVector3d YAxis,
    OcctVector3d Normal,
    CadWorkPlanePreset Preset);

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

public sealed class CadWorkPlane
{
    private CadPlaneFrame _userPlane;
    private CadPlaneFrame? _toolPlane;
    private CadPlaneFrame? _gripPlane;
    private bool _userPlaneLocked;
    private bool _toolPlaneFixed;
    private bool _gripPlaneFixed;
    private bool _axisLockEnabled;
    private bool _lengthLockEnabled;
    private double _lockedLength;
    private bool _angleLockEnabled;
    private double _lockedAngleDegrees;
    private bool _orthogonalTrackingEnabled = true;
    private bool _polarTrackingEnabled;
    private double _polarIncrementDegrees = 45.0;
    private double _trackingToleranceDegrees = 2.0;

    public CadWorkPlane()
    {
        _userPlane = CreatePresetFrame(
            CadWorkPlanePreset.XY,
            OcctPoint3d.Origin);
    }

    public bool IsActive => _toolPlane is not null || _gripPlane is not null;
    public bool UserPlaneLocked => _userPlaneLocked;
    public bool ToolPlaneFixed => _toolPlaneFixed;
    public bool GripPlaneFixed => _gripPlaneFixed;
    public bool EffectivePlaneFixed =>
        _gripPlane is not null
            ? _gripPlaneFixed
            : _toolPlane is not null
                ? _toolPlaneFixed
                : _userPlaneLocked;

    // Existing callers can still query the effective lock. New code should use
    // UserPlaneLocked / ToolPlaneFixed / GripPlaneFixed explicitly.
    public bool IsPlaneLocked => EffectivePlaneFixed;

    public CadPlaneFrame UserPlane => _userPlane;
    public CadPlaneFrame? ToolPlane => _toolPlane;
    public CadPlaneFrame? GripPlane => _gripPlane;
    public CadPlaneFrame EffectivePlane =>
        _gripPlane ?? _toolPlane ?? _userPlane;

    // Preset describes the persistent user plane. A temporary Tool/Grip frame
    // may independently be Custom.
    public CadWorkPlanePreset Preset => _userPlane.Preset;
    public CadWorkPlanePreset EffectivePreset => EffectivePlane.Preset;
    public OcctPoint3d Origin => EffectivePlane.Origin;
    public OcctVector3d XAxis => EffectivePlane.XAxis;
    public OcctVector3d YAxis => EffectivePlane.YAxis;
    public OcctVector3d Normal => EffectivePlane.Normal;

    public bool AxisLockEnabled
    {
        get => _axisLockEnabled;
        set
        {
            if (_axisLockEnabled == value) return;
            _axisLockEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool LengthLockEnabled
    {
        get => _lengthLockEnabled;
        set
        {
            if (_lengthLockEnabled == value) return;
            _lengthLockEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double LockedLength
    {
        get => _lockedLength;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Locked length must be finite and non-negative.");
            if (_lockedLength.Equals(value)) return;
            _lockedLength = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool AngleLockEnabled
    {
        get => _angleLockEnabled;
        set
        {
            if (_angleLockEnabled == value) return;
            _angleLockEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double LockedAngleDegrees
    {
        get => _lockedAngleDegrees;
        set
        {
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Locked angle must be finite.");
            if (_lockedAngleDegrees.Equals(value)) return;
            _lockedAngleDegrees = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // Tracking switches remain drafting state for now, but no longer participate
    // in plane-frame lifetime or restoration.
    public bool OrthogonalTrackingEnabled
    {
        get => _orthogonalTrackingEnabled;
        set
        {
            if (_orthogonalTrackingEnabled == value) return;
            _orthogonalTrackingEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool PolarTrackingEnabled
    {
        get => _polarTrackingEnabled;
        set
        {
            if (_polarTrackingEnabled == value) return;
            _polarTrackingEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double PolarIncrementDegrees
    {
        get => _polarIncrementDegrees;
        set
        {
            if (!double.IsFinite(value) || value <= 0.0 || value > 180.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Polar increment must be between 0 and 180 degrees.");
            if (_polarIncrementDegrees.Equals(value)) return;
            _polarIncrementDegrees = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double TrackingToleranceDegrees
    {
        get => _trackingToleranceDegrees;
        set
        {
            if (!double.IsFinite(value) || value <= 0.0 || value > 45.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Tracking tolerance must be between 0 and 45 degrees.");
            if (_trackingToleranceDegrees.Equals(value)) return;
            _trackingToleranceDegrees = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;

    public void BeginToolPlane(OcctPoint3d origin)
    {
        if (!origin.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(origin));

        _toolPlane = WithOrigin(_userPlane, origin);
        _toolPlaneFixed = false;
        _gripPlane = null;
        _gripPlaneFixed = false;
        ResetTransientLocksCore();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void EndToolPlane()
    {
        if (_toolPlane is null &&
            _gripPlane is null &&
            !LengthLockEnabled &&
            !AngleLockEnabled)
            return;

        _gripPlane = null;
        _gripPlaneFixed = false;
        _toolPlane = null;
        _toolPlaneFixed = false;
        ResetTransientLocksCore();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetUserPlaneLocked(bool value)
    {
        if (_userPlaneLocked == value) return;
        _userPlaneLocked = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetToolPlaneFixed(bool value)
    {
        if (_toolPlane is null || _toolPlaneFixed == value) return;
        _toolPlaneFixed = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetGripPlaneFixed(bool value)
    {
        if (_gripPlane is null || _gripPlaneFixed == value) return;
        _gripPlaneFixed = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetPreset(
        CadWorkPlanePreset preset,
        OcctPoint3d? origin = null)
    {
        if (_userPlaneLocked) return;
        if (preset == CadWorkPlanePreset.Custom)
            throw new ArgumentException(
                "Custom work planes require explicit axes.",
                nameof(preset));

        var next = CreatePresetFrame(
            preset,
            origin ?? OcctPoint3d.Origin);
        if (_userPlane == next) return;

        _userPlane = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCustom(
        OcctPoint3d origin,
        OcctVector3d xAxis,
        OcctVector3d yAxis) =>
        SetUserCustomPlane(origin, xAxis, yAxis);

    public void SetUserCustomPlane(
        OcctPoint3d origin,
        OcctVector3d xAxis,
        OcctVector3d yAxis)
    {
        if (_userPlaneLocked) return;

        var next = CreateFrame(
            origin,
            xAxis,
            yAxis,
            CadWorkPlanePreset.Custom);
        if (_userPlane == next) return;

        _userPlane = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetToolPlane(
        OcctPoint3d origin,
        OcctVector3d xAxis,
        OcctVector3d yAxis)
    {
        if (_toolPlane is null)
            throw new InvalidOperationException(
                "No Tool work plane is active.");
        if (_toolPlaneFixed) return;

        var next = CreateFrame(
            origin,
            xAxis,
            yAxis,
            CadWorkPlanePreset.Custom);
        if (_toolPlane == next) return;

        _toolPlane = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetGripPlane(
        OcctPoint3d origin,
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        bool fixedPlane = false)
    {
        if (_toolPlane is null)
            throw new InvalidOperationException(
                "A Grip plane requires an active Tool plane.");

        var next = CreateFrame(
            origin,
            xAxis,
            yAxis,
            CadWorkPlanePreset.Custom);
        if (_gripPlane == next &&
            _gripPlaneFixed == fixedPlane)
            return;

        _gripPlane = next;
        _gripPlaneFixed = fixedPlane;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearGripPlane()
    {
        if (_gripPlane is null && !_gripPlaneFixed) return;
        _gripPlane = null;
        _gripPlaneFixed = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetOrigin(OcctPoint3d origin)
    {
        if (!origin.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(origin));

        if (_gripPlane is { } grip)
        {
            if (grip.Origin == origin) return;
            _gripPlane = WithOrigin(grip, origin);
        }
        else if (_toolPlane is { } tool)
        {
            if (tool.Origin == origin) return;
            _toolPlane = WithOrigin(tool, origin);
        }
        else
        {
            if (_userPlaneLocked ||
                _userPlane.Origin == origin)
                return;
            _userPlane = WithOrigin(_userPlane, origin);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryIntersect(
        OcctProjectionRay ray,
        out OcctPoint3d point)
    {
        if (!ray.Origin.IsFinite ||
            !ray.Direction.TryNormalize(out var direction))
        {
            point = default;
            return false;
        }

        var frame = EffectivePlane;
        var denominator = direction.Dot(frame.Normal);
        if (Math.Abs(denominator) <= 1e-12)
        {
            point = default;
            return false;
        }

        var t =
            (frame.Origin - ray.Origin).Dot(frame.Normal) /
            denominator;
        point = ray.Origin + direction * t;
        return point.IsFinite;
    }

    public CadPlanePoint WorldToLocal(OcctPoint3d point)
    {
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(point));

        var frame = EffectivePlane;
        var delta = point - frame.Origin;
        return new CadPlanePoint(
            delta.Dot(frame.XAxis),
            delta.Dot(frame.YAxis));
    }

    public OcctPoint3d LocalToWorld(CadPlanePoint point)
    {
        if (!double.IsFinite(point.X) ||
            !double.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));

        var frame = EffectivePlane;
        return frame.Origin +
               frame.XAxis * point.X +
               frame.YAxis * point.Y;
    }

    public CadTrackingResult? Track(
        OcctPoint3d reference,
        OcctPoint3d point)
    {
        if (!reference.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(reference));
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(point));
        if (!IsActive ||
            (!OrthogonalTrackingEnabled &&
             !PolarTrackingEnabled) ||
            AxisLockEnabled ||
            AngleLockEnabled)
            return null;

        var a = WorldToLocal(reference);
        var b = WorldToLocal(point);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 1e-12) return null;

        var angleDegrees =
            Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var bestDelta = double.PositiveInfinity;
        var bestAngle = 0.0;
        var bestKind = CadTrackingKind.Orthogonal;

        if (OrthogonalTrackingEnabled)
            Consider(
                CadTrackingKind.Orthogonal,
                Math.Round(angleDegrees / 90.0) * 90.0);
        if (PolarTrackingEnabled)
            Consider(
                CadTrackingKind.Polar,
                Math.Round(
                    angleDegrees /
                    PolarIncrementDegrees) *
                PolarIncrementDegrees);
        if (bestDelta > TrackingToleranceDegrees)
            return null;

        var radians = bestAngle * Math.PI / 180.0;
        var directionX = Math.Cos(radians);
        var directionY = Math.Sin(radians);
        var projectedLength =
            dx * directionX + dy * directionY;
        var tracked = LocalToWorld(
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
            if (delta >= bestDelta) return;
            bestDelta = delta;
            bestAngle = candidateAngle;
            bestKind = kind;
        }
    }

    public OcctPoint3d Constrain(
        OcctPoint3d reference,
        OcctPoint3d point)
    {
        if (!AxisLockEnabled &&
            !LengthLockEnabled &&
            !AngleLockEnabled)
            return point;

        var a = WorldToLocal(reference);
        var b = WorldToLocal(point);
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
                throw new InvalidOperationException(
                    "Locked length must be finite and greater than zero.");

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

        return LocalToWorld(
            new CadPlanePoint(
                a.X + dx,
                a.Y + dy));
    }

    public void ResetLocks()
    {
        ResetLocksCore();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ResetTransientLocksCore()
    {
        _lengthLockEnabled = false;
        _angleLockEnabled = false;
        _lockedLength = 0.0;
        _lockedAngleDegrees = 0.0;
    }

    private void ResetLocksCore()
    {
        _axisLockEnabled = false;
        ResetTransientLocksCore();
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

    private static double NormalizeAngleDegrees(double angle)
    {
        var normalized = angle % 360.0;
        return normalized < 0.0
            ? normalized + 360.0
            : normalized;
    }

    private static CadPlaneFrame CreatePresetFrame(
        CadWorkPlanePreset preset,
        OcctPoint3d origin) =>
        preset switch
        {
            CadWorkPlanePreset.XY => CreateFrame(
                origin,
                OcctVector3d.UnitX,
                OcctVector3d.UnitY,
                preset),
            CadWorkPlanePreset.YZ => CreateFrame(
                origin,
                OcctVector3d.UnitY,
                OcctVector3d.UnitZ,
                preset),
            CadWorkPlanePreset.XZ => CreateFrame(
                origin,
                OcctVector3d.UnitZ,
                OcctVector3d.UnitX,
                preset),
            _ => throw new ArgumentOutOfRangeException(
                nameof(preset))
        };

    private static CadPlaneFrame CreateFrame(
        OcctPoint3d origin,
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        CadWorkPlanePreset preset)
    {
        if (!origin.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (!xAxis.TryNormalize(out var x))
            throw new ArgumentOutOfRangeException(nameof(xAxis));

        var cross = x.Cross(yAxis);
        if (!cross.TryNormalize(out var normal))
            throw new ArgumentException(
                "Work-plane axes must not be parallel.",
                nameof(yAxis));

        var y = normal.Cross(x).Normalized();
        return new CadPlaneFrame(
            origin,
            x,
            y,
            normal,
            preset);
    }

    private static CadPlaneFrame WithOrigin(
        CadPlaneFrame frame,
        OcctPoint3d origin)
    {
        if (!origin.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(origin));
        return frame with { Origin = origin };
    }
}
