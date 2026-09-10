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
        if (!IsActive || Context.Workspace.Tools.ActiveTool != this)
            return;

        ArgumentNullException.ThrowIfNull(entity);
        entity.Layer = Context.Workspace.Layers.Current.Name;
        Context.Preview.Show(entity);
    }

    protected void RefreshPreviewFromLastPointer(bool useCurrentPointer = true)
    {
        if (!useCurrentPointer ||
            !IsActive ||
            Context.Workspace.Tools.ActiveTool != this ||
            Context.Workspace.LastPointerPosition is not { } pointer)
            return;

        RefreshPreviewFromLastPointer(pointer);
    }

    protected internal override void RefreshPreviewFromLastPointer(CadPointerPosition pointer)
    {
        if (!IsActive || Context.Workspace.Tools.ActiveTool != this)
            return;

        var moveEvent = new OcctPointerInputEventArgs(
            OcctPointerInputKind.Moved,
            OcctPointerButton.None,
            OcctPointerButtons.None,
            pointer.X,
            pointer.Y,
            0,
            OcctInputModifiers.None);
        HandlePointer(moveEvent);
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        // Exact input is a Core interaction, not a UI side effect. Rebuild the
        // active preview immediately so command-line/MCP/floating-panel callers
        // all observe the same geometry without requiring another mouse move.
        RefreshPreviewFromLastPointer();
        return true;
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

        var committed = entity.Duplicate();
        var engine = Context.Engine;

        // Preview is transient and owned by the active tool. Commit the
        // persistent entity in the same display batch, then let the common
        // tool-deactivation path remove every transient interaction object.
        using (engine.BeginDisplayBatch())
        {
            Context.Preview.Clear();
            try
            {
                Context.AddEntity(committed);
                Context.Workspace.Tools.CompleteCurrent();
            }
            catch
            {
                if (IsActive)
                    ShowPreview(entity);
                throw;
            }
        }

        engine.Redraw();
    }
}
