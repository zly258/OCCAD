using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class RectangularArrayTool : CadSelectionTransformToolBase
{
    private int _columns = 2;
    private int _rows = 2;
    private double _columnSpacing = 10;
    private double _rowSpacing = 10;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    public override string Id => "rectarray";
    public override string DisplayName => "Rectangular Array";
    protected override bool CanFinishCore => State == CadToolState.Drawing &&
        CadRectangularArray.IsValid(Entities.Count, _columns, _rows, _columnSpacing, _rowSpacing);
    public override CadToolPanelDescriptor? ParameterPanel => State != CadToolState.Drawing ? null : new(
        "Rectangular Array",
        [
            new CadIntegerToolParameterDescriptor("Columns", "Columns (including source)", _columns, 1, 100),
            new CadIntegerToolParameterDescriptor("Rows", "Rows (including source)", _rows, 1, 100),
            new CadDoubleToolParameterDescriptor("ColumnSpacing", "Column spacing", _columnSpacing, -1e9, 1e9),
            new CadDoubleToolParameterDescriptor("RowSpacing", "Row spacing", _rowSpacing, -1e9, 1e9)
        ]);

    protected override void OnTransformStarted()
    {
        _xAxis = Context.WorkPlane.XAxis;
        _yAxis = Context.WorkPlane.YAxis;
        Context.WorkPlane.SetToolPlaneFixed(true);
        Context.Snap.Active = false;
        SetStageLocalized(1, "Cad.Prompt.rectarray.Parameters", "Rectangular Array: set rows, columns and spacing [Enter accept, Esc cancel]");
        UpdatePreview();
    }

    protected override bool OnSetParameter(string id, string value)
    {
        if (State != CadToolState.Drawing) return false;
        switch (id.ToUpperInvariant())
        {
            case "COLUMNS":
            case "ROWS":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var count) || count < 1 || count > 100) return false;
                if (id.Equals("Columns", StringComparison.OrdinalIgnoreCase)) _columns = count;
                else _rows = count;
                break;
            case "COLUMNSPACING":
            case "ROWSPACING":
                if ((!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var spacing) &&
                     !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out spacing)) ||
                    !double.IsFinite(spacing) || Math.Abs(spacing) > 1e9) return false;
                if (id.Equals("ColumnSpacing", StringComparison.OrdinalIgnoreCase)) _columnSpacing = spacing;
                else _rowSpacing = spacing;
                break;
            default: return false;
        }
        UpdatePreview();
        return true;
    }

    protected override bool OnFinish()
    {
        if (!CanFinish) return false;
        Context.Workspace.CreateRectangularArray(Entities, _columns, _rows,
            _xAxis * _columnSpacing, _yAxis * _rowSpacing);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void UpdatePreview()
    {
        if (!CanFinish)
        {
            Context.Preview.Clear();
            SetPromptLocalized("Cad.Prompt.rectarray.Invalid",
                "Rectangular Array: use nonzero spacing, at least two cells, and at most 10000 copies [Esc cancel]");
            return;
        }
        var copies = CadRectangularArray.CreateCopies(Entities, _columns, _rows,
            _xAxis * _columnSpacing, _yAxis * _rowSpacing, CadRectangularArray.PreviewCopies);
        Context.Preview.Show(copies);
        var total = Entities.Count * (_columns * _rows - 1);
        SetPromptLocalized("Cad.Prompt.rectarray.Preview",
            "Rectangular Array: {0} copies; preview shows {1}. Set parameters [Enter accept, Esc cancel]",
            CadPrecisionInputKind.None, total, copies.Length);
    }

    protected override void ResetTransformState()
    {
        _columns = 2;
        _rows = 2;
        _columnSpacing = 10;
        _rowSpacing = 10;
    }
}
