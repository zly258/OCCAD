using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadSplineEntity : CadEntity
{
    private const double PointTolerance = 1e-12;
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
        var result = new List<CadSnapPoint>(_fitPoints.Count + 2);
        for (var index = 0; index < _fitPoints.Count; index++)
        {
            var type =
                !Periodic && (index == 0 || index == _fitPoints.Count - 1)
                    ? CadSnapType.Endpoint
                    : CadSnapType.Vertex;
            result.Add(new CadSnapPoint(this, _fitPoints[index], type, index));
        }

        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var result = new CadGripPoint[_fitPoints.Count];
        for (var index = 0; index < _fitPoints.Count; index++)
            result[index] = new CadGripPoint(
                this,
                index,
                _fitPoints[index],
                Kind: CadGripKind.Vertex);
        return result;
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if ((uint)index >= (uint)_fitPoints.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));
        if (_fitPoints[index] == targetPoint)
            return;

        if (index > 0 && targetPoint.DistanceTo(_fitPoints[index - 1]) <= PointTolerance)
            return;
        if (index + 1 < _fitPoints.Count && targetPoint.DistanceTo(_fitPoints[index + 1]) <= PointTolerance)
            return;
        if (Periodic)
        {
            if (index == 0 && targetPoint.DistanceTo(_fitPoints[^1]) <= PointTolerance)
                return;
            if (index == _fitPoints.Count - 1 && targetPoint.DistanceTo(_fitPoints[0]) <= PointTolerance)
                return;
        }

        _fitPoints[index] = targetPoint;
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
