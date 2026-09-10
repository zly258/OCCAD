using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadPolylineEntity : CadEntity
{
    private const double PointTolerance = 1e-12;
    private readonly List<OcctPoint3d> _points;

    public CadPolylineEntity(IEnumerable<OcctPoint3d> points, bool closed = false) : base("Polyline")
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.ToList();
        if (_points.Count < (closed ? 3 : 2)) throw new ArgumentException("Polyline has too few points.", nameof(points));
        if (_points.Any(static point => !point.IsFinite)) throw new ArgumentException("Polyline points must be finite.", nameof(points));
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

    internal override OcctShape BuildShape(OcctEngine engine) => engine.MakePolyline(_points, Closed);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        var result = new List<CadSnapCurve>();

        for (var index = 0; index + 1 < Points.Count; index++)
        {
            if (CadPrecisionSnapGeometry.TryCreateSegment(
                    this, index, Points[index], Points[index + 1], workPlane, out var curve))
                result.Add(curve);
        }

        if (Closed &&
            CadPrecisionSnapGeometry.TryCreateSegment(
                this, Points.Count - 1, Points[^1], Points[0], workPlane, out var closingCurve))
            result.Add(closingCurve);

        return result;
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var result = new List<CadSnapPoint>(_points.Count * 2);
        for (var index = 0; index < _points.Count; index++)
        {
            result.Add(new CadSnapPoint(this, _points[index], CadSnapType.Endpoint, index));
            var next = index + 1;
            if (next < _points.Count)
                result.Add(new CadSnapPoint(this, Midpoint(_points[index], _points[next]), CadSnapType.Midpoint, index));
            else if (Closed)
                result.Add(new CadSnapPoint(this, Midpoint(_points[index], _points[0]), CadSnapType.Midpoint, index));
        }
        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var result = new CadGripPoint[_points.Count];
        for (var index = 0; index < _points.Count; index++)
            result[index] = new CadGripPoint(this, index, _points[index], Kind: CadGripKind.Vertex);
        return result;
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if ((uint)index >= (uint)_points.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        if (_points[index] == targetPoint) return;

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
        if ((previous is { } before && before.DistanceTo(targetPoint) <= PointTolerance) ||
            (next is { } after && after.DistanceTo(targetPoint) <= PointTolerance))
            return;

        _points[index] = targetPoint;
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    internal CadPolylineEntity CopyWithPoints(
        IEnumerable<OcctPoint3d> points) =>
        CopyWithPoints(points, Closed);

    internal CadPolylineEntity CopyWithPoints(
        IEnumerable<OcctPoint3d> points,
        bool closed) =>
        CopyPropertiesTo(
            new CadPolylineEntity(points, closed));

    internal CadPathEntity CreatePath(
        IEnumerable<CadEntity> segments) =>
        CopyPropertiesTo(
            new CadPathEntity(segments));

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
        if (displacement.X == 0.0 && displacement.Y == 0.0 && displacement.Z == 0.0) return;
        for (var index = 0; index < _points.Count; index++) _points[index] = Translated(_points[index], displacement);
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
        for (var index = 0; index < _points.Count; index++)
            _points[index] = CadTransformMath.ScalePoint(_points[index], center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private static void ValidateSegments(IReadOnlyList<OcctPoint3d> points, bool closed, string parameterName)
    {
        for (var index = 1; index < points.Count; index++)
        {
            if (points[index - 1].DistanceTo(points[index]) <= PointTolerance)
                throw new ArgumentException("Polyline contains a zero-length segment.", parameterName);
        }

        if (closed && points[^1].DistanceTo(points[0]) <= PointTolerance)
            throw new ArgumentException("Closed polyline contains a zero-length closing segment.", parameterName);
    }

    private static OcctPoint3d Midpoint(OcctPoint3d a, OcctPoint3d b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5, (a.Z + b.Z) * 0.5);

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
