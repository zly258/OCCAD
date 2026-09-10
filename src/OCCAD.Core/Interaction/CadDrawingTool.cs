using OcctNet;

namespace OCCAD;

public abstract class CadDrawingTool : CadTool
{
    protected const double MinimumPreviewSize = 0.1;

    protected readonly record struct WorkPlaneFrame(
        OcctPoint3d Origin,
        OcctVector3d XAxis,
        OcctVector3d YAxis);

    protected override bool CanCommitCurrentStageCore => true;

    protected bool CancelOnRightClick(OcctPointerInputEventArgs input)
    {
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Right)
            return false;

        return Context.Workspace.Tools.HandleSecondaryAction();
    }

    protected bool FinishOnDoubleClick(OcctPointerInputEventArgs input)
    {
        if (input.Kind != OcctPointerInputKind.DoubleClicked ||
            input.Button != OcctPointerButton.Left ||
            !CanFinish)
            return false;

        return Context.Workspace.Tools.FinishCurrent();
    }

    protected OcctPoint3d Resolve(
        OcctPointerInputEventArgs input,
        OcctPoint3d? reference = null) =>
        Context.ResolvePoint(input.X, input.Y, reference).Point;

    protected bool CommitResolvedPoint(
        CadPointerPosition pointer,
        OcctPoint3d? reference,
        Func<OcctPoint3d, bool> acceptPoint)
    {
        ArgumentNullException.ThrowIfNull(acceptPoint);

        var point = Context.ResolvePoint(pointer.X, pointer.Y, reference).Point;
        return acceptPoint(point);
    }

    protected void ShowPreview(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.Layer =
            Context.Workspace.Layers.Current.Name;
        Context.Preview.Show(entity);
    }

    protected WorkPlaneFrame CaptureWorkPlaneFrame() =>
        new(
            Context.WorkPlane.Origin,
            Context.WorkPlane.XAxis,
            Context.WorkPlane.YAxis);

    protected void RestoreWorkPlaneFrame(
        WorkPlaneFrame frame,
        OcctPoint3d? origin = null) =>
        SetWorkPlane(
            origin ?? frame.Origin,
            frame.XAxis,
            frame.YAxis,
            lockPlane: false);

    protected void AdvanceWorkPlaneAlongSegment(
        OcctPoint3d from,
        OcctPoint3d to)
    {
        var normal = Context.WorkPlane.Normal;
        var segment = CadTransformMath.Between(from, to);
        var axial = CadTransformMath.Dot(segment, normal);
        var planar = new OcctVector3d(
            segment.X - normal.X * axial,
            segment.Y - normal.Y * axial,
            segment.Z - normal.Z * axial);

        if (!planar.TryNormalize(out var xAxis))
        {
            Context.WorkPlane.SetOrigin(to);
            return;
        }

        var yAxis = normal.Cross(xAxis).Normalized();
        Context.WorkPlane.SetToolPlaneFixed(false);
        Context.WorkPlane.SetToolPlane(to, xAxis, yAxis);
    }

    protected void CommitPreview(CadEntity? entity)
    {
        if (entity is null)
            return;

        // Keep the transient preview until the document/history commit
        // succeeds. If presentation creation or history recording fails, the
        // Tool remains active with its last valid preview.
        Context.AddEntity(entity.Duplicate());
        Context.Preview.Clear();
        Context.Workspace.Tools.CompleteCurrent();
    }
}
