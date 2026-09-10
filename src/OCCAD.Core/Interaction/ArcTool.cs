using OcctNet;

namespace OCCAD;

public sealed class ArcTool : CadDrawingTool, ICadPointInputTool
{
    private const string ThreePoints = "ThreePoints";
    private const string CenterStartEnd = "CenterStartEnd";
    private const string StartCenterEnd = "StartCenterEnd";
    private const string StartEndCenter = "StartEndCenter";
    private const string StartEndPoint = "StartEndPoint";

    private readonly List<OcctPoint3d> _points = [];
    private string _method = ThreePoints;
    private bool _clockwise;
    private OcctPoint3d _planeOrigin;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private CadEntity? _preview;

    public override string Id => "arc";
    public override string DisplayName => "Arc";
    public override string PrecisionLengthLabel =>
        _method is CenterStartEnd or StartCenterEnd && Stage == 1
            ? "Radius"
            : base.PrecisionLengthLabel;

    public override CadToolPanelDescriptor ParameterPanel =>
        new("Arc", BuildParameters());

    protected override bool CanStepBackCore => _points.Count > 0;

    protected override void OnActivated()
    {
        _points.Clear();
        _preview = null;
        _clockwise = false;
        _planeOrigin = Context.WorkPlane.Origin;
        _xAxis = Context.WorkPlane.XAxis;
        _yAxis = Context.WorkPlane.YAxis;
        RestorePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;

        if (input.Kind == OcctPointerInputKind.Moved && _points.Count > 0)
        {
            UpdatePreview(ProjectToDrawingPlane(Context.ResolvePoint(input.X, input.Y, PrecisionReferencePoint).Point));
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(ProjectToDrawingPlane(
            Context.ResolvePoint(input.X, input.Y, PrecisionReferencePoint).Point));
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(
            pointer,
            PrecisionReferencePoint,
            point => AcceptPoint(ProjectToDrawingPlane(point)));

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(ProjectToDrawingPlane(point));

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Method", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage != 0) return false;

            var normalized = value.Trim();
            if (normalized is not (ThreePoints or CenterStartEnd or StartCenterEnd or StartEndCenter or StartEndPoint))
                return false;

            _method = normalized;
            _clockwise = false;
            RestorePrecisionFrame();
            RestorePrompt();
            NotifyUpdated();
            return true;
        }

        if (id.Equals("Clockwise", StringComparison.OrdinalIgnoreCase) &&
            _method is CenterStartEnd or StartCenterEnd or StartEndCenter &&
            bool.TryParse(value, out var clockwise))
        {
            _clockwise = clockwise;
            RefreshPreviewFromLastPointer();
            NotifyUpdated();
            return true;
        }

        return false;
    }

    protected override bool OnStepBack()
    {
        if (_points.Count == 0) return false;
        _points.RemoveAt(_points.Count - 1);
        _preview = null;
        Context.Preview.Clear();
        RestorePrecisionFrame();
        RestorePrompt();
        return true;
    }

    protected override void OnCanceled() => Reset();

    private IReadOnlyList<CadToolParameterDescriptor> BuildParameters()
    {
        var parameters = new List<CadToolParameterDescriptor>(2);
        if (Stage == 0)
        {
            parameters.Add(new CadChoiceToolParameterDescriptor(
                "Method",
                "Method",
                _method,
                [ThreePoints, CenterStartEnd, StartCenterEnd, StartEndCenter, StartEndPoint]));
        }

        if (_method is CenterStartEnd or StartCenterEnd or StartEndCenter)
        {
            parameters.Add(new CadBooleanToolParameterDescriptor(
                "Clockwise",
                "Clockwise",
                _clockwise));
        }

        return parameters;
    }

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (_points.Count > 0 && _points[^1].DistanceTo(point) <= 1e-9)
            return true;

        _points.Add(point);
        RestorePrecisionFrame();
        if (_points.Count < 3)
        {
            RestorePrompt();
            return true;
        }

        if (!TryCreateArc(_points[0], _points[1], _points[2], out var arc))
        {
            _points.RemoveAt(_points.Count - 1);
            RestorePrecisionFrame();
            SetPromptLocalized(
                "Cad.Prompt.Arc.Invalid",
                "Arc: points do not define a valid arc.",
                _method == ThreePoints
                    ? CadPrecisionInputKind.LengthAndAngle
                    : CadPrecisionInputKind.Angle);
            return true;
        }

        Context.AddEntity(arc);
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void UpdatePreview(OcctPoint3d cursor)
    {
        if (_points.Count == 1)
        {
            if (_points[0].DistanceTo(cursor) <= 1e-9) return;
            _preview = new CadLineEntity(_points[0], cursor);
            Context.Preview.Show(_preview);
            return;
        }

        if (_points.Count < 2) return;
        if (TryCreateArc(_points[0], _points[1], cursor, out var arc))
            _preview = arc;
        else if (_points[1].DistanceTo(cursor) > 1e-9)
            _preview = new CadPolylineEntity([_points[0], _points[1], cursor]);
        else
            return;

        Context.Preview.Show(_preview);
    }

    private void RefreshPreviewFromLastPointer()
    {
        if (_points.Count == 0 || Context.Workspace.LastPointerPosition is not { } pointer)
            return;

        var point = Context.ResolvePoint(
            pointer.X,
            pointer.Y,
            PrecisionReferencePoint).Point;
        UpdatePreview(ProjectToDrawingPlane(point));
    }

    private bool TryCreateArc(
        OcctPoint3d first,
        OcctPoint3d second,
        OcctPoint3d third,
        out CadArcEntity arc)
    {
        try
        {
            switch (_method)
            {
                case ThreePoints:
                    arc = new CadArcEntity(first, second, third);
                    return true;

                case CenterStartEnd:
                    return TryCreateCenterArc(first, second, third, out arc);

                case StartCenterEnd:
                    return TryCreateCenterArc(second, first, third, out arc);

                case StartEndCenter:
                    var adjustedCenter = AdjustCenterForEndpoints(
                        first,
                        second,
                        third);
                    return TryCreateCenterArc(
                        adjustedCenter,
                        first,
                        second,
                        out arc);

                case StartEndPoint:
                    arc = new CadArcEntity(first, third, second);
                    return true;

                default:
                    arc = null!;
                    return false;
            }
        }
        catch (ArgumentException)
        {
            arc = null!;
            return false;
        }
    }

    private OcctPoint3d AdjustCenterForEndpoints(
        OcctPoint3d start,
        OcctPoint3d end,
        OcctPoint3d centerInput)
    {
        var startLocal = ToLocal(_planeOrigin, start);
        var endLocal = ToLocal(_planeOrigin, end);
        var centerLocal = ToLocal(_planeOrigin, centerInput);

        var midpointX = (startLocal.X + endLocal.X) * 0.5;
        var midpointY = (startLocal.Y + endLocal.Y) * 0.5;
        var chordX = endLocal.X - startLocal.X;
        var chordY = endLocal.Y - startLocal.Y;
        var chordLength = Math.Sqrt(chordX * chordX + chordY * chordY);
        if (chordLength <= 1e-9)
            return centerInput;

        var perpendicularX = -chordY / chordLength;
        var perpendicularY = chordX / chordLength;
        var inputX = centerLocal.X - midpointX;
        var inputY = centerLocal.Y - midpointY;
        var height =
            inputX * perpendicularX +
            inputY * perpendicularY;

        return _planeOrigin +
               _xAxis * (midpointX + perpendicularX * height) +
               _yAxis * (midpointY + perpendicularY * height);
    }

    private bool TryCreateCenterArc(
        OcctPoint3d center,
        OcctPoint3d start,
        OcctPoint3d endInput,
        out CadArcEntity arc)
    {
        var startLocal = ToLocal(center, start);
        var endLocal = ToLocal(center, endInput);
        var radius = Math.Sqrt(startLocal.X * startLocal.X + startLocal.Y * startLocal.Y);
        var endLength = Math.Sqrt(endLocal.X * endLocal.X + endLocal.Y * endLocal.Y);
        if (radius <= 1e-9 || endLength <= 1e-9)
        {
            arc = null!;
            return false;
        }

        var startAngle = Math.Atan2(startLocal.Y, startLocal.X);
        var endAngle = Math.Atan2(endLocal.Y, endLocal.X);
        var sweep = _clockwise
            ? -CadPrecisionSnapGeometry.NormalizePositive(startAngle - endAngle)
            : CadPrecisionSnapGeometry.NormalizePositive(endAngle - startAngle);
        if (Math.Abs(sweep) <= 1e-9 ||
            Math.Abs(Math.Abs(sweep) - Math.PI * 2.0) <= 1e-9)
        {
            arc = null!;
            return false;
        }

        var end = center +
                  _xAxis * (Math.Cos(endAngle) * radius) +
                  _yAxis * (Math.Sin(endAngle) * radius);
        var middleAngle = startAngle + sweep * 0.5;
        var middle = center +
                     _xAxis * (Math.Cos(middleAngle) * radius) +
                     _yAxis * (Math.Sin(middleAngle) * radius);
        arc = new CadArcEntity(start, middle, end);
        return true;
    }

    private CadPlanePoint ToLocal(OcctPoint3d center, OcctPoint3d point)
    {
        var delta = CadTransformMath.Between(center, point);
        return new CadPlanePoint(
            CadTransformMath.Dot(delta, _xAxis),
            CadTransformMath.Dot(delta, _yAxis));
    }

    private void RestorePrecisionFrame()
    {
        if (_points.Count == 0)
        {
            SetWorkPlane(_planeOrigin, _xAxis, _yAxis, lockPlane: false);
            return;
        }

        if (_method == CenterStartEnd)
        {
            var center = _points[0];
            if (_points.Count >= 2 && TryRadialAxes(center, _points[1], out var radialX, out var radialY))
            {
                SetWorkPlane(center, radialX, radialY);
                return;
            }

            SetWorkPlane(center, _xAxis, _yAxis);
            return;
        }

        if (_method == StartCenterEnd && _points.Count >= 2)
        {
            var center = _points[1];
            if (TryRadialAxes(center, _points[0], out var radialX, out var radialY))
            {
                SetWorkPlane(center, radialX, radialY);
                return;
            }

            SetWorkPlane(center, _xAxis, _yAxis);
            return;
        }

        if (_method == StartEndCenter)
        {
            SetWorkPlane(
                _points.Count >= 2 ? _points[1] : _points[0],
                _xAxis,
                _yAxis,
                lockPlane: _points.Count >= 2);
            return;
        }

        SetWorkPlane(_points[^1], _xAxis, _yAxis);
    }

    private bool TryRadialAxes(
        OcctPoint3d center,
        OcctPoint3d radialPoint,
        out OcctVector3d xAxis,
        out OcctVector3d yAxis)
    {
        var delta = CadTransformMath.Between(center, radialPoint);
        var x = CadTransformMath.Dot(delta, _xAxis);
        var y = CadTransformMath.Dot(delta, _yAxis);
        var planar = new OcctVector3d(
            _xAxis.X * x + _yAxis.X * y,
            _xAxis.Y * x + _yAxis.Y * y,
            _xAxis.Z * x + _yAxis.Z * y);
        if (!planar.TryNormalize(out xAxis))
        {
            yAxis = default;
            return false;
        }

        var normal = _xAxis.Cross(_yAxis).Normalized();
        yAxis = normal.Cross(xAxis).Normalized();
        return true;
    }

    private void RestorePrompt()
    {
        var (key, fallback, precision) = (_method, _points.Count) switch
        {
            (ThreePoints, 0) => ("Cad.Prompt.Arc.First", "Arc: specify first point [Esc cancel]", CadPrecisionInputKind.None),
            (ThreePoints, 1) => ("Cad.Prompt.Arc.Second", "Arc: specify second point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            (ThreePoints, _) => ("Cad.Prompt.Arc.Third", "Arc: specify third point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            (CenterStartEnd, 0) => ("Cad.Prompt.Arc.Center", "Arc: specify center [Esc cancel]", CadPrecisionInputKind.None),
            (CenterStartEnd, 1) => ("Cad.Prompt.Arc.Start", "Arc: specify start point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            (CenterStartEnd, _) => ("Cad.Prompt.Arc.End", "Arc: specify end point [Backspace undo, Esc cancel]", CadPrecisionInputKind.Angle),
            (StartCenterEnd, 0) => ("Cad.Prompt.Arc.Start", "Arc: specify start point [Esc cancel]", CadPrecisionInputKind.None),
            (StartCenterEnd, 1) => ("Cad.Prompt.Arc.Center", "Arc: specify center [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            (StartEndCenter, 0) => ("Cad.Prompt.Arc.Start", "Arc: specify start point [Esc cancel]", CadPrecisionInputKind.None),
            (StartEndCenter, 1) => ("Cad.Prompt.Arc.End", "Arc: specify end point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            (StartEndCenter, _) => ("Cad.Prompt.Arc.CenterForEndpoints", "Arc: specify center side/position [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length),
            (StartEndPoint, 0) => ("Cad.Prompt.Arc.Start", "Arc: specify start point [Esc cancel]", CadPrecisionInputKind.None),
            (StartEndPoint, 1) => ("Cad.Prompt.Arc.End", "Arc: specify end point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            (StartEndPoint, _) => ("Cad.Prompt.Arc.PointOnArc", "Arc: specify a point on the arc [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            _ => ("Cad.Prompt.Arc.End", "Arc: specify end point [Backspace undo, Esc cancel]", CadPrecisionInputKind.Angle)
        };
        SetStageLocalized(_points.Count, key, fallback, precision);
    }

    private OcctPoint3d ProjectToDrawingPlane(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_planeOrigin, point, _xAxis, _yAxis);

    private void Reset()
    {
        _points.Clear();
        _preview = null;
        _clockwise = false;
    }
}
