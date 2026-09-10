using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadArcEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private OcctVector3d _xAxis;
    private double _radius;
    private double _startAngleDegrees;
    private double _sweepAngleDegrees;

    public CadArcEntity(OcctPoint3d start, OcctPoint3d middle, OcctPoint3d end) : base("Arc")
    {
        SetFromThreePoints(start, middle, end, raiseEvents: false);
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    private CadArcEntity(
        OcctPoint3d center,
        OcctVector3d normal,
        OcctVector3d xAxis,
        double radius,
        double startAngleDegrees,
        double sweepAngleDegrees) : base("Arc")
    {
        _center = center;
        _normal = normal;
        _xAxis = xAxis;
        _radius = radius;
        _startAngleDegrees = startAngleDegrees;
        _sweepAngleDegrees = sweepAngleDegrees;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d Normal => _normal;
    [Browsable(false)] public OcctVector3d XAxis => _xAxis;
    [Browsable(false)] public OcctPoint3d Start => PointAt(_startAngleDegrees);
    [Browsable(false)] public OcctPoint3d Middle => PointAt(_startAngleDegrees + _sweepAngleDegrees * 0.5);
    [Browsable(false)] public OcctPoint3d End => PointAt(_startAngleDegrees + _sweepAngleDegrees);

    [Browsable(false)] public double NormalX => _normal.X;
    [Browsable(false)] public double NormalY => _normal.Y;
    [Browsable(false)] public double NormalZ => _normal.Z;
    [Browsable(false)] public double XAxisX => _xAxis.X;
    [Browsable(false)] public double XAxisY => _xAxis.Y;
    [Browsable(false)] public double XAxisZ => _xAxis.Z;

    [Category("Geometry")] public double CenterX { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double CenterY { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double CenterZ { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }

    [Category("Geometry")]
    public double Radius
    {
        get => _radius;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _radius, value);
        }
    }

    [Category("Geometry")]
    public double StartAngleDegrees
    {
        get => _startAngleDegrees;
        set
        {
            ValidateFinite(value, nameof(value));
            SetGeometry(ref _startAngleDegrees, NormalizeDegrees(value));
        }
    }

    [Category("Geometry")]
    public double EndAngleDegrees
    {
        get => NormalizeDegrees(_startAngleDegrees + _sweepAngleDegrees);
        set
        {
            ValidateFinite(value, nameof(value));
            var sweep = SweepBetween(
                _startAngleDegrees,
                NormalizeDegrees(value),
                _sweepAngleDegrees < 0.0);
            if (Math.Abs(sweep) <= 1e-9)
                throw new ArgumentException("Arc start and end angles must be distinct.", nameof(value));
            SetGeometry(ref _sweepAngleDegrees, sweep);
        }
    }

    [Category("Geometry"), ReadOnly(true)]
    public double SweepAngleDegrees => _sweepAngleDegrees;

    [Category("Measurement"), ReadOnly(true)]
    public double ArcLength =>
        _radius * Math.Abs(_sweepAngleDegrees) * Math.PI / 180.0;

    internal override OcctShape BuildShape(OcctEngine engine) =>
        engine.MakeArc(Start, Middle, End);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        return CadPrecisionSnapGeometry.TryCreateArc(
            this,
            workPlane,
            out var curve)
            ? [curve]
            : Array.Empty<CadSnapCurve>();
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var plane = new CadSnapWorkPlane(_center, _xAxis, yAxis);
        var result = new List<CadSnapPoint>
        {
            new(this, _center, CadSnapType.Center, 0, plane),
            new(this, Start, CadSnapType.Endpoint, 1, plane),
            new(this, Middle, CadSnapType.Midpoint, 2, plane),
            new(this, End, CadSnapType.Endpoint, 3, plane)
        };

        var (quadrantX, quadrantY) = CadCircleEntity.PlaneAxes(_normal);
        var quadrantPoints = new[]
        {
            _center + quadrantX * _radius,
            _center + quadrantY * _radius,
            _center - quadrantX * _radius,
            _center - quadrantY * _radius
        };
        for (var index = 0; index < quadrantPoints.Length; index++)
        {
            if (!TryAngleOf(quadrantPoints[index], out var angle) || !ContainsAngle(angle))
                continue;
            result.Add(new CadSnapPoint(
                this,
                quadrantPoints[index],
                CadSnapType.Quadrant,
                4 + index,
                plane));
        }

        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var plane = new CadGripWorkPlane(_center, _xAxis, yAxis);
        return
        [
            new(this, 0, _center, plane, Kind: CadGripKind.Center),
            new(
                this,
                1,
                Start,
                plane,
                _center,
                CadPrecisionInputKind.Angle),
            new(
                this,
                2,
                End,
                plane,
                _center,
                CadPrecisionInputKind.Angle),
            new(
                this,
                3,
                Middle,
                plane,
                _center,
                CadPrecisionInputKind.Length)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        ValidatePoint(targetPoint, nameof(targetPoint));
        switch (index)
        {
            case 0:
                Translate(CadTransformMath.Between(_center, targetPoint));
                return;

            case 1:
                {
                    if (!TryAngleOf(targetPoint, out var angle)) return;
                    var endAngle = NormalizeDegrees(_startAngleDegrees + _sweepAngleDegrees);
                    var sweep = SweepBetween(angle, endAngle, _sweepAngleDegrees < 0.0);
                    if (Math.Abs(sweep) <= 1e-9) return;
                    _startAngleDegrees = angle;
                    _sweepAngleDegrees = sweep;
                    break;
                }

            case 2:
                {
                    if (!TryAngleOf(targetPoint, out var angle)) return;
                    var sweep = SweepBetween(
                        _startAngleDegrees,
                        angle,
                        _sweepAngleDegrees < 0.0);
                    if (Math.Abs(sweep) <= 1e-9) return;
                    _sweepAngleDegrees = sweep;
                    break;
                }

            case 3:
                {
                    var radius = DistanceInPlane(targetPoint);
                    if (radius <= 1e-9) return;
                    _radius = radius;
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    internal CadArcEntity CopyWithParameterRange(
        double startParameter,
        double endParameter)
    {
        if (!double.IsFinite(startParameter) ||
            !double.IsFinite(endParameter) ||
            startParameter < 0.0 ||
            endParameter > 1.0 ||
            endParameter - startParameter <= 1e-9)
            throw new ArgumentOutOfRangeException(
                nameof(endParameter));

        return CopyPropertiesTo(
            new CadArcEntity(
                _center,
                _normal,
                _xAxis,
                _radius,
                _startAngleDegrees +
                _sweepAngleDegrees * startParameter,
                _sweepAngleDegrees *
                (endParameter - startParameter)));
    }

    internal CadArcEntity CopyWithAngles(
        double startAngleDegrees,
        double sweepAngleDegrees)
    {
        if (!double.IsFinite(startAngleDegrees) ||
            !double.IsFinite(sweepAngleDegrees) ||
            Math.Abs(sweepAngleDegrees) <= 1e-9 ||
            Math.Abs(sweepAngleDegrees) >= 360.0 - 1e-9)
            throw new ArgumentOutOfRangeException(
                nameof(sweepAngleDegrees));

        return CopyPropertiesTo(
            new CadArcEntity(
                _center,
                _normal,
                _xAxis,
                _radius,
                NormalizeDegrees(startAngleDegrees),
                sweepAngleDegrees));
    }

    internal bool TryParameterAt(
        OcctPoint3d point,
        out double parameter)
    {
        if (!TryAngleOf(point, out var angle))
        {
            parameter = 0.0;
            return false;
        }

        var span = Math.Abs(_sweepAngleDegrees);
        var delta = _sweepAngleDegrees >= 0.0
            ? NormalizeDegrees(angle - _startAngleDegrees)
            : NormalizeDegrees(_startAngleDegrees - angle);
        if (delta > span + 1e-7)
        {
            parameter = 0.0;
            return false;
        }

        parameter = span <= 1e-12
            ? 0.0
            : Math.Clamp(delta / span, 0.0, 1.0);
        return true;
    }

    internal bool TryClosestParameter(
        OcctPoint3d point,
        out double parameter)
    {
        if (!TryAngleOf(point, out var angle))
        {
            parameter = 0.0;
            return false;
        }

        var span = Math.Abs(_sweepAngleDegrees);
        var delta = _sweepAngleDegrees >= 0.0
            ? NormalizeDegrees(angle - _startAngleDegrees)
            : NormalizeDegrees(_startAngleDegrees - angle);
        parameter = span <= 1e-12
            ? 0.0
            : Math.Clamp(delta / span, 0.0, 1.0);
        return true;
    }

    internal OcctPoint3d PointAtParameter(double parameter) =>
        PointAt(
            _startAngleDegrees +
            _sweepAngleDegrees * parameter);

    internal CadArcEntity CreateArc(
        OcctPoint3d start,
        OcctPoint3d middle,
        OcctPoint3d end) =>
        CopyPropertiesTo(
            new CadArcEntity(
                start,
                middle,
                end));

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadArcEntity(
            _center,
            _normal,
            _xAxis,
            _radius,
            _startAngleDegrees,
            _sweepAngleDegrees));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadArcEntity value)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));

        _center = value._center;
        _normal = value._normal;
        _xAxis = value._xAxis;
        _radius = value._radius;
        _startAngleDegrees = value._startAngleDegrees;
        _sweepAngleDegrees = value._sweepAngleDegrees;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        _center = Translated(_center, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        _center = CadTransformMath.RotatePoint(_center, center, axis, angleDegrees);
        _normal = CadTransformMath.RotateVector(_normal, axis, angleDegrees).Normalized();
        _xAxis = CadTransformMath.RotateVector(_xAxis, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _center = CadTransformMath.ScalePoint(_center, center, factor);
        _radius *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    private OcctPoint3d PointAt(double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var yAxis = _normal.Cross(_xAxis).Normalized();
        return _center
            + _xAxis * (Math.Cos(radians) * _radius)
            + yAxis * (Math.Sin(radians) * _radius);
    }

    private double DistanceInPlane(OcctPoint3d point)
    {
        var delta = CadTransformMath.Between(_center, point);
        var axial = CadTransformMath.Dot(delta, _normal);
        var planar = new OcctVector3d(
            delta.X - _normal.X * axial,
            delta.Y - _normal.Y * axial,
            delta.Z - _normal.Z * axial);
        return Math.Sqrt(planar.LengthSquared);
    }

    private bool TryAngleOf(OcctPoint3d point, out double angleDegrees)
    {
        var delta = CadTransformMath.Between(_center, point);
        var yAxis = _normal.Cross(_xAxis).Normalized();
        var x = CadTransformMath.Dot(delta, _xAxis);
        var y = CadTransformMath.Dot(delta, yAxis);
        if (x * x + y * y <= 1e-18)
        {
            angleDegrees = 0.0;
            return false;
        }

        angleDegrees = NormalizeDegrees(Math.Atan2(y, x) * 180.0 / Math.PI);
        return true;
    }

    private bool ContainsAngle(double angleDegrees)
    {
        if (_sweepAngleDegrees >= 0.0)
            return NormalizeDegrees(angleDegrees - _startAngleDegrees) <= _sweepAngleDegrees + 1e-9;
        return NormalizeDegrees(_startAngleDegrees - angleDegrees) <= -_sweepAngleDegrees + 1e-9;
    }

    private void SetFromThreePoints(
        OcctPoint3d start,
        OcctPoint3d middle,
        OcctPoint3d end,
        bool raiseEvents)
    {
        ValidatePoint(start, nameof(start));
        ValidatePoint(middle, nameof(middle));
        ValidatePoint(end, nameof(end));

        var first = CadTransformMath.Between(start, middle);
        var second = CadTransformMath.Between(start, end);
        var cross = first.Cross(second);
        if (!cross.TryNormalize(out var normal))
            throw new ArgumentException("Arc points must not be collinear.");

        var e1 = CadTransformMath.Normalize(first, nameof(middle));
        var e2 = normal.Cross(e1).Normalized();
        var middleX = Math.Sqrt(first.LengthSquared);
        var endX = CadTransformMath.Dot(second, e1);
        var endY = CadTransformMath.Dot(second, e2);
        if (Math.Abs(endY) <= 1e-12)
            throw new ArgumentException("Arc points must not be collinear.");

        var centerX = middleX * 0.5;
        var centerY =
            (endX * endX + endY * endY - 2.0 * endX * centerX) /
            (2.0 * endY);
        var center = start + e1 * centerX + e2 * centerY;
        var radius = center.DistanceTo(start);
        if (radius <= 1e-12)
            throw new ArgumentException("Arc radius is invalid.");

        var xAxis = CadTransformMath.Normalize(
            CadTransformMath.Between(center, start),
            nameof(start));
        var yAxis = normal.Cross(xAxis).Normalized();

        static double Angle(
            OcctPoint3d centerPoint,
            OcctPoint3d point,
            OcctVector3d xDirection,
            OcctVector3d yDirection)
        {
            var delta = CadTransformMath.Between(centerPoint, point);
            return NormalizeDegrees(Math.Atan2(
                CadTransformMath.Dot(delta, yDirection),
                CadTransformMath.Dot(delta, xDirection)) * 180.0 / Math.PI);
        }

        var middleAngle = Angle(center, middle, xAxis, yAxis);
        var endAngle = Angle(center, end, xAxis, yAxis);
        var ccwSweep = NormalizeDegrees(endAngle);
        var sweep = middleAngle <= ccwSweep + 1e-9
            ? ccwSweep
            : ccwSweep - 360.0;
        if (Math.Abs(sweep) <= 1e-9)
            throw new ArgumentException("Arc sweep is invalid.");

        if (raiseEvents) RaiseGeometryChanging(nameof(SetFromThreePoints));
        _center = center;
        _normal = normal;
        _xAxis = xAxis;
        _radius = radius;
        _startAngleDegrees = 0.0;
        _sweepAngleDegrees = sweep;
        if (raiseEvents) RaiseGeometryChanged(nameof(SetFromThreePoints));
    }

    private static double SweepBetween(double start, double end, bool clockwise)
    {
        if (clockwise)
            return -NormalizeDegrees(start - end);
        return NormalizeDegrees(end - start);
    }

    private static double NormalizeDegrees(double angle)
    {
        var value = angle % 360.0;
        return value < 0.0 ? value + 360.0 : value;
    }

    private static void ValidatePoint(OcctPoint3d value, string name)
    {
        if (!value.IsFinite) throw new ArgumentOutOfRangeException(name);
    }

    internal static JsonObject WriteGeometry(CadArcEntity entity) =>
        new()
        {
            ["start"] = CadEntityJson.Point(entity.Start),
            ["middle"] = CadEntityJson.Point(entity.Middle),
            ["end"] = CadEntityJson.Point(entity.End)
        };

    internal static CadArcEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "start"),
            CadEntityJson.ReadPoint(data, "middle"),
            CadEntityJson.ReadPoint(data, "end"));
}
