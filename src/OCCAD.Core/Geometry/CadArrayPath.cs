using OcctNet;

namespace OCCAD;

/// <summary>Owns an exact OCCT curve/wire snapshot sampled by cumulative arc length.</summary>
public sealed class CadArrayPath : IDisposable
{
    private const double Tolerance = 1e-7;
    private readonly OcctModelingSession _session = new();
    private readonly List<Segment> _segments = [];
    private bool _disposed;
    public double Length { get; private set; }
    public bool Closed { get; private set; }

    public static bool Supports(CadEntity entity) => entity is CadLineEntity or CadPolylineEntity or
        CadPolygonEntity or CadRegularPolygonEntity or CadRectangleEntity or CadCircleEntity or
        CadArcEntity or CadEllipseEntity or CadSplineEntity or CadHelixEntity;

    public CadArrayPath(CadEntity entity)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entity);
            var shape = Build(entity);
            var edges = _session.GetShapeType(shape) == OcctShapeType.Edge
                ? new[] { shape } : _session.GetWireEdges(shape).ToArray();
            OcctPoint3d? previous = entity switch
            {
                CadLineEntity e => e.Start,
                CadPolylineEntity e => e.Points[0],
                CadPolygonEntity e => e.Points[0],
                CadRegularPolygonEntity e => e.VertexPoints[0],
                CadRectangleEntity e => e.CornerPoints[0],
                CadArcEntity e => e.Start,
                CadSplineEntity e => e.FitPoints[0],
                _ => null
            };
            OcctPoint3d? first = previous;
            foreach (var edge in edges)
            {
                var length = _session.GetEdgeLength(edge);
                if (!double.IsFinite(length) || length <= Tolerance)
                    throw new ArgumentException("Path contains a degenerate edge.", nameof(entity));
                var start = _session.EvaluateEdgeAtLength(edge, 0).Point;
                var end = _session.EvaluateEdgeAtLength(edge, length).Point;
                var reverse = previous is { } p && p.DistanceTo(start) > Tolerance;
                if (reverse && previous!.Value.DistanceTo(end) > Tolerance)
                    throw new ArgumentException("Path edges are not connected in order.", nameof(entity));
                first ??= start;
                _segments.Add(new Segment(edge, Length, length, reverse));
                Length += length;
                previous = reverse ? start : end;
            }
            if (_segments.Count == 0 || !double.IsFinite(Length))
                throw new ArgumentException("Path must have a finite positive length.", nameof(entity));
            Closed = first!.Value.DistanceTo(previous!.Value) <= Tolerance;
        }
        catch { _session.Dispose(); throw; }
    }

    public int StationCount(double spacing, int maximum = 10000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(spacing) || spacing <= Tolerance)
            throw new ArgumentOutOfRangeException(nameof(spacing), "Path spacing must exceed 1e-7.");
        if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        var count = Closed ? Math.Max(1, Math.Ceiling((Length - Tolerance) / spacing))
            : Math.Floor((Length + Tolerance) / spacing) + 1;
        if (!double.IsFinite(count) || count > maximum)
            throw new ArgumentException("Path array exceeds the maximum copy count.", nameof(spacing));
        return (int)count;
    }

    public OcctEdgeEvaluation Evaluate(double distance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(distance) || distance < 0 || distance > Length + Tolerance)
            throw new ArgumentOutOfRangeException(nameof(distance));
        distance = Math.Min(distance, Length);
        // At a corner, use the outgoing edge's tangent, except at the final endpoint.
        var segment = _segments[^1];
        foreach (var candidate in _segments)
            if (distance < candidate.Start + candidate.Length) { segment = candidate; break; }
        var local = Math.Clamp(distance - segment.Start, 0, segment.Length);
        var value = _session.EvaluateEdgeAtLength(segment.Edge, segment.Reverse ? segment.Length - local : local);
        if (!value.Tangent.TryNormalize(out var tangent))
            throw new InvalidOperationException("Path tangent is undefined at this station.");
        return new OcctEdgeEvaluation(value.Point, segment.Reverse ? tangent * -1 : tangent);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
        _segments.Clear();
    }

    private OcctModelShape Build(CadEntity entity) => entity switch
    {
        CadLineEntity e => _session.MakeLine(e.Start, e.End),
        CadPolylineEntity e => _session.MakePolyline(e.Points, e.Closed),
        CadPolygonEntity e => _session.MakePolyline(e.Points, true),
        CadRegularPolygonEntity e => _session.MakePolyline(e.VertexPoints, true),
        CadRectangleEntity e => _session.MakePolyline(e.CornerPoints, true),
        CadCircleEntity e => _session.MakeCircle(e.Center, e.Normal, e.Radius),
        CadArcEntity e => _session.MakeArc(e.Start, e.Middle, e.End),
        CadEllipseEntity e => BuildEllipse(e),
        CadSplineEntity e => _session.MakeInterpolatedBSpline(e.FitPoints, e.Periodic, e.Tolerance),
        CadHelixEntity e => _session.MakeHelix(e.Radius, e.Pitch, e.Turns, e.Origin, e.Axis, e.XAxis),
        _ => throw new ArgumentException("Select a curve or connected wire as the array path.", nameof(entity))
    };

    private OcctModelShape BuildEllipse(CadEllipseEntity ellipse)
    {
        var shape = _session.MakeEllipse(OcctPoint3d.Origin, OcctVector3d.UnitZ, ellipse.MajorRadius, ellipse.MinorRadius);
        var yAxis = ellipse.Normal.Cross(ellipse.XAxis).Normalized();
        if (CadTransformMath.TryGetAxisAngle(ellipse.XAxis, yAxis, ellipse.Normal, out var axis, out var angle))
            shape = _session.Rotate(shape, OcctPoint3d.Origin, axis, angle);
        return _session.Translate(shape, ellipse.Center - OcctPoint3d.Origin);
    }

    private readonly record struct Segment(OcctModelShape Edge, double Start, double Length, bool Reverse);
}
