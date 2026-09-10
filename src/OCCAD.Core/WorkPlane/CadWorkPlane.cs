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

public sealed class CadWorkPlane
{
    private CadPlaneFrame _userPlane;
    private CadPlaneFrame? _toolPlane;
    private CadPlaneFrame? _gripPlane;
    private bool _userPlaneLocked;
    private bool _toolPlaneFixed;
    private bool _gripPlaneFixed;

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

    public event EventHandler? Changed;

    public void BeginToolPlane(OcctPoint3d origin)
    {
        if (!origin.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(origin));

        _toolPlane = WithOrigin(_userPlane, origin);
        _toolPlaneFixed = false;
        _gripPlane = null;
        _gripPlaneFixed = false;
        PublishChanged();
    }

    public void EndToolPlane()
    {
        if (_toolPlane is null &&
            _gripPlane is null)
            return;

        _gripPlane = null;
        _gripPlaneFixed = false;
        _toolPlane = null;
        _toolPlaneFixed = false;
        PublishChanged();
    }

    public void SetUserPlaneLocked(bool value)
    {
        if (_userPlaneLocked == value) return;
        _userPlaneLocked = value;
        PublishChanged();
    }

    public void SetToolPlaneFixed(bool value)
    {
        if (_toolPlane is null || _toolPlaneFixed == value) return;
        _toolPlaneFixed = value;
        PublishChanged();
    }

    public void SetGripPlaneFixed(bool value)
    {
        if (_gripPlane is null || _gripPlaneFixed == value) return;
        _gripPlaneFixed = value;
        PublishChanged();
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
        var changed = _userPlane != next;
        _userPlane = next;

        if (_toolPlane is not null && !_toolPlaneFixed)
        {
            var toolOrigin = origin ?? _toolPlane.Value.Origin;
            var nextToolPlane = CreatePresetFrame(preset, toolOrigin);
            if (_toolPlane != nextToolPlane)
            {
                _toolPlane = nextToolPlane;
                changed = true;
            }
        }

        if (changed)
            PublishChanged();
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
        PublishChanged();
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
        PublishChanged();
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
        PublishChanged();
    }

    public void ClearGripPlane()
    {
        if (_gripPlane is null && !_gripPlaneFixed) return;
        _gripPlane = null;
        _gripPlaneFixed = false;
        PublishChanged();
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

        PublishChanged();
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

    private void PublishChanged()
    {
        var handlers = Changed;
        if (handlers is null)
            return;

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception exception) when (IsRecoverableObserverFailure(exception))
            {
                // Work-plane state is already authoritative. Changed is used by
                // UI/status observers and must not turn an applied frame/lock
                // transition into a false failure or starve later observers.
                System.Diagnostics.Debug.WriteLine(
                    $"Work-plane Changed observer failed after state changed: {exception}");
            }
        }
    }

    private static bool IsRecoverableObserverFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

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