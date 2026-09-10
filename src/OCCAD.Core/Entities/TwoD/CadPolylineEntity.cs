using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadPolylineEntity : CadEntity
{
    private const double PointTolerance = 1e-12;
    private const double PlaneTolerance = 1e-9;
    private readonly List<OcctPoint3d> _points;

    public CadPolylineEntity(IEnumerable<OcctPoint3d> points, bool closed = false) : base("Polyline")
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.ToList();
        if (_points.Count < (closed ? 3 : 2))
            throw new ArgumentException("Polyline has too few points.", nameof(points));
        if (_points.Any(static point => !point.IsFinite))
            throw new ArgumentException("Polyline points must be finite.", nameof(points));
        ValidateSegments(_points, closed, nameof(points));
        Closed = closed;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public IReadOnlyList<OcctPoint3d> Points => _points;
    [Category("Geometry"), ReadOnly(true)] public int PointCount => _points.Count;
    [Category("Geometry"), ReadOnly(true)] public bool Closed { get; }

    [Category("Measurement"), ReadOnly(true)]
    public double Length
    {
        get
        {
            var result = 0.0;
            for (var index = 1; index < _points.Count; index++)
                result += _points[index - 1].DistanceTo(_points[index]);

            if (Closed && _points.Count > 2)
                result += _points[^1].DistanceTo(_points[0]);

            return result;
        }
    }

    internal override OcctShape BuildShape(OcctEngine engine) =>
        engine.MakePolyline(_points, Closed);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        var result = new List<CadSnapCurve>();

        for (var index = 0; index + 1 < Points.Count; index++)
        {
            if (CadPrecisionSnapGeometry.TryCreateSegment(
                    this,
                    index,
                    Points[index],
                    Points[index + 1],
                    workPlane,
                    out var curve))
                result.Add(curve);
        }

        if (Closed &&
            CadPrecisionSnapGeometry.TryCreateSegment(
                this,
                Points.Count - 1,
                Points[^1],
                Points[0],
                workPlane,
                out var closingCurve))
            result.Add(closingCurve);

        return result;
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        CadSnapWorkPlane? plane = TryGetPlanarFrame(out var frame)
            ? new CadSnapWorkPlane(frame.Origin, frame.XAxis, frame.YAxis)
            : null;

        var result = new List<CadSnapPoint>(_points.Count * 2);
        for (var index = 0; index < _points.Count; index++)
        {
            result.Add(new CadSnapPoint(
                this,
                _points[index],
                CadSnapType.Endpoint,
                index,
                plane));

            var next = index + 1;
            if (next < _points.Count)
            {
                result.Add(new CadSnapPoint(
                    this,
                    Midpoint(_points[index], _points[next]),
                    CadSnapType.Midpoint,
                    index,
                    plane));
            }
            else if (Closed)
            {
                result.Add(new CadSnapPoint(
                    this,
                    Midpoint(_points[index], _points[0]),
                    CadSnapType.Midpoint,
                    index,
                    plane));
            }
        }

        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        CadGripWorkPlane? plane = TryGetPlanarFrame(out var frame)
            ? new CadGripWorkPlane(frame.Origin, frame.XAxis, frame.YAxis)
            : null;

        var segmentCount = Closed ? _points.Count : Math.Max(0, _points.Count - 1);
        var result = new List<CadGripPoint>(_points.Count + segmentCount);

        for (var index = 0; index < _points.Count; index++)
        {
            result.Add(new CadGripPoint(
                this,
                index,
                _points[index],
                plane,
                Kind: CadGripKind.Vertex));
        }

        for (var segIndex = 0; segIndex < segmentCount; segIndex++)
        {
            var next = (segIndex + 1) % _points.Count;
            var mid = Midpoint(_points[segIndex], _points[next]);
            result.Add(new CadGripPoint(
                this,
                _points.Count + segIndex,
                mid,
                plane,
                Kind: CadGripKind.Midpoint));
        }

        return result;
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        var segmentCount = Closed ? _points.Count : Math.Max(0, _points.Count - 1);
        if (index < 0 || index >= _points.Count + segmentCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var candidate = TryGetPlanarFrame(out var frame)
            ? ProjectToPlane(targetPoint, frame)
            : targetPoint;

        if (index < _points.Count)
        {
            if (_points[index] == candidate)
                return;

            var previous = index > 0
                ? _points[index - 1]
                : Closed
                    ? _points[^1]
                    : (OcctPoint3d?)null;
            var next = index + 1 < _points.Count
                ? _points[index + 1]
                : Closed
                    ? _points[0]
                    : (OcctPoint3d?)null;
            if ((previous is { } before &&
                 before.DistanceTo(candidate) <= PointTolerance) ||
                (next is { } after &&
                 after.DistanceTo(candidate) <= PointTolerance))
                return;

            _points[index] = candidate;
        }
        else
        {
            var segIndex = index - _points.Count;
            var nextIndex = (segIndex + 1) % _points.Count;
            var currentMid = Midpoint(_points[segIndex], _points[nextIndex]);
            var displacement = CadTransformMath.Between(currentMid, candidate);
            if (displacement.LengthSquared <= 1e-18)
                return;

            _points[segIndex] = Translated(_points[segIndex], displacement);
            _points[nextIndex] = Translated(_points[nextIndex], displacement);
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    internal CadPolylineEntity CopyWithPoints(
        IEnumerable<OcctPoint3d> points) =>
        CopyWithPoints(points, Closed);

    internal CadPolylineEntity CopyWithPoints(
        IEnumerable<OcctPoint3d> points,
        bool closed) =>
        CopyPropertiesTo(new CadPolylineEntity(points, closed));

    internal CadPathEntity CreatePath(
        IEnumerable<CadEntity> segments) =>
        CopyPropertiesTo(new CadPathEntity(segments));

    public override CadEntity Duplicate() =>
        CopyWithPoints(_points);

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadPolylineEntity value || value.Closed != Closed)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _points.Clear();
        _points.AddRange(value._points);
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        if (displacement.X == 0.0 &&
            displacement.Y == 0.0 &&
            displacement.Z == 0.0)
            return;

        for (var index = 0; index < _points.Count; index++)
            _points[index] = Translated(_points[index], displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        for (var index = 0; index < _points.Count; index++)
            _points[index] = CadTransformMath.RotatePoint(
                _points[index], center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        for (var index = 0; index < _points.Count; index++)
            _points[index] = CadTransformMath.ScalePoint(
                _points[index], center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private bool TryGetPlanarFrame(out PlanarFrame frame)
    {
        frame = default;
        if (_points.Count < 3)
            return false;

        var origin = _points[0];
        OcctVector3d? xAxis = null;
        OcctVector3d? normal = null;

        for (var index = 1; index < _points.Count && normal is null; index++)
        {
            var first = CadTransformMath.Between(origin, _points[index]);
            if (!first.TryNormalize(out var x))
                continue;

            for (var other = index + 1; other < _points.Count; other++)
            {
                var second = CadTransformMath.Between(origin, _points[other]);
                var cross = first.Cross(second);
                if (!cross.TryNormalize(out var n))
                    continue;

                xAxis = x;
                normal = n;
                break;
            }
        }

        if (xAxis is null || normal is null)
            return false;

        foreach (var point in _points)
        {
            var distance = Math.Abs(CadTransformMath.Dot(
                CadTransformMath.Between(origin, point),
                normal.Value));
            if (distance > PlaneTolerance)
                return false;
        }

        var yAxis = normal.Value.Cross(xAxis.Value).Normalized();
        frame = new PlanarFrame(
            origin,
            xAxis.Value,
            yAxis,
            normal.Value);
        return true;
    }

    private static OcctPoint3d ProjectToPlane(
        OcctPoint3d point,
        PlanarFrame frame)
    {
        var delta = CadTransformMath.Between(frame.Origin, point);
        var distance = CadTransformMath.Dot(delta, frame.Normal);
        return new OcctPoint3d(
            point.X - frame.Normal.X * distance,
            point.Y - frame.Normal.Y * distance,
            point.Z - frame.Normal.Z * distance);
    }

    private static void ValidateSegments(
        IReadOnlyList<OcctPoint3d> points,
        bool closed,
        string parameterName)
    {
        for (var index = 1; index < points.Count; index++)
        {
            if (points[index - 1].DistanceTo(points[index]) <= PointTolerance)
                throw new ArgumentException(
                    "Polyline contains a zero-length segment.",
                    parameterName);
        }

        if (closed && points[^1].DistanceTo(points[0]) <= PointTolerance)
            throw new ArgumentException(
                "Closed polyline contains a zero-length closing segment.",
                parameterName);
    }

    private static OcctPoint3d Midpoint(OcctPoint3d a, OcctPoint3d b) =>
        new(
            (a.X + b.X) * 0.5,
            (a.Y + b.Y) * 0.5,
            (a.Z + b.Z) * 0.5);

    private readonly record struct PlanarFrame(
        OcctPoint3d Origin,
        OcctVector3d XAxis,
        OcctVector3d YAxis,
        OcctVector3d Normal);

    internal static JsonObject WriteGeometry(CadPolylineEntity entity) =>
        new()
        {
            ["points"] = CadEntityJson.Points(entity.Points),
            ["closed"] = entity.Closed
        };

    internal static CadPolylineEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoints(data, "points"),
            CadEntityJson.ReadBool(data, "closed"));
}
