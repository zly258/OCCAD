using OcctNet;

namespace OCCAD;

internal sealed class CadPointResolver(CadWorkspace workspace)
{
    public CadResolvedPoint Resolve(int x, int y, OcctPoint3d? constraintOrigin, CadSnapResolvePolicy snapPolicy)
    {
        var engine = workspace.Engine ??
            throw new InvalidOperationException("No OCCT engine is attached.");

        OcctPoint3d point;
        if (workspace.WorkPlane.IsActive)
        {
            var ray = engine.GetViewRay(x, y);
            if (!workspace.WorkPlane.TryIntersect(ray, out point))
                throw new InvalidOperationException(
                    "The view ray is parallel to the active work plane.");
        }
        else
        {
            point = engine.ScreenToWorld(x, y);
        }

        var snap = workspace.Snap.Resolve(
            x,
            y,
            point,
            workspace.WorkPlane,
            constraintOrigin,
            snapPolicy);
        if (snap is { } value)
        {
            point = value.Position;
            // Hovering a snap target must not replace the drawing frame or unlock it.
            // Tools capture their frame when activated; only an explicit plane change
            // may reinitialize that frame before the first point.
        }

        CadTrackingResult? tracking = null;
        if (constraintOrigin is { } trackingOrigin)
        {
            if (snap is null)
                tracking = workspace.Drafting.Track(
                    workspace.WorkPlane,
                    trackingOrigin,
                    point);
            if (tracking is { } tracked)
                point = tracked.Point;

            var constrained = workspace.Drafting.Constrain(
                workspace.WorkPlane,
                trackingOrigin,
                point);
            if (snap is not null && constrained.DistanceTo(point) > 1e-9)
            {
                // An explicit dimension wins over an incompatible object snap.
                snap = null;
                workspace.Snap.Clear();
            }
            point = constrained;

            tracking = tracking is { } activeTracking
                ? activeTracking with { Point = point }
                : workspace.Drafting.ConstraintGuide(
                    workspace.WorkPlane,
                    trackingOrigin,
                    point);
        }

        if (constraintOrigin is not null)
            workspace.Tracking.Update(workspace.WorkPlane, point, tracking);
        else
            workspace.Tracking.Clear();

        var resolved = new CadResolvedPoint(point, snap, tracking);
        return resolved;
    }

}
