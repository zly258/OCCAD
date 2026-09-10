using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadSplineEntity : CadEntity
{
    private const double PointTolerance = 1e-12;
    private const double PlaneTolerance = 1e-9;
    private readonly List<OcctPoint3d> _fitPoints;

    public CadSplineEntity(
        IEnumerable<OcctPoint3d> fitPoints,
        bool periodic = false,
        double tolerance = 1e-7) : base("Spline")
    {
        ArgumentNullException.ThrowIfNull(fitPoints);
        _fitPoints = fitPoints.ToList();
        if (_fitPoints.Count < (periodic ? 3 : 2))
            throw new ArgumentException("Spline has too few fit points.", nameof(fitPoints));
        if (_fitPoints.Any(static point => !point.IsFinite))
            throw new ArgumentException("Spline fit points must be finite.", nameof(fitPoints));
        ValidateFitPoints(_fitPoints, periodic, nameof(fitPoints));
        if (!double.IsFinite(tolerance) || tolerance <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));

        Periodic = periodic;
        Tolerance = tolerance;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)]
    public IReadOnlyList<OcctPoint3d> FitPoints => _fitPoints;

    [Category("Geometry"), ReadOnly(true)]
    public int FitPointCount => _fitPoints.Count;

    [Category("Geometry"), ReadOnly(true)]
    public bool Periodic { get; }

    [Category("Geometry"), ReadOnly(true)]
    public double Tolerance { get; }

    internal override OcctShape BuildShape(OcctEngine engine) =>
        engine.MakeInterpolatedBSpline(_fitPoints, Periodic, Tolerance);

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        CadSnapWorkPlane? plane = TryGetPlanarFrame(out var frame)
            ? new CadSnapWorkPlane(frame.Origin, frame.XAxis, frame.YAxis)
            : null;

        var result = new List<CadSnapPoint>(_fitPoints.Count + 2);
        for (var index = 0; index < _fitPoints.Count; index++)
        {
            var type =
                !Periodic && (index == 0 || index == _fitPoints.Count - 1)
                    ? CadSnapType.Endpoint
                    : CadSnapType.Node | CadSnapType.Vertex;
            result.Add(new CadSnapPoint(
                this,
                _fitPoints[index],
                type,
                index,
                plane));
        }

        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        CadGripWorkPlane? plane = TryGetPlanarFrame(out var frame)
            ? new CadGripWorkPlane(frame.Origin, frame.XAxis, frame.YAxis)
            : null;

        var result = new CadGripPoint[_fitPoints.Count];
        for (var index = 0; index < _fitPoints.Count; index++)
        {
            result[index] = new CadGripPoint(
                this,
                index,
                _fitPoints[index],
                plane,
                Kind: CadGripKind.Vertex);
        }

        return result;
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if ((uint)index >= (uint)_fitPoints.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var candidate = TryGetPlanarFrame(out var frame)
            ? ProjectToPlane(targetPoint, frame)
            : targetPoint;
        if (_fitPoints[index] == candidate)
            return;

        if (index > 0 && candidate.DistanceTo(_fitPoints[index - 1]) <= PointTolerance)
            return;
        if (index + 1 < _fitPoints.Count && candidate.DistanceTo(_fitPoints[index + 1]) <= PointTolerance)
            return;
        if (Periodic)
        {
            if (index == 0 && candidate.DistanceTo(_fitPoints[^1]) <= PointTolerance)
                return;
            if (index == _fitPoints.Count - 1 && candidate.DistanceTo(_fitPoints[0]) <= PointTolerance)
                return;
        }

        _fitPoints[index] = candidate;
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadSplineEntity(_fitPoints, Periodic, Tolerance));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadSplineEntity value ||
            value.Periodic != Periodic)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));

        _fitPoints.Clear();
        _fitPoints.AddRange(value._fitPoints);
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        if (displacement.X == 0.0 && displacement.Y == 0.0 && displacement.Z == 0.0)
            return;

        for (var index = 0; index < _fitPoints.Count; index++)
            _fitPoints[index] = Translated(_fitPoints[index], displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        for (var index = 0; index < _fitPoints.Count; index++)
            _fitPoints[index] = CadTransformMath.RotatePoint(_fitPoints[index], center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        for (var index = 0; index < _fitPoints.Count; index++)
            _fitPoints[index] = CadTransformMath.ScalePoint(_fitPoints[index], center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private bool TryGetPlanarFrame(out PlanarFrame frame)
    {
        frame = default;
        if (_fitPoints.Count < 3)
            return false;

        var origin = _fitPoints[0];
        OcctVector3d? xAxis = null;
        OcctVector3d? normal = null;

        for (var index = 1; index < _fitPoints.Count && normal is null; index++)
        {
            var first = CadTransformMath.Between(origin, _fitPoints[index]);
            if (!first.TryNormalize(out var x))
                continue;

            for (var other = index + 1; other < _fitPoints.Count; other++)
            {
                var second = CadTransformMath.Between(origin, _fitPoints[other]);
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

        foreach (var point in _fitPoints)
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

    private static void ValidateFitPoints(
        IReadOnlyList<OcctPoint3d> points,
        bool periodic,
        string parameterName)
    {
        for (var index = 1; index < points.Count; index++)
        {
            if (points[index - 1].DistanceTo(points[index]) <= PointTolerance)
                throw new ArgumentException("Adjacent spline fit points must be distinct.", parameterName);
        }

        if (periodic && points[^1].DistanceTo(points[0]) <= PointTolerance)
            throw new ArgumentException("Periodic spline closing fit points must be distinct.", parameterName);
    }

    private readonly record struct PlanarFrame(
        OcctPoint3d Origin,
        OcctVector3d XAxis,
        OcctVector3d YAxis,
        OcctVector3d Normal);

    internal static JsonObject WriteGeometry(CadSplineEntity entity) =>
        new()
        {
            ["fitPoints"] = CadEntityJson.Points(entity.FitPoints),
            ["periodic"] = entity.Periodic,
            ["tolerance"] = entity.Tolerance
        };

    internal static CadSplineEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoints(data, "fitPoints"),
            CadEntityJson.ReadBool(data, "periodic"),
            CadEntityJson.ReadDouble(data, "tolerance"));
}
