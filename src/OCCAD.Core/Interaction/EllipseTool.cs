using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class EllipseTool : CadDrawingTool, ICadPointInputTool
{
    private const string CenterMajorMinor = "CenterMajorMinor";
    private const string AxisEndpointsMinor = "AxisEndpointsMinor";

    private readonly List<OcctPoint3d> _points = [];
    private string _method = CenterMajorMinor;
    private WorkPlaneFrame _initialPlane;
    private OcctPoint3d _planeOrigin;
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private OcctVector3d _planeX;
    private OcctVector3d _planeY;
    private OcctVector3d _majorAxis;
    private double _majorRadius;
    private double? _majorRadiusParameter;
    private double? _minorRadiusParameter;
    private double? _angleDegrees;
    private CadEllipseEntity? _preview;

    public override string Id => "ellipse";
    public override string DisplayName => "Ellipse";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Ellipse",
            [
                new CadChoiceToolParameterDescriptor(
                    "Method",
                    "Method",
                    _method,
                    [CenterMajorMinor, AxisEndpointsMinor]),
                new CadOptionalDoubleToolParameterDescriptor(
                    "MajorRadius",
                    "Major Radius",
                    _majorRadiusParameter,
                    1e-9,
                    double.MaxValue),
                new CadOptionalDoubleToolParameterDescriptor(
                    "MinorRadius",
                    "Minor Radius",
                    _minorRadiusParameter,
                    1e-9,
                    double.MaxValue),
                new CadOptionalDoubleToolParameterDescriptor(
                    "Angle",
                    "Angle",
                    _angleDegrees,
                    -360000.0,
                    360000.0)
            ]);

    protected override bool CanStepBackCore => _points.Count > 0;

    protected override void OnActivated()
    {
        Reset();
        _initialPlane = CaptureWorkPlaneFrame();
        _planeOrigin = Context.WorkPlane.Origin;
        _normal = Context.WorkPlane.Normal;
        _planeX = Context.WorkPlane.XAxis;
        _planeY = Context.WorkPlane.YAxis;
        RestorePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && _points.Count > 0)
        {
            UpdatePreview(ProjectToDrawingPlane(Context.ResolvePoint(input.X, input.Y, ReferencePoint()).Point));
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;
        return AcceptPoint(ProjectToDrawingPlane(
            Context.ResolvePoint(input.X, input.Y, ReferencePoint()).Point));
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(
            pointer,
            ReferencePoint(),
            point => AcceptPoint(ProjectToDrawingPlane(point)));

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && AcceptPoint(ProjectToDrawingPlane(point));

    protected override bool OnSetParameter(string id, string value)
    {
        if (id.Equals("Method", StringComparison.OrdinalIgnoreCase))
        {
            if (Stage != 0) return false;
            var normalized = value.Trim();
            if (normalized is not (CenterMajorMinor or AxisEndpointsMinor))
                return false;
            if (string.Equals(_method, normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            _method = normalized;
            RefreshParameterStateAndPreview();
            NotifyUpdated();
            return true;
        }

        if (id.Equals("MajorRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalPositive(value, out var major)) return false;
            if (major is { } majorValue &&
                _minorRadiusParameter is { } minorValue &&
                minorValue > majorValue)
                return false;
            _majorRadiusParameter = major;
        }
        else if (id.Equals("MinorRadius", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalPositive(value, out var minor)) return false;
            if (minor is { } minorValue &&
                _majorRadiusParameter is { } majorValue &&
                minorValue > majorValue)
                return false;
            _minorRadiusParameter = minor;
        }
        else if (id.Equals("Angle", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOptionalFinite(value, out _angleDegrees)) return false;
        }
        else
        {
            return false;
        }

        RefreshParameterStateAndPreview();
        NotifyUpdated();
        return true;
    }

    protected override bool OnStepBack()
    {
        if (_points.Count == 0) return false;
        _points.RemoveAt(_points.Count - 1);
        _preview = null;
        Context.Preview.Clear();
        RestoreConstructionState();
        RestorePrompt();
        return true;
    }

    protected override void OnCanceled() => Reset();

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (_points.Count > 0 && _points[^1].DistanceTo(point) <= 1e-9)
            return true;

        _points.Add(point);
        if (!RestoreConstructionState())
        {
            _points.RemoveAt(_points.Count - 1);
            return true;
        }

        if (_points.Count < 3)
        {
            RestorePrompt();
            return true;
        }

        var minor = _minorRadiusParameter ?? MinorRadius(point);
        if (!IsValidMinor(minor))
        {
            _points.RemoveAt(_points.Count - 1);
            SetPromptLocalized(
                "Cad.Prompt.Ellipse.InvalidMinor",
                "Ellipse: minor radius must be positive and not exceed major radius.");
            return true;
        }

        var entity = new CadEllipseEntity(
            _center,
            _normal,
            _majorAxis,
            _majorRadius,
            minor);
        ShowPreview(entity);
        Context.AddEntity(entity.Duplicate());
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private bool RestoreConstructionState()
    {
        if (_points.Count == 0)
            return true;

        if (_method == CenterMajorMinor)
        {
            _center = _points[0];
            if (_points.Count < 2)
                return true;
            return TrySetMajor(_center, _points[1], fullAxis: false);
        }

        if (_points.Count < 2)
            return true;

        var first = _points[0];
        if (!TrySetMajor(first, _points[1], fullAxis: true))
            return false;
        _center = first + _majorAxis * _majorRadius;
        return true;
    }

    private bool TrySetMajor(OcctPoint3d first, OcctPoint3d second, bool fullAxis)
    {
        var delta = CadTransformMath.Between(first, second);
        var x = CadTransformMath.Dot(delta, _planeX);
        var y = CadTransformMath.Dot(delta, _planeY);
        var length = Math.Sqrt(x * x + y * y);

        if (_angleDegrees is { } angle)
        {
            _majorAxis = AxisFromAngle(angle);
        }
        else
        {
            if (length <= 1e-9) return false;
            _majorAxis = new OcctVector3d(
                _planeX.X * x + _planeY.X * y,
                _planeX.Y * x + _planeY.Y * y,
                _planeX.Z * x + _planeY.Z * y).Normalized();
        }

        if (_majorRadiusParameter is { } majorRadius)
        {
            _majorRadius = majorRadius;
        }
        else
        {
            if (length <= 1e-9) return false;
            _majorRadius = fullAxis ? length * 0.5 : length;
        }

        return _minorRadiusParameter is not { } minorRadius || minorRadius <= _majorRadius;
    }

    private void UpdatePreview(OcctPoint3d cursor)
    {
        CadEllipseEntity? entity = null;

        if (_points.Count == 1)
        {
            if (_method == CenterMajorMinor)
            {
                if (TrySetMajor(_points[0], cursor, fullAxis: false))
                {
                    var minor = _minorRadiusParameter ?? _majorRadius;
                    if (IsValidMinor(minor))
                        entity = new CadEllipseEntity(
                            _points[0], _normal, _majorAxis, _majorRadius, minor);
                }
            }
            else if (TrySetMajor(_points[0], cursor, fullAxis: true))
            {
                _center = _points[0] + _majorAxis * _majorRadius;
                var minor = _minorRadiusParameter ?? _majorRadius;
                if (IsValidMinor(minor))
                    entity = new CadEllipseEntity(
                        _center, _normal, _majorAxis, _majorRadius, minor);
            }
        }
        else if (_points.Count >= 2 && RestoreConstructionState())
        {
            var minor = _minorRadiusParameter ?? MinorRadius(cursor);
            if (IsValidMinor(minor))
                entity = new CadEllipseEntity(
                    _center, _normal, _majorAxis, _majorRadius, minor);
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

    private void RefreshParameterStateAndPreview()
    {
        if (_points.Count >= 2)
        {
            if (!RestoreConstructionState())
            {
                _preview = null;
                Context.Preview.Clear();
                return;
            }
            RestoreMinorWorkPlane();
        }

        if (_points.Count == 0 ||
            Context.Workspace.LastPointerPosition is not { } pointer)
            return;

        var point = ProjectToDrawingPlane(
            Context.ResolvePoint(pointer.X, pointer.Y, ReferencePoint()).Point);
        UpdatePreview(point);
    }

    private double MinorRadius(OcctPoint3d point)
    {
        var minorAxis = _normal.Cross(_majorAxis).Normalized();
        return Math.Abs(CadTransformMath.Dot(
            CadTransformMath.Between(_center, point), minorAxis));
    }

    private bool IsValidMinor(double minor) =>
        double.IsFinite(minor) && minor > 1e-9 && minor <= _majorRadius;

    private OcctVector3d AxisFromAngle(double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new OcctVector3d(
            _planeX.X * cos + _planeY.X * sin,
            _planeX.Y * cos + _planeY.Y * sin,
            _planeX.Z * cos + _planeY.Z * sin).Normalized();
    }

    private OcctPoint3d? ReferencePoint() =>
        _points.Count >= 2 ? _center : _points.Count == 1 ? _points[0] : null;

    private void RestorePrompt()
    {
        var (key, fallback) = (_method, _points.Count) switch
        {
            (CenterMajorMinor, 0) => ("Cad.Prompt.Ellipse.Center", "Ellipse: specify center [Esc cancel]"),
            (CenterMajorMinor, 1) => ("Cad.Prompt.Ellipse.Major", "Ellipse: specify major-axis point [Backspace undo, Esc cancel]"),
            (CenterMajorMinor, _) => ("Cad.Prompt.Ellipse.Minor", "Ellipse: specify minor-axis distance [Backspace undo, Esc cancel]"),
            (AxisEndpointsMinor, 0) => ("Cad.Prompt.Ellipse.AxisFirst", "Ellipse: specify first major-axis endpoint [Esc cancel]"),
            (AxisEndpointsMinor, 1) => ("Cad.Prompt.Ellipse.AxisSecond", "Ellipse: specify second major-axis endpoint [Backspace undo, Esc cancel]"),
            _ => ("Cad.Prompt.Ellipse.Minor", "Ellipse: specify minor-axis distance [Backspace undo, Esc cancel]")
        };

        if (_points.Count == 0)
            RestoreWorkPlaneFrame(_initialPlane);
        else if (_points.Count == 1)
            RestoreWorkPlaneFrame(_initialPlane, _points[0]);

        SetStageLocalized(_points.Count, key, fallback);

        if (_points.Count >= 2 && RestoreConstructionState())
            RestoreMinorWorkPlane();
    }

    private void RestoreMinorWorkPlane()
    {
        var minorAxis = _normal.Cross(_majorAxis).Normalized();
        SetWorkPlane(_center, minorAxis, _majorAxis);
        LockStageAngle(0.0);
    }

    private OcctPoint3d ProjectToDrawingPlane(OcctPoint3d point) =>
        CadPlaneGeometry.ProjectToPlane(_planeOrigin, point, _planeX, _planeY);

    private void Reset()
    {
        _points.Clear();
        _preview = null;
        _majorRadiusParameter = null;
        _minorRadiusParameter = null;
        _angleDegrees = null;
    }

    private static bool TryOptionalPositive(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!TryFinite(text, out var parsed) || parsed <= 0.0)
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryOptionalFinite(string text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (!TryFinite(text, out var parsed))
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryFinite(string text, out double value)
    {
        var parsed = double.TryParse(
                         text,
                         NumberStyles.Float,
                         CultureInfo.CurrentCulture,
                         out value) ||
                     double.TryParse(
                         text,
                         NumberStyles.Float,
                         CultureInfo.InvariantCulture,
                         out value);
        return parsed && double.IsFinite(value);
    }
}
