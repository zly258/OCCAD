using OcctNet;

namespace OCCAD;

public sealed class GripEditTool : CadTool, ICadPointInputTool
{
    private readonly CadGripPoint _grip;
    private CadEntity? _before;
    private CadEntity? _preview;
    private bool _invalidGrip;
    private CadPrecisionInputKind _effectivePrecisionInputs;
    private bool _sourcePresentationSuppressed;

    public GripEditTool(CadGripPoint grip)
    {
        _grip = grip;
    }

    public override string Id => "grip";
    public override string DisplayName => "Grip Edit";

    public override OcctPoint3d? PrecisionReferencePoint => ConstraintOrigin;

    private OcctPoint3d ConstraintOrigin =>
        _grip.ConstraintOrigin ?? _grip.Position;

    protected override void OnActivated()
    {
        _before = Context.Workspace.CaptureGeometry(_grip.Entity);
        _preview = _grip.Entity.Duplicate();

        _effectivePrecisionInputs = _grip.PrecisionInputs;
        if (_grip.WorkPlane?.LockedAngleDegrees is not null)
            _effectivePrecisionInputs &= ~CadPrecisionInputKind.Angle;

        SetGripPrompt(_effectivePrecisionInputs);

        if (_grip.WorkPlane is { } plane)
        {
            Context.WorkPlane.SetGripPlane(
                plane.Origin,
                plane.XAxis,
                plane.YAxis,
                plane.LockPlane);
            if (plane.LockedAngleDegrees is { } angle)
                LockStageAngle(angle);
        }
        else
        {
            Context.WorkPlane.SetGripPlane(
                _grip.Position,
                Context.WorkPlane.XAxis,
                Context.WorkPlane.YAxis);
        }

        Context.Preview.Show(_preview);
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (_preview is null) return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            TryMovePreview(Context.ResolvePoint(input.X, input.Y, ConstraintOrigin).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed) return false;

        if (input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (input.Button != OcctPointerButton.Left) return false;

        _ = TryCommitPoint(
            Context.ResolvePoint(
                input.X,
                input.Y,
                ConstraintOrigin).Point);
        return true;
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        TryCommitPoint(point);

    protected override bool CanCommitCurrentStageCore => true;

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (_preview is null)
            return false;

        return TryCommitPoint(
            Context.ResolvePoint(
                pointer.X,
                pointer.Y,
                ConstraintOrigin).Point);
    }

    private bool TryCommitPoint(OcctPoint3d point)
    {
        if (_preview is null ||
            _before is null ||
            !point.IsFinite)
            return false;

        if (!TryMovePreview(point))
            return false;

        if (Context.Workspace.Entities.GeometryEquals(
                _before,
                _preview))
            return false;

        var workspace = Context.Workspace;
        var actualBefore = _grip.Entity.Duplicate();
        var wasModified = workspace.IsModified;
        var historyState = workspace.History.CurrentStateId;
        Exception? failure = null;

        using (workspace.Document.BeginChangeSet())
        {
            try
            {
                _grip.Entity.RestoreGeometrySnapshot(_preview);
                workspace.RecordGeometryChange(
                    _grip.Entity,
                    _before,
                    "Grip Edit");
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failure = exception;
                try
                {
                    _grip.Entity.RestoreGeometrySnapshot(actualBefore);
                }
                catch (Exception restoreException) when (IsRecoverable(restoreException))
                {
                    failure = new AggregateException(
                        "Grip commit and rollback both failed.",
                        exception,
                        restoreException);
                }
            }
        }

        if (failure is not null)
        {
            if (!wasModified &&
                workspace.History.CurrentStateId == historyState)
                workspace.MarkSaved();

            ShowInvalidGripPrompt();
            return true;
        }

        // Model and history are already committed at this point. Tool cleanup is
        // deliberately outside the mutation rollback path: a transient cleanup
        // failure must never roll back a successful model edit while leaving its
        // history entry behind.
        workspace.Tools.CompleteCurrent();
        return true;
    }

    private bool TryMovePreview(OcctPoint3d point)
    {
        if (_preview is null || !point.IsFinite)
        {
            ShowInvalidGripPrompt();
            return false;
        }

        CadEntity snapshot;
        try
        {
            snapshot = _preview.Duplicate();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            ShowInvalidGripPrompt();
            return false;
        }

        try
        {
            // Every pointer frame starts from the original geometry. This keeps
            // indexed grips deterministic when a side/arc crosses its opposite
            // side and avoids cumulative drift from the previous preview frame.
            if (_before is not null)
                _preview.RestoreGeometrySnapshot(_before);
            _preview.MoveWorldGrip(_grip.Index, point);
            Context.Preview.Update(_preview);
            if (_invalidGrip)
            {
                _invalidGrip = false;
                SetGripPrompt(_effectivePrecisionInputs);
            }
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            try
            {
                _preview.RestoreGeometrySnapshot(snapshot);
                Context.Preview.Update(_preview);
            }
            catch (Exception restoreException) when (IsRecoverable(restoreException))
            {
                _preview = snapshot;
                try
                {
                    Context.Preview.Update(_preview);
                }
                catch (Exception previewException) when (IsRecoverable(previewException))
                {
                }
            }

            ShowInvalidGripPrompt();
            return false;
        }
    }

    private void SuppressSourcePresentation()
    {
        if (_sourcePresentationSuppressed ||
            Context.Workspace.Engine is not
            { IsInitialized: true } engine ||
            _grip.Entity.ViewerObject is not
            { } source ||
            !engine.ContainsObject(source.Id))
            return;

        using var batch =
            engine.BeginDisplayBatch();
        engine.SetObjectSelectable(
            source,
            false);
        engine.SetObjectVisible(
            source,
            false);
        _sourcePresentationSuppressed = true;
    }

    private void RestoreSourcePresentation()
    {
        if (!_sourcePresentationSuppressed)
            return;

        _sourcePresentationSuppressed = false;

        if (Context.Workspace.Engine is not
            { IsInitialized: true } engine ||
            _grip.Entity.ViewerObject is not
            { } source ||
            !engine.ContainsObject(source.Id))
            return;

        var appearance =
            Context.Workspace.Document
                .ResolveAppearance(
                    _grip.Entity);

        using var batch =
            engine.BeginDisplayBatch();
        engine.SetObjectVisible(
            source,
            appearance.Visible);
        engine.SetObjectSelectable(
            source,
            appearance.Selectable);
    }

    private void SetGripPrompt(
        CadPrecisionInputKind precisionInputs)
    {
        var (key, fallback) = _grip.Kind switch
        {
            CadGripKind.Vertex =>
                ("Cad.Prompt.grip.Vertex",
                 "Grip Edit: move vertex [Esc cancel]"),
            CadGripKind.Midpoint =>
                ("Cad.Prompt.grip.Midpoint",
                 "Grip Edit: move midpoint [Esc cancel]"),
            CadGripKind.Center =>
                ("Cad.Prompt.grip.Center",
                 "Grip Edit: move center [Esc cancel]"),
            CadGripKind.Radius =>
                ("Cad.Prompt.grip.Radius",
                 "Grip Edit: change radius [Esc cancel]"),
            CadGripKind.Axis =>
                ("Cad.Prompt.grip.Axis",
                 "Grip Edit: change axis [Esc cancel]"),
            CadGripKind.Height =>
                ("Cad.Prompt.grip.Height",
                 "Grip Edit: change height [Esc cancel]"),
            _ =>
                ("Cad.Prompt.grip.Move",
                 "Grip Edit: move - specify new point [Esc cancel]")
        };

        SetStageLocalized(
            0,
            key,
            fallback,
            precisionInputs);
    }

    private void ShowInvalidGripPrompt()
    {
        if (_invalidGrip) return;
        _invalidGrip = true;
        SetPromptLocalized(
            "Cad.Prompt.grip.Invalid",
            "Grip Edit: invalid position; choose another point [Esc cancel]",
            _effectivePrecisionInputs);
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
