using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadPolygonEntity : CadEntity
{
    private const double PointTolerance = 1e-12;
    private const double PlaneTolerance = 1e-9;
    private readonly List<OcctPoint3d> _points;

    public CadPolygonEntity(IEnumerable<OcctPoint3d> points) : base("Polygon")
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.ToList();
        if (_points.Count < 3)
            throw new ArgumentException("Polygon requires at least three points.", nameof(points));
        if (_points.Any(static point => !point.IsFinite))
            throw new ArgumentException("Polygon points must be finite.", nameof(points));
        ValidateTopology(_points, nameof(points));

        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)]
    public IReadOnlyList<OcctPoint3d> Points => _points;

    [Category("Geometry"), ReadOnly(true)]
    public int PointCount => _points.Count;

    [Category("Measurement"), ReadOnly(true)]
    public double Perimeter
    {
        get
        {
            var length = 0.0;
            for (var index = 0; index < _points.Count; index++)
                length += _points[index].DistanceTo(_points[(index + 1) % _points.Count]);
            return length;
        }
    }

    [Category("Measurement"), ReadOnly(true)]
    public double Area
    {
        get
        {
            var nx = 0.0;
            var ny = 0.0;
            var nz = 0.0;
            for (var index = 0; index < _points.Count; index++)
            {
                var current = _points[index];
                var next = _points[(index + 1) % _points.Count];
                nx += (current.Y - next.Y) * (current.Z + next.Z);
                ny += (current.Z - next.Z) * (current.X + next.X);
                nz += (current.X - next.X) * (current.Y + next.Y);
            }

            return 0.5 * Math.Sqrt(nx * nx + ny * ny + nz * nz);
        }
    }

    internal override OcctShape BuildShape(OcctEngine engine) =>
        engine.MakePolyline(_points, true);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        var result = new List<CadSnapCurve>(Points.Count);

        for (var index = 0; index < Points.Count; index++)
        {
            if (CadPrecisionSnapGeometry.TryCreateSegment(
                    this,
                    index,
                    Points[index],
                    Points[(index + 1) % Points.Count],
                    workPlane,
                    out var curve))
                result.Add(curve);
        }

        return result;
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var plane = CreateSnapPlane();
        var result = new List<CadSnapPoint>(_points.Count * 2 + 1)
        {
            new(this, Center(), CadSnapType.Center, 0, plane)
        };

        for (var index = 0; index < _points.Count; index++)
        {
            var next = (index + 1) % _points.Count;
            result.Add(new CadSnapPoint(this, _points[index], CadSnapType.Endpoint, index, plane));
            result.Add(new CadSnapPoint(this, Midpoint(_points[index], _points[next]), CadSnapType.Midpoint, index, plane));
        }

        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var plane = CreateGripPlane();
        var result = new CadGripPoint[_points.Count + 1];
        result[0] = new CadGripPoint(this, 0, Center(), plane, Kind: CadGripKind.Center);
        for (var index = 0; index < _points.Count; index++)
            result[index + 1] = new CadGripPoint(this, index + 1, _points[index], plane, Kind: CadGripKind.Vertex);
        return result;
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        if (index == 0)
        {
            Translate(CadTransformMath.Between(Center(), targetPoint));
            return;
        }

        var pointIndex = index - 1;
        if ((uint)pointIndex >= (uint)_points.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (_points[pointIndex] == targetPoint)
            return;

        var projected = ProjectToPlane(targetPoint);
        var previous = _points[(pointIndex - 1 + _points.Count) % _points.Count];
        var next = _points[(pointIndex + 1) % _points.Count];
        if (projected.DistanceTo(previous) <= PointTolerance ||
            projected.DistanceTo(next) <= PointTolerance)
            return;

        _points[pointIndex] = projected;
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadPolygonEntity(_points));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadPolygonEntity value)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));

        _points.Clear();
        _points.AddRange(value._points);
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        if (displacement.X == 0.0 && displacement.Y == 0.0 && displacement.Z == 0.0)
            return;

        for (var index = 0; index < _points.Count; index++)
            _points[index] = Translated(_points[index], displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        for (var index = 0; index < _points.Count; index++)
            _points[index] = CadTransformMath.RotatePoint(_points[index], center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        for (var index = 0; index < _points.Count; index++)
            _points[index] = CadTransformMath.ScalePoint(_points[index], center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private OcctPoint3d Center()
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;
        foreach (var point in _points)
        {
            x += point.X;
            y += point.Y;
            z += point.Z;
        }

        var scale = 1.0 / _points.Count;
        return new OcctPoint3d(x * scale, y * scale, z * scale);
    }

    private CadSnapWorkPlane CreateSnapPlane()
    {
        var (origin, xAxis, yAxis, _) = PlaneFrame(_points);
        return new CadSnapWorkPlane(origin, xAxis, yAxis);
    }

    private CadGripWorkPlane CreateGripPlane()
    {
        var (origin, xAxis, yAxis, _) = PlaneFrame(_points);
        return new CadGripWorkPlane(origin, xAxis, yAxis);
    }

    private OcctPoint3d ProjectToPlane(OcctPoint3d point)
    {
        var (origin, _, _, normal) = PlaneFrame(_points);
        var delta = CadTransformMath.Between(origin, point);
        var distance = CadTransformMath.Dot(delta, normal);
        return new OcctPoint3d(
            point.X - normal.X * distance,
            point.Y - normal.Y * distance,
            point.Z - normal.Z * distance);
    }

    private static void ValidateTopology(IReadOnlyList<OcctPoint3d> points, string parameterName)
    {
        for (var index = 0; index < points.Count; index++)
        {
            if (points[index].DistanceTo(points[(index + 1) % points.Count]) <= PointTolerance)
                throw new ArgumentException("Polygon edges must have non-zero length.", parameterName);
        }

        var (origin, _, _, normal) = PlaneFrame(points);
        foreach (var point in points)
        {
            var distance = Math.Abs(CadTransformMath.Dot(CadTransformMath.Between(origin, point), normal));
            if (distance > PlaneTolerance)
                throw new ArgumentException("Polygon points must be coplanar.", parameterName);
        }
    }

    private static (OcctPoint3d Origin, OcctVector3d XAxis, OcctVector3d YAxis, OcctVector3d Normal)
        PlaneFrame(IReadOnlyList<OcctPoint3d> points)
    {
        var origin = points[0];
        OcctVector3d? xAxis = null;
        OcctVector3d? normal = null;

        for (var index = 1; index < points.Count && normal is null; index++)
        {
            var first = CadTransformMath.Between(origin, points[index]);
            if (!first.TryNormalize(out var x))
                continue;
            xAxis ??= x;

            for (var other = index + 1; other < points.Count; other++)
            {
                var second = CadTransformMath.Between(origin, points[other]);
                var cross = first.Cross(second);
                if (cross.TryNormalize(out var n))
                {
                    xAxis = x;
                    normal = n;
                    break;
                }
            }
        }

        if (xAxis is null || normal is null)
            throw new ArgumentException("Polygon points must not be collinear.", nameof(points));

        var yAxis = normal.Value.Cross(xAxis.Value).Normalized();
        return (origin, xAxis.Value, yAxis, normal.Value);
    }

    private static OcctPoint3d Midpoint(OcctPoint3d a, OcctPoint3d b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5, (a.Z + b.Z) * 0.5);

    internal static JsonObject WriteGeometry(CadPolygonEntity entity) =>
        new()
        {
            ["points"] = CadEntityJson.Points(entity.Points)
        };

    internal static CadPolygonEntity ReadGeometry(JsonObject data) =>
        new(CadEntityJson.ReadPoints(data, "points"));
}
