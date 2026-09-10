using OcctNet;

namespace OCCAD;

public enum CadWorkPlanePreset { XY, YZ, XZ }
public readonly record struct CadPlanePoint(double X, double Y);

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
    private bool _isActive;
    private bool _planeLocked;
    private OcctPoint3d _referenceOrigin;
    private OcctVector3d _referenceXAxis = OcctVector3d.UnitX;
    private OcctVector3d _referenceYAxis = OcctVector3d.UnitY;
    private OcctVector3d _referenceNormal = OcctVector3d.UnitZ;
    private bool _axisLockEnabled;
    private bool _lengthLockEnabled;
    private double _lockedLength;
    private bool _angleLockEnabled;
    private double _lockedAngleDegrees;
    private bool _orthogonalTrackingEnabled = true;
    private bool _polarTrackingEnabled;
    private double _polarIncrementDegrees = 45.0;
    private double _trackingToleranceDegrees = 2.0;

    public CadWorkPlane() => SetPreset(CadWorkPlanePreset.XY);

    public bool IsActive => _isActive;
    public bool IsPlaneLocked => _planeLocked;
    public OcctPoint3d Origin { get; private set; }
    public OcctVector3d XAxis { get; private set; }
    public OcctVector3d YAxis { get; private set; }
    public OcctVector3d Normal { get; private set; }
    public CadWorkPlanePreset Preset { get; private set; }

    // Explicit hard axis constraint. Unlike ORTHO/POLAR tracking this does not
    // use an angular tolerance: the point is projected to the dominant local axis.
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

    // Explicit exact angular constraint. It takes precedence over automatic
    // ORTHO/POLAR tracking and over AXIS lock.
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

    // Automatic tracking modes. They only project when the pointer is inside
    // TrackingToleranceDegrees and are suppressed by explicit direction locks.
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

    public void Activate(OcctPoint3d origin)
    {
        if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin));
        _isActive = true;
        _planeLocked = false;
        Origin = origin;
        XAxis = _referenceXAxis;
        YAxis = _referenceYAxis;
        Normal = _referenceNormal;
        ResetTransientLocksCore();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Deactivate()
    {
        if (!_isActive && !_planeLocked && !LengthLockEnabled && !AngleLockEnabled)
            return;

        _isActive = false;
        _planeLocked = false;
        Origin = _referenceOrigin;
        XAxis = _referenceXAxis;
        YAxis = _referenceYAxis;
        Normal = _referenceNormal;
        ResetTransientLocksCore();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetPlaneLocked(bool value)
    {
        if (_planeLocked == value) return;
        _planeLocked = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetPreset(CadWorkPlanePreset preset, OcctPoint3d? origin = null)
    {
        if (_planeLocked) return;
        var point = origin ?? OcctPoint3d.Origin;
        switch (preset)
        {
            case CadWorkPlanePreset.XY: SetBasisCore(point, OcctVector3d.UnitX, OcctVector3d.UnitY); break;
            case CadWorkPlanePreset.YZ: SetBasisCore(point, OcctVector3d.UnitY, OcctVector3d.UnitZ); break;
            // Match OCCTBIM-Source XOZ orientation: local X follows world Z,
            // local Y follows world X and the plane normal is +world Y.
            case CadWorkPlanePreset.XZ: SetBasisCore(point, OcctVector3d.UnitZ, OcctVector3d.UnitX); break;
            default: throw new ArgumentOutOfRangeException(nameof(preset));
        }
        Preset = preset;
        if (!_isActive) CaptureReferencePlane();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCustom(OcctPoint3d origin, OcctVector3d xAxis, OcctVector3d yAxis)
    {
        if (_planeLocked) return;
        var previousOrigin = Origin;
        var previousX = XAxis;
        var previousY = YAxis;
        SetBasisCore(origin, xAxis, yAxis);
        if (Origin == previousOrigin &&
            (XAxis - previousX).Length <= 1e-12 &&
            (YAxis - previousY).Length <= 1e-12) return;
        if (!_isActive) CaptureReferencePlane();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetOrigin(OcctPoint3d origin)
    {
        if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin));
        if (Origin == origin) return;
        Origin = origin;
        if (!_isActive) _referenceOrigin = origin;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryIntersect(OcctProjectionRay ray, out OcctPoint3d point)
    {
        if (!ray.Origin.IsFinite || !ray.Direction.TryNormalize(out var direction))
        {
            point = default;
            return false;
        }
        var denominator = direction.Dot(Normal);
        if (Math.Abs(denominator) <= 1e-12)
        {
            point = default;
            return false;
        }
        var t = (Origin - ray.Origin).Dot(Normal) / denominator;
        point = ray.Origin + direction * t;
        return point.IsFinite;
    }

    public CadPlanePoint WorldToLocal(OcctPoint3d point)
    {
        if (!point.IsFinite) throw new ArgumentOutOfRangeException(nameof(point));
        var delta = point - Origin;
        return new CadPlanePoint(delta.Dot(XAxis), delta.Dot(YAxis));
    }

    public OcctPoint3d LocalToWorld(CadPlanePoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point));
        return Origin + XAxis * point.X + YAxis * point.Y;
    }

    public CadTrackingResult? Track(OcctPoint3d reference, OcctPoint3d point)
    {
        if (!reference.IsFinite) throw new ArgumentOutOfRangeException(nameof(reference));
        if (!point.IsFinite) throw new ArgumentOutOfRangeException(nameof(point));
        if (!IsActive ||
            (!OrthogonalTrackingEnabled && !PolarTrackingEnabled) ||
            AxisLockEnabled || AngleLockEnabled)
            return null;

        var a = WorldToLocal(reference);
        var b = WorldToLocal(point);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 1e-12) return null;

        var angleDegrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var bestDelta = double.PositiveInfinity;
        var bestAngle = 0.0;
        var bestKind = CadTrackingKind.Orthogonal;

        if (OrthogonalTrackingEnabled)
            Consider(CadTrackingKind.Orthogonal, Math.Round(angleDegrees / 90.0) * 90.0);
        if (PolarTrackingEnabled)
            Consider(CadTrackingKind.Polar, Math.Round(angleDegrees / PolarIncrementDegrees) * PolarIncrementDegrees);
        if (bestDelta > TrackingToleranceDegrees) return null;

        var radians = bestAngle * Math.PI / 180.0;
        var directionX = Math.Cos(radians);
        var directionY = Math.Sin(radians);
        var projectedLength = dx * directionX + dy * directionY;
        var tracked = LocalToWorld(new CadPlanePoint(
            a.X + directionX * projectedLength,
            a.Y + directionY * projectedLength));
        return new CadTrackingResult(reference, tracked, bestKind, NormalizeAngleDegrees(bestAngle));

        void Consider(CadTrackingKind kind, double candidateAngle)
        {
            var delta = Math.Abs(AngleDeltaDegrees(angleDegrees, candidateAngle));
            if (delta >= bestDelta) return;
            bestDelta = delta;
            bestAngle = candidateAngle;
            bestKind = kind;
        }
    }

    public OcctPoint3d Constrain(OcctPoint3d reference, OcctPoint3d point)
    {
        if (!AxisLockEnabled && !LengthLockEnabled && !AngleLockEnabled) return point;
        var a = WorldToLocal(reference);
        var b = WorldToLocal(point);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;

        if (AxisLockEnabled && !AngleLockEnabled)
        {
            if (Math.Abs(dx) >= Math.Abs(dy)) dy = 0.0;
            else dx = 0.0;
        }

        var angle = Math.Atan2(dy, dx);
        if (AngleLockEnabled)
        {
            // A direction lock constrains by orthogonal projection. Rotating the
            // full radial pointer distance onto the locked direction makes side
            // grips grow when the pointer moves diagonally and loses the signed
            // direction when crossing the opposite side.
            angle = LockedAngleDegrees * Math.PI / 180.0;
            var directionX = Math.Cos(angle);
            var directionY = Math.Sin(angle);
            var projectedLength = dx * directionX + dy * directionY;
            dx = directionX * projectedLength;
            dy = directionY * projectedLength;
        }

        if (LengthLockEnabled)
        {
            if (!double.IsFinite(LockedLength) || LockedLength <= 0.0)
                throw new InvalidOperationException("Locked length must be finite and greater than zero.");
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

        return LocalToWorld(new CadPlanePoint(a.X + dx, a.Y + dy));
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

    private static double AngleDeltaDegrees(double left, double right)
    {
        var delta = NormalizeAngleDegrees(left - right);
        return delta > 180.0 ? delta - 360.0 : delta;
    }

    private static double NormalizeAngleDegrees(double angle)
    {
        var normalized = angle % 360.0;
        return normalized < 0.0 ? normalized + 360.0 : normalized;
    }

    private void CaptureReferencePlane()
    {
        _referenceOrigin = Origin;
        _referenceXAxis = XAxis;
        _referenceYAxis = YAxis;
        _referenceNormal = Normal;
    }

    private void SetBasisCore(OcctPoint3d origin, OcctVector3d xAxis, OcctVector3d yAxis)
    {
        if (!origin.IsFinite) throw new ArgumentOutOfRangeException(nameof(origin));
        if (!xAxis.TryNormalize(out var x)) throw new ArgumentOutOfRangeException(nameof(xAxis));
        var cross = x.Cross(yAxis);
        if (!cross.TryNormalize(out var normal))
            throw new ArgumentException("Work-plane axes must not be parallel.", nameof(yAxis));
        var y = normal.Cross(x).Normalized();
        Origin = origin;
        XAxis = x;
        YAxis = y;
        Normal = normal;
    }
}
