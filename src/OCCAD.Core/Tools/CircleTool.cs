using OcctNet;

namespace OCCAD;

public sealed class CircleTool : CadDrawingTool, ICadPointInputTool
{
    private const string CenterRadius = "CenterRadius";
    private const string CenterDiameter = "CenterDiameter";
    private const string TwoPoints = "TwoPoints";
    private const string ThreePoints = "ThreePoints";
    private const string PointCenter = "PointCenter";

    private readonly List<OcctPoint3d> _points = [];
    private string _method = CenterRadius;
    private double? _radius;
    private double? _diameter;
    private OcctPoint3d _planeOrigin;
    private OcctVector3d _normal;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private CadCircleEntity? _preview;
    private bool _invalidPointPrompt;

    public override string Id => "circle";
    public override string DisplayName => "Circle";
    public override string PrecisionLengthLabel =>
        Stage == 1
            ? _method switch
            {
                CenterRadius => "Radius",
                CenterDiameter or TwoPoints => "Diameter",
                PointCenter => "Radius",
                _ => base.PrecisionLengthLabel
            }
            : base.PrecisionLengthLabel;

    public override CadToolParameterSchema ParameterSchema
    {
        get
        {
            var parameters = new List<CadToolParameterDescriptor>();
            if (Stage == 0)
            {
                parameters.Add(new CadChoiceToolParameterDescriptor(
                    "Method", "Method", _method,
                    [CenterRadius, CenterDiameter, TwoPoints, ThreePoints, PointCenter]));
            }

            if (_method == CenterRadius)
                parameters.Add(new CadOptionalDoubleToolParameterDescriptor("Radius", "Radius", _radius, 1e-9, double.MaxValue));
            else if (_method == CenterDiameter)
                parameters.Add(new CadOptionalDoubleToolParameterDescriptor("Diameter", "Diameter", _diameter, 2e-9, double.MaxValue));

            return new CadToolParameterSchema("Circle", parameters);
        }
    }

    protected override bool CanStepBackCore => _points.Count > 0;

    public override bool CanCommitCurrentStage =>
        IsActive &&
        State == CadToolState.Drawing &&
        TryResolveExactInputPoint(out _)
            ? true
            : base.CanCommitCurrentStage;

    protected override void OnActivated()
    {
        _points.Clear();
        _preview = null;
        _radius = null;
        _diameter = null;
        _invalidPointPrompt = false;
        _planeOrigin = Context.WorkPlane.Origin;
        _normal = Context.WorkPlane.Normal;
        _xAxis = Context.WorkPlane.XAxis;
        _yAxis = Context.WorkPlane.YAxis;
        RestorePrompt();
    }

    protected internal override void OnWorkPlaneChanged()
    {
        _planeOrigin = Context.WorkPlane.Origin;
        _normal = Context.WorkPlane.Normal;
        _xAxis = Context.WorkPlane.XAxis;
        _yAxis = Context.WorkPlane.YAxis;
        base.OnWorkPlaneChanged();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && _points.Count > 0)
        {
            UpdatePreview(ProjectToDrawingPlane(Resolve(input, _points[^1])));
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;
        return AcceptPoint(ProjectToDrawingPlane(Resolve(input, _points.Count == 0 ? null : _points[^1])));
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer)
    {
        if (TryResolveExactInputPoint(out var exactPoint))
            return AcceptPoint(ProjectToDrawingPlane(exactPoint));

        return CommitResolvedPoint(
            pointer,
            _points.Count == 0 ? null : _points[^1],
            point => AcceptPoint(ProjectToDrawingPlane(point)));
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(ProjectToDrawingPlane(point));

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Method", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage != 0) return false;
            var normalized = value.Trim();
            if (normalized is not (CenterRadius or CenterDiameter or TwoPoints or ThreePoints or PointCenter))
                return false;
            if (string.Equals(_method, normalized, StringComparison.OrdinalIgnoreCase)) return true;
            _method = normalized;
            _radius = null;
            _diameter = null;
            _invalidPointPrompt = false;
            RestorePrompt();
            NotifyUpdated();
            return true;
        }

        if (id.Equals("Radius", StringComparison.OrdinalIgnoreCase) && _method == CenterRadius)
        {
            if (!TryOptionalPositive(value, out _radius)) return false;
            RefreshPreviewFromInput();
            NotifyUpdated();
            return true;
        }

        if (id.Equals("Diameter", StringComparison.OrdinalIgnoreCase) && _method == CenterDiameter)
        {
            if (!TryOptionalPositive(value, out _diameter)) return false;
            RefreshPreviewFromInput();
            NotifyUpdated();
            return true;
        }

        return false;
    }

    protected override bool OnPrecisionInputApplied(CadPrecisionInput input)
    {
        if (TryResolveExactInputPoint(out var exactPoint))
        {
            UpdatePreview(ProjectToDrawingPlane(exactPoint));
            return true;
        }

        return base.OnPrecisionInputApplied(input);
    }

    protected override bool OnStepBack()
    {
        if (_points.Count == 0) return false;
        _points.RemoveAt(_points.Count - 1);
        _preview = null;
        _invalidPointPrompt = false;
        Context.Preview.Clear();
        Context.WorkPlane.SetOrigin(_points.Count == 0 ? _planeOrigin : _points[^1]);
        RestorePrompt();
        return true;
    }

    protected override void OnCanceled() => Reset();

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (_points.Count > 0 && _points[^1].DistanceTo(point) <= 1e-9) return false;

        _points.Add(point);
        Context.WorkPlane.SetOrigin(point);

        switch (_method)
        {
            case CenterRadius:
            case CenterDiameter:
                if (_points.Count == 1) { RestorePrompt(); RefreshPreviewFromInput(); return true; }
                return CommitCenterCircle(point);
            case TwoPoints:
                if (_points.Count == 1) { RestorePrompt(); return true; }
                return CommitTwoPointCircle();
            case ThreePoints:
                if (_points.Count < 3) { RestorePrompt(); RefreshPreviewFromInput(); return true; }
                return CommitThreePointCircle();
            case PointCenter:
                if (_points.Count == 1) { RestorePrompt(); return true; }
                return CommitPointCenterCircle();
            default:
                return false;
        }
    }

    private bool CommitCenterCircle(OcctPoint3d point)
    {
        var center = _points[0];
        var mouseDistance = CadPlaneGeometry.RadialDistance(center, point, _xAxis, _yAxis);
        var radius = _method == CenterDiameter
            ? (_diameter ?? mouseDistance) * 0.5
            : _radius ?? mouseDistance;
        if (!double.IsFinite(radius) || radius <= 1e-9)
        {
            _points.RemoveAt(_points.Count - 1);
            return false;
        }
        Commit(new CadCircleEntity(center, _normal, radius));
        return true;
    }

    private bool CommitTwoPointCircle()
    {
        var first = _points[0];
        var second = _points[1];
        var radius = first.DistanceTo(second) * 0.5;
        if (!double.IsFinite(radius) || radius <= 1e-9)
        {
            _points.RemoveAt(_points.Count - 1);
            return false;
        }
        Commit(new CadCircleEntity(Midpoint(first, second), _normal, radius));
        return true;
    }

    private bool CommitPointCenterCircle()
    {
        var edge = _points[0];
        var center = _points[1];
        var radius = CadPlaneGeometry.RadialDistance(center, edge, _xAxis, _yAxis);
        if (!double.IsFinite(radius) || radius <= 1e-9)
        {
            _points.RemoveAt(_points.Count - 1);
            return false;
        }
        Commit(new CadCircleEntity(center, _normal, radius));
        return true;
    }

    private bool CommitThreePointCircle()
    {
        if (!TryCircleFromThreePoints(_points[0], _points[1], _points[2], out var center, out var radius))
        {
            _points.RemoveAt(_points.Count - 1);
            _invalidPointPrompt = true;
            SetPromptLocalized("Cad.Prompt.Circle.Invalid", "Circle: points do not define a valid circle.", CadPrecisionInputKind.LengthAndAngle);
            return false;
        }
        _invalidPointPrompt = false;
        Commit(new CadCircleEntity(center, _normal, radius));
        return true;
    }

    private void UpdatePreview(OcctPoint3d cursor)
    {
        CadCircleEntity? entity = null;
        try
        {
            switch (_method)
            {
                case CenterRadius:
                case CenterDiameter when _points.Count >= 1:
                {
                    var mouseDistance = CadPlaneGeometry.RadialDistance(_points[0], cursor, _xAxis, _yAxis);
                    var radius = _method == CenterDiameter ? (_diameter ?? mouseDistance) * 0.5 : _radius ?? mouseDistance;
                    if (double.IsFinite(radius) && radius > 1e-9)
                        entity = new CadCircleEntity(_points[0], _normal, radius);
                    break;
                }
                case TwoPoints when _points.Count >= 1:
                {
                    var radius = _points[0].DistanceTo(cursor) * 0.5;
                    if (double.IsFinite(radius) && radius > 1e-9)
                        entity = new CadCircleEntity(Midpoint(_points[0], cursor), _normal, radius);
                    break;
                }
                case ThreePoints when _points.Count >= 2:
                    if (TryCircleFromThreePoints(_points[0], _points[1], cursor, out var center, out var threePointRadius))
                    {
                        if (_invalidPointPrompt) { _invalidPointPrompt = false; RestorePrompt(); }
                        entity = new CadCircleEntity(center, _normal, threePointRadius);
                    }
                    break;
                case PointCenter when _points.Count >= 1:
                {
                    var radius = CadPlaneGeometry.RadialDistance(cursor, _points[0], _xAxis, _yAxis);
                    if (double.IsFinite(radius) && radius > 1e-9)
                        entity = new CadCircleEntity(cursor, _normal, radius);
                    break;
                }
            }
        }
        catch (ArgumentException)
        {
            entity = null;
        }

        if (entity is null)
        {
            _preview = null;
            Context.Preview.Clear();
            return;
        }
        _preview = entity;
        ShowPreview(entity);
    }

    private void RefreshPreviewFromInput()
    {
        if (_points.Count == 0)
            return;

        if (TryResolveExactInputPoint(out var exactPoint))
        {
            UpdatePreview(ProjectToDrawingPlane(exactPoint));
            return;
        }

        if (Context.Workspace.LastPointerPosition is not { } pointer)
            return;

        var point = Context.ResolvePoint(pointer.X, pointer.Y, _points[^1]).Point;
        UpdatePreview(ProjectToDrawingPlane(point));
    }

    private bool TryResolveExactInputPoint(out OcctPoint3d point)
    {
        point = default;
        if (_points.Count == 0)
            return false;

        var reference = _points[^1];
        if (_points.Count == 1)
        {
            if (_method == CenterRadius && _radius is { } radius && radius > 1e-9)
            {
                point = _points[0] + _xAxis * radius;
                return point.IsFinite;
            }

            if (_method == CenterDiameter && _diameter is { } diameter && diameter > 2e-9)
            {
                point = _points[0] + _xAxis * diameter;
                return point.IsFinite;
            }

            if (Context.Workspace.Drafting.LengthLockEnabled &&
                Context.Workspace.Drafting.LockedLength > 1e-9 &&
                _method is CenterRadius or CenterDiameter or TwoPoints or PointCenter)
            {
                point = _points[0] +
                        _xAxis * Context.Workspace.Drafting.LockedLength;
                return point.IsFinite;
            }
        }

        return _method == ThreePoints &&
               CadExactInputGeometry.TryResolveLengthAnglePoint(
                   Context.Workspace,
                   reference,
                   out point);
    }

    private void RestorePrompt()
    {
        var (key, fallback, precision) = (_method, _points.Count) switch
        {
            (CenterRadius, 0) => ("Cad.Prompt.Circle.Center", "Circle: specify center [Esc cancel]", CadPrecisionInputKind.None),
            (CenterRadius, _) => ("Cad.Prompt.Circle.Radius", "Circle: specify radius [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length),
            (CenterDiameter, 0) => ("Cad.Prompt.Circle.Center", "Circle: specify center [Esc cancel]", CadPrecisionInputKind.None),
            (CenterDiameter, _) => ("Cad.Prompt.Circle.Diameter", "Circle: specify diameter [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length),
            (TwoPoints, 0) => ("Cad.Prompt.Circle.FirstDiameterPoint", "Circle: specify first diameter point [Esc cancel]", CadPrecisionInputKind.None),
            (TwoPoints, _) => ("Cad.Prompt.Circle.SecondDiameterPoint", "Circle: specify second diameter point [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length),
            (ThreePoints, 0) => ("Cad.Prompt.Circle.FirstPoint", "Circle: specify first point [Esc cancel]", CadPrecisionInputKind.None),
            (ThreePoints, 1) => ("Cad.Prompt.Circle.SecondPoint", "Circle: specify second point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle),
            (PointCenter, 0) => ("Cad.Prompt.Circle.EdgePoint", "Circle: specify a point on the circle [Esc cancel]", CadPrecisionInputKind.None),
            (PointCenter, _) => ("Cad.Prompt.Circle.CenterAfterPoint", "Circle: specify center [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length),
            _ => ("Cad.Prompt.Circle.ThirdPoint", "Circle: specify third point [Backspace undo, Esc cancel]", CadPrecisionInputKind.LengthAndAngle)
        };
        SetStageLocalized(_points.Count, key, fallback, precision);
    }

    private OcctPoint3d ProjectToDrawingPlane(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_planeOrigin, point, _xAxis, _yAxis);

    private bool TryCircleFromThreePoints(OcctPoint3d first, OcctPoint3d second, OcctPoint3d third, out OcctPoint3d center, out double radius)
    {
        var a = ToPlane(first);
        var b = ToPlane(second);
        var c = ToPlane(third);
        var d = 2.0 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (!double.IsFinite(d) || Math.Abs(d) <= 1e-12)
        {
            center = default;
            radius = 0.0;
            return false;
        }
        var aa = a.X * a.X + a.Y * a.Y;
        var bb = b.X * b.X + b.Y * b.Y;
        var cc = c.X * c.X + c.Y * c.Y;
        var x = (aa * (b.Y - c.Y) + bb * (c.Y - a.Y) + cc * (a.Y - b.Y)) / d;
        var y = (aa * (c.X - b.X) + bb * (a.X - c.X) + cc * (b.X - a.X)) / d;
        center = _planeOrigin + _xAxis * x + _yAxis * y;
        radius = Math.Sqrt((x - a.X) * (x - a.X) + (y - a.Y) * (y - a.Y));
        return center.IsFinite && double.IsFinite(radius) && radius > 1e-9;
    }

    private CadPlanePoint ToPlane(OcctPoint3d point)
    {
        var delta = CadTransformMath.Between(_planeOrigin, point);
        return new CadPlanePoint(CadTransformMath.Dot(delta, _xAxis), CadTransformMath.Dot(delta, _yAxis));
    }

    private void Commit(CadCircleEntity entity)
    {
        _points.Clear();
        _preview = null;
        CommitPreview(entity);
    }

    private void Reset()
    {
        _points.Clear();
        _preview = null;
        _radius = null;
        _diameter = null;
        _invalidPointPrompt = false;
    }

    private static bool TryOptionalPositive(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!CadValueTextConverter.TryParseFiniteDouble(text, out var number) ||
            number <= 0.0)
        {
            value = null;
            return false;
        }

        value = number;
        return true;
    }

    private static OcctPoint3d Midpoint(OcctPoint3d first, OcctPoint3d second) =>
        new((first.X + second.X) * 0.5, (first.Y + second.Y) * 0.5, (first.Z + second.Z) * 0.5);
}
