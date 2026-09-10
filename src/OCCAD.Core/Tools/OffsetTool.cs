using OcctNet;

namespace OCCAD;

public sealed class OffsetTool : CadTool, ICadPointInputTool
{
    private const double MinimumDistance = 1e-4;
    private double _distance = 10.0;
    private CadEntity? _selectedEntity;

    public override string Id => "offset";
    public override string DisplayName => "Offset";

    public override CadToolInputKind InputKind =>
        _selectedEntity is null
            ? CadToolInputKind.Selection
            : CadToolInputKind.Point;

    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with { PreselectionEnabled = true };

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Offset",
            [
                new CadDoubleToolParameterDescriptor(
                    "Distance",
                    "Distance",
                    _distance,
                    MinimumDistance,
                    1e6)
            ]);

    protected override void OnActivated()
    {
        _selectedEntity = null;
        SetSelectionFilter(
            new CadEntityFilter(
                "offset.curves",
                CadOffsetGeometry.IsSupported));
        Context.Selection.Clear();
        Context.Workspace.Preselection.Clear();
        SetStageLocalized(
            0,
            "Cad.Prompt.offset.Select",
            "Offset: select object to offset [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Right)
        {
            Context.Workspace.Tools.CancelCurrent();
            return true;
        }

        if (_selectedEntity is null)
        {
            if (input.Kind != OcctPointerInputKind.Pressed ||
                input.Button != OcctPointerButton.Left)
                return false;

            if (Context.Workspace.Preselection.Current is not
                {
                    Entity: var entity
                } || !CadOffsetGeometry.IsSupported(entity))
                return true;

            _selectedEntity = entity;
            SetStageLocalized(
                1,
                "Cad.Prompt.offset.Side",
                "Offset: specify point on side to offset [Backspace undo, Esc cancel]",
                CadPrecisionInputKind.Length);
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            var point = Context.ResolvePoint(input.X, input.Y, null).Point;
            if (CadOffsetGeometry.TryOffset(_selectedEntity, point, _distance, Context.WorkPlane, out var preview) && preview is not null)
                Context.Preview.Show(preview);
            else
                Context.Preview.Clear();
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left)
        {
            var point = Context.ResolvePoint(input.X, input.Y, null).Point;
            return TryAcceptPoint(point);
        }

        return false;
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        _selectedEntity is null ? base.OnCommitCurrentStage(pointer) :
        TryAcceptPoint(Context.ResolvePoint(pointer.X, pointer.Y, null).Point);

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (_selectedEntity is null)
            return false;

        if (!CadOffsetGeometry.TryOffset(_selectedEntity, point, _distance, Context.WorkPlane, out var result) || result is null)
            return false;

        Context.Preview.Clear();
        Context.Workspace.AddGeneratedEntities([result], "Offset");

        // Return to selection stage to allow sequential offsets
        _selectedEntity = null;
        Context.Workspace.Preselection.Clear();
        SetStageLocalized(
            0,
            "Cad.Prompt.offset.Select",
            "Offset: select object to offset [Esc cancel]");
        return true;
    }

    protected override bool CanStepBackCore => _selectedEntity is not null;

    protected override bool OnStepBack()
    {
        if (_selectedEntity is null)
            return false;

        Context.Preview.Clear();
        _selectedEntity = null;
        Context.Workspace.Preselection.Clear();
        SetStageLocalized(
            0,
            "Cad.Prompt.offset.Select",
            "Offset: select object to offset [Esc cancel]");
        return true;
    }

    protected override void OnCanceled()
    {
        Context.Preview.Clear();
        _selectedEntity = null;
        Context.Workspace.Preselection.Clear();
    }

    protected override void OnDeactivated()
    {
        Context.Preview.Clear();
        _selectedEntity = null;
        Context.Workspace.Preselection.Clear();
    }

    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals("Distance", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!CadValueTextConverter.TryParseFiniteDouble(value, out var distance) ||
            distance < MinimumDistance ||
            distance > 1e6)
            return false;

        _distance = distance;
        NotifyUpdated();
        return true;
    }
}
