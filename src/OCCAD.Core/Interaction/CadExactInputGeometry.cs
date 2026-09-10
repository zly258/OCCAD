using OcctNet;

namespace OCCAD;

/// <summary>
/// Resolves geometry that is fully determined by drafting locks without
/// requiring a previous mouse sample. This keeps command-line, floating-panel
/// and pointer input on the same Core tool state machine.
/// </summary>
public static class CadExactInputGeometry
{
    public static bool TryResolveLengthAnglePoint(
        CadWorkspace workspace,
        OcctPoint3d reference,
        out OcctPoint3d point)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        point = default;

        var drafting = workspace.Drafting;
        if (!reference.IsFinite ||
            !drafting.LengthLockEnabled ||
            !drafting.AngleLockEnabled ||
            !double.IsFinite(drafting.LockedLength) ||
            drafting.LockedLength <= 1e-12 ||
            !double.IsFinite(drafting.LockedAngleDegrees))
        {
            return false;
        }

        return TryResolveAnglePoint(
            workspace,
            reference,
            drafting.LockedAngleDegrees,
            drafting.LockedLength,
            out point);
    }

    public static bool TryResolveLockedAnglePoint(
        CadWorkspace workspace,
        OcctPoint3d reference,
        out OcctPoint3d point)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        point = default;

        var drafting = workspace.Drafting;
        return drafting.AngleLockEnabled &&
               double.IsFinite(drafting.LockedAngleDegrees) &&
               TryResolveAnglePoint(
                   workspace,
                   reference,
                   drafting.LockedAngleDegrees,
                   1.0,
                   out point);
    }

    public static bool TryResolveAnglePoint(
        CadWorkspace workspace,
        OcctPoint3d reference,
        double angleDegrees,
        double length,
        out OcctPoint3d point)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        point = default;
        if (!reference.IsFinite ||
            !double.IsFinite(angleDegrees) ||
            !double.IsFinite(length) ||
            length <= 1e-12)
        {
            return false;
        }

        var plane = workspace.WorkPlane.EffectivePlane;
        if (!plane.XAxis.TryNormalize(out var xAxis) ||
            !plane.YAxis.TryNormalize(out var yAxis))
        {
            return false;
        }

        var radians = angleDegrees * Math.PI / 180.0;
        var dx = Math.Cos(radians) * length;
        var dy = Math.Sin(radians) * length;
        point = reference + xAxis * dx + yAxis * dy;
        return point.IsFinite;
    }
}
