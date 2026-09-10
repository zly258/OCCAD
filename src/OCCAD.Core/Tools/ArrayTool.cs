using OcctNet;

namespace OCCAD;

public sealed class ArrayTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private string _mode = "Rectangular";
    private int _columns = 4;
    private int _rows = 3;
    private double _columnSpacing = 20.0;
    private double _rowSpacing = 20.0;
    private int _count = 6;
    private double _sweepAngle = 360.0;
    private bool _rotateItems = true;
    private OcctPoint3d? _centerPoint;

    public override string Id => "array";
    public override string DisplayName => "Array";

    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Confirmation;

    public override CadToolInteractionPolicy InteractionPolicy =>
        State == CadToolState.WaitForSelect
            ? CadToolInteractionPolicy.Selection
            : CadToolInteractionPolicy.Drawing;

    public override bool CanCommitCurrentStage =>
        State == CadToolState.WaitForSelect
            ? base.CanCommitCurrentStage
            : IsActive && State == CadToolState.Drawing && Entities.Count > 0;

    protected override bool AutoCommitValidSelection => true;

    public override CadToolParameterSchema ParameterSchema =>
        string.Equals(_mode, "Circular", StringComparison.OrdinalIgnoreCase)
            ? new(
                "Array",
                [
                    new CadChoiceToolParameterDescriptor(
                        "Mode",
                        "Mode",
                        _mode,
                        ["Rectangular", "Circular"]),
                    new CadIntegerToolParameterDescriptor(
                        "Count",
                        "Count",
                        _count,
                        2,
                        360),
                    new CadDoubleToolParameterDescriptor(
                        "SweepAngle",
                        "Sweep Angle",
                        _sweepAngle,
                        -360.0,
                        360.0),
                    new CadBooleanToolParameterDescriptor(
                        "RotateItems",
                        "Rotate Items",
                        _rotateItems)
                ])
            : new(
                "Array",
                [
                    new CadChoiceToolParameterDescriptor(
                        "Mode",
                        "Mode",
                        _mode,
                        ["Rectangular", "Circular"]),
                    new CadIntegerToolParameterDescriptor(
                        "Columns",
                        "Columns",
                        _columns,
                        1,
                        100),
                    new CadIntegerToolParameterDescriptor(
                        "Rows",
                        "Rows",
                        _rows,
                        1,
                        100),
                    new CadDoubleToolParameterDescriptor(
                        "ColumnSpacing",
                        "Column Spacing",
                        _columnSpacing,
                        -1e6,
                        1e6),
                    new CadDoubleToolParameterDescriptor(
                        "RowSpacing",
                        "Row Spacing",
                        _rowSpacing,
                        -1e6,
                        1e6)
                ]);

    protected override void OnTransformStarted()
    {
        _centerPoint = null;
        SetStageLocalized(
            1,
            "Cad.Prompt.array.Confirm",
            "Array: adjust parameters and click or press Enter to commit [Backspace select, Esc cancel]");
        RefreshPreview();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (State == CadToolState.WaitForSelect || Entities.Count == 0)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            var point = Context.ResolvePoint(input.X, input.Y, null).Point;
            if (string.Equals(_mode, "Circular", StringComparison.OrdinalIgnoreCase) && _centerPoint is null)
            {
                // Can dynamically track center point
            }
            return true;
        }

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left)
        {
            return CommitArray();
        }

        return false;
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (State != CadToolState.Drawing || Entities.Count == 0)
            return false;

        return CommitArray();
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitArray();

    protected override bool CanStepBackCore =>
        State == CadToolState.Drawing && Entities.Count > 0;

    protected override bool OnStepBack()
    {
        RestartSelection();
        return true;
    }

    protected override bool CanFinishCore =>
        State == CadToolState.Drawing && Entities.Count > 0;

    protected override bool OnFinish() => CommitArray();

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Mode", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = value.Trim();
            if (string.Equals(normalized, "Rectangular", StringComparison.OrdinalIgnoreCase))
                _mode = "Rectangular";
            else if (string.Equals(normalized, "Circular", StringComparison.OrdinalIgnoreCase))
                _mode = "Circular";
            else
                return false;
        }
        else if (id.Equals("Columns", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, out var cols) || cols < 1 || cols > 100)
                return false;
            _columns = cols;
        }
        else if (id.Equals("Rows", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, out var rows) || rows < 1 || rows > 100)
                return false;
            _rows = rows;
        }
        else if (id.Equals("ColumnSpacing", StringComparison.OrdinalIgnoreCase))
        {
            if (!CadValueTextConverter.TryParseFiniteDouble(value, out var cs) ||
                cs < -1e6 || cs > 1e6)
                return false;
            _columnSpacing = cs;
        }
        else if (id.Equals("RowSpacing", StringComparison.OrdinalIgnoreCase))
        {
            if (!CadValueTextConverter.TryParseFiniteDouble(value, out var rs) ||
                rs < -1e6 || rs > 1e6)
                return false;
            _rowSpacing = rs;
        }
        else if (id.Equals("Count", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, out var count) || count < 2 || count > 360)
                return false;
            _count = count;
        }
        else if (id.Equals("SweepAngle", StringComparison.OrdinalIgnoreCase))
        {
            if (!CadValueTextConverter.TryParseFiniteDouble(value, out var sweep) ||
                sweep < -360.0 || sweep > 360.0)
                return false;
            _sweepAngle = sweep;
        }
        else if (id.Equals("RotateItems", StringComparison.OrdinalIgnoreCase))
        {
            if (!bool.TryParse(value, out var rotate))
                return false;
            _rotateItems = rotate;
        }
        else
        {
            return false;
        }

        RefreshPreview();
        NotifyUpdated();
        return true;
    }

    private void RefreshPreview()
    {
        if (Entities.Count == 0)
        {
            Context.Preview.Clear();
            return;
        }

        try
        {
            if (string.Equals(_mode, "Circular", StringComparison.OrdinalIgnoreCase))
            {
                var center = _centerPoint ?? Context.WorkPlane.Origin;
                var axis = Context.WorkPlane.Normal.Normalized();
                var reference = center + Context.WorkPlane.XAxis * 10.0;
                var copies = CadCircularArray.CreateCopies(
                    Entities,
                    center,
                    axis,
                    _count,
                    _sweepAngle,
                    _rotateItems,
                    reference,
                    CadCircularArray.PreviewCopies);
                Context.Preview.Show(copies);
            }
            else
            {
                var colStep = Context.WorkPlane.XAxis.Normalized() * _columnSpacing;
                var rowStep = Context.WorkPlane.YAxis.Normalized() * _rowSpacing;
                var copies = CadRectangularArray.CreateCopies(
                    Entities,
                    _columns,
                    _rows,
                    colStep,
                    rowStep,
                    CadRectangularArray.PreviewCopies);
                Context.Preview.Show(copies);
            }
        }
        catch
        {
            Context.Preview.Clear();
        }
    }

    private bool CommitArray()
    {
        if (Entities.Count == 0)
            return false;

        Context.Preview.Clear();

        if (string.Equals(_mode, "Circular", StringComparison.OrdinalIgnoreCase))
        {
            var center = _centerPoint ?? Context.WorkPlane.Origin;
            var axis = Context.WorkPlane.Normal.Normalized();
            var reference = center + Context.WorkPlane.XAxis * 10.0;
            Context.Workspace.CreateCircularArray(
                Entities,
                center,
                axis,
                _count,
                _sweepAngle,
                _rotateItems,
                reference);
        }
        else
        {
            var colStep = Context.WorkPlane.XAxis.Normalized() * _columnSpacing;
            var rowStep = Context.WorkPlane.YAxis.Normalized() * _rowSpacing;
            Context.Workspace.CreateRectangularArray(
                Entities,
                _columns,
                _rows,
                colStep,
                rowStep);
        }

        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    protected override void ResetTransformState()
    {
        _centerPoint = null;
    }
}
