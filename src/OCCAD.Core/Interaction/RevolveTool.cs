using OcctNet;

namespace OCCAD;

public sealed class RevolveTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private double _angleDegrees = 360.0;
    private OcctPoint3d? _axisStart;
    private OcctPoint3d? _axisEnd;
    private CadLineEntity? _axisLine;

    public override string Id => "revolve";
    public override string DisplayName => "Revolve";

    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Point;

    public override CadToolInteractionPolicy InteractionPolicy =>
        State == CadToolState.WaitForSelect
            ? CadToolInteractionPolicy.Selection
            : _axisStart is null
                ? new CadToolInteractionPolicy(
                    SelectionEnabled: false,
                    PreselectionEnabled: true,
                    GripEnabled: false)
                : CadToolInteractionPolicy.Drawing;

    public override bool CanCommitCurrentStage =>
        State == CadToolState.WaitForSelect
            ? base.CanCommitCurrentStage
            : _axisStart is not null && base.CanCommitCurrentStage;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Revolve",
            [
                new CadDoubleToolParameterDescriptor(
                    "Angle",
                    "Angle",
                    _angleDegrees,
                    0.1,
                    360.0)
            ]);

    protected override bool AutoCommitValidSelection => true;

    protected override bool IsSelectionValid(CadEntity[] entities) =>
        entities.Length == 1 &&
        CadPlanarProfileGeometry.IsSupported(entities[0]);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadEntityFilter(
                "revolve.profiles",
                CadPlanarProfileGeometry.IsSupported));

        if (!IsSelectionValid(Context.Selection.Selected.ToArray()))
            Context.Selection.Clear();

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        _axisStart = null;
        _axisEnd = null;
        _axisLine = null;

        SetAxisLineFilter();
        SetStageLocalized(
            1,
            "Cad.Prompt.revolve.AxisStart",
            "Revolve: specify axis start point or click an axis line [Backspace select, Esc cancel]",
            CadPrecisionInputKind.None);
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (State == CadToolState.WaitForSelect || Entities.Count != 1)
            return false;

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (_axisStart is null)
        {
            if (input.Kind == OcctPointerInputKind.Moved)
                return true;

            if (input.Kind != OcctPointerInputKind.Pressed ||
                input.Button != OcctPointerButton.Left)
                return false;

            if (Context.Workspace.Preselection.Current?.Entity is CadLineEntity line)
            {
                _axisLine = line;
                _axisStart = line.ToWorldPoint(line.Start);
                _axisEnd = line.ToWorldPoint(line.End);
                SetSelectionFilter(null);
                RefreshPreview();
                return CommitRevolve();
            }

            var point = Context.ResolvePoint(input.X, input.Y, null).Point;
            _axisStart = point;
            SetSelectionFilter(null);
            SetStageLocalized(
                2,
                "Cad.Prompt.revolve.AxisEnd",
                "Revolve: specify axis end point [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.None);
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            var point = Context.ResolvePoint(input.X, input.Y, _axisStart).Point;
            _axisEnd = point;
            RefreshPreview();
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left)
        {
            var point = Context.ResolvePoint(input.X, input.Y, _axisStart).Point;
            return TryAcceptPoint(point);
        }

        return false;
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive ||
            State != CadToolState.Drawing ||
            Entities.Count != 1 ||
            !point.IsFinite)
            return false;

        if (_axisStart is null)
        {
            _axisStart = point;
            SetSelectionFilter(null);
            SetStageLocalized(
                2,
                "Cad.Prompt.revolve.AxisEnd",
                "Revolve: specify axis end point [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.None);
            return true;
        }

        _axisEnd = point;
        return CommitRevolve();
    }

    protected override bool CanStepBackCore =>
        State == CadToolState.Drawing &&
        (Entities.Count > 0 || _axisStart is not null);

    protected override bool OnStepBack()
    {
        if (_axisStart is not null)
        {
            _axisStart = null;
            _axisEnd = null;
            _axisLine = null;
            Context.Preview.Clear();
            SetAxisLineFilter();
            SetStageLocalized(
                1,
                "Cad.Prompt.revolve.AxisStart",
                "Revolve: specify axis start point or click an axis line [Backspace select, Esc cancel]",
                CadPrecisionInputKind.None);
            return true;
        }

        RestartSelection();
        return true;
    }

    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals("Angle", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!CadValueTextConverter.TryParseFiniteDouble(value, out var angle) ||
            angle <= 0.0 ||
            angle > 360.0)
            return false;

        _angleDegrees = angle;
        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    private void RefreshPreview()
    {
        if (!TryCreate(out var entity))
        {
            Context.Preview.Clear();
            return;
        }

        try
        {
            Context.Preview.Show(entity);
        }
        catch (Exception exception) when (IsRecoverablePreviewFailure(exception))
        {
            Context.Preview.Clear();
        }
    }

    private bool TryCreate(out CadRevolveEntity entity)
    {
        entity = null!;
        if (Entities.Count != 1 || _axisStart is null || _axisEnd is null)
            return false;

        var start = _axisStart.Value;
        var end = _axisEnd.Value;
        var direction = end - start;
        if (!direction.TryNormalize(out var normalizedDirection))
            return false;

        entity = new CadRevolveEntity(
            Entities[0],
            start,
            normalizedDirection,
            _angleDegrees)
        {
            Layer = Context.Workspace.Layers.Current.Name
        };

        if (_axisLine is not null)
            entity.BindSources(Entities[0], _axisLine);

        return true;
    }

    private bool CommitRevolve()
    {
        if (!TryCreate(out var entity))
            return false;

        var engine = Context.Engine;
        Context.Preview.Clear();
        try
        {
            CadTransaction.ApplyCreatedEntity(
                Context.Workspace,
                entity,
                "Revolve",
                Context.Workspace.Tools.CompleteCurrent);
        }
        catch
        {
            if (IsActive)
                RefreshPreview();
            throw;
        }

        engine.Redraw();
        return true;
    }

    private void SetAxisLineFilter() =>
        SetSelectionFilter(
            new CadEntityFilter(
                "revolve.axis",
                static entity => entity is CadLineEntity));

    private static bool IsRecoverablePreviewFailure(Exception exception) =>
        exception is ArgumentException or
        InvalidOperationException or
        ArithmeticException or
        OcctException;

    protected override void ResetTransformState()
    {
        _axisStart = null;
        _axisEnd = null;
        _axisLine = null;
    }
}
