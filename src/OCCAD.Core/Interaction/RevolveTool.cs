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
            : CadToolInteractionPolicy.Drawing;

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

        SetSelectionFilter(null);
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
            if (input.Kind == OcctPointerInputKind.Pressed &&
                input.Button == OcctPointerButton.Left)
            {
                // Check if user clicked an existing line entity
                if (Context.Workspace.Preselection.Current?.Entity is CadLineEntity line)
                {
                    _axisLine = line;
                    _axisStart = line.ToWorldPoint(line.Start);
                    _axisEnd = line.ToWorldPoint(line.End);
                    RefreshPreview();
                    return CommitRevolve();
                }

                var point = Context.ResolvePoint(input.X, input.Y, null).Point;
                _axisStart = point;
                SetStageLocalized(
                    2,
                    "Cad.Prompt.revolve.AxisEnd",
                    "Revolve: specify axis end point [Backspace undo, Esc cancel]",
                    CadPrecisionInputKind.None);
                return true;
            }
            return false;
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
        if (State != CadToolState.Drawing || Entities.Count != 1)
            return false;

        if (_axisStart is null)
        {
            _axisStart = point;
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
        State == CadToolState.Drawing && (Entities.Count > 0 || _axisStart is not null);

    protected override bool OnStepBack()
    {
        if (_axisStart is not null)
        {
            _axisStart = null;
            _axisEnd = null;
            _axisLine = null;
            Context.Preview.Clear();
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
        if (TryCreate(out var entity))
            Context.Preview.Show(entity);
        else
            Context.Preview.Clear();
    }

    private bool TryCreate(out CadRevolveEntity entity)
    {
        entity = null!;
        if (Entities.Count != 1 || _axisStart is null || _axisEnd is null)
            return false;

        var start = _axisStart.Value;
        var end = _axisEnd.Value;
        var dir = end - start;
        if (!dir.TryNormalize(out var normalizedDir))
            return false;

        try
        {
            entity = new CadRevolveEntity(
                Entities[0],
                start,
                normalizedDir,
                _angleDegrees);

            if (_axisLine is not null)
                entity.BindSources(Entities[0], _axisLine);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CommitRevolve()
    {
        if (!TryCreate(out var entity))
            return false;

        Context.Preview.Clear();
        Context.Workspace.AddGeneratedEntities([entity], "Revolve");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    protected override void ResetTransformState()
    {
        _axisStart = null;
        _axisEnd = null;
        _axisLine = null;
    }
}
