using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadLineEntity : CadEntity
{
    private OcctPoint3d _start;
    private OcctPoint3d _end;

    public CadLineEntity(OcctPoint3d start, OcctPoint3d end) : base("Line")
    {
        ValidatePoint(start, nameof(start));
        ValidatePoint(end, nameof(end));
        if (start.DistanceTo(end) <= 1e-12) throw new ArgumentException("Line endpoints must be distinct.", nameof(end));
        _start = start;
        _end = end;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Start => _start;
    [Browsable(false)] public OcctPoint3d End => _end;

    [Category("Geometry")] public double StartX { get => _start.X; set => SetStart(value, _start.Y, _start.Z); }
    [Category("Geometry")] public double StartY { get => _start.Y; set => SetStart(_start.X, value, _start.Z); }
    [Category("Geometry")] public double StartZ { get => _start.Z; set => SetStart(_start.X, _start.Y, value); }
    [Category("Geometry")] public double EndX { get => _end.X; set => SetEnd(value, _end.Y, _end.Z); }
    [Category("Geometry")] public double EndY { get => _end.Y; set => SetEnd(_end.X, value, _end.Z); }
    [Category("Geometry")] public double EndZ { get => _end.Z; set => SetEnd(_end.X, _end.Y, value); }
    [Category("Measurement"), ReadOnly(true)] public double Length => _start.DistanceTo(_end);

    internal override OcctShape BuildShape(OcctEngine engine) => engine.MakeLine(_start, _end);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        return CadPrecisionSnapGeometry.TryCreateSegment(
            this,
            0,
            Start,
            End,
            workPlane,
            out var curve)
            ? [curve]
            : Array.Empty<CadSnapCurve>();
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
    [
        new(this, _start, CadSnapType.Endpoint, 0),
        new(this, Midpoint(_start, _end), CadSnapType.Midpoint, 0),
        new(this, _end, CadSnapType.Endpoint, 1)
    ];

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var direction = CadTransformMath.Normalize(
            CadTransformMath.Between(_start, _end),
            nameof(End));
        var reference = Math.Abs(CadTransformMath.Dot(direction, OcctVector3d.UnitZ)) > 0.999
            ? OcctVector3d.UnitY
            : OcctVector3d.UnitZ;
        var yAxis = reference.Cross(direction).Normalized();
        var reverse = new OcctVector3d(
            -direction.X,
            -direction.Y,
            -direction.Z);
        var reverseYAxis =
            reference.Cross(reverse).Normalized();
        return
        [
            new(
                this,
                0,
                _start,
                new CadGripWorkPlane(
                    _end,
                    reverse,
                    reverseYAxis),
                _end,
                Kind: CadGripKind.Vertex),
            new(
                this,
                1,
                Midpoint(_start, _end),
                Kind: CadGripKind.Center),
            new(
                this,
                2,
                _end,
                new CadGripWorkPlane(
                    _start,
                    direction,
                    yAxis),
                _start,
                Kind: CadGripKind.Vertex)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        ValidatePoint(targetPoint, nameof(targetPoint));
        switch (index)
        {
            case 0:
                if (targetPoint.DistanceTo(_end) <= 1e-12) return;
                _start = targetPoint;
                break;
            case 1:
                {
                    var midpoint = Midpoint(_start, _end);
                    var displacement = CadTransformMath.Between(midpoint, targetPoint);
                    _start = Translated(_start, displacement);
                    _end = Translated(_end, displacement);
                    break;
                }
            case 2:
                if (targetPoint.DistanceTo(_start) <= 1e-12) return;
                _end = targetPoint;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    internal CadLineEntity CreateLine(
        OcctPoint3d start,
        OcctPoint3d end) =>
        CopyPropertiesTo(
            new CadLineEntity(start, end));

    internal CadArcEntity CreateArc(
        OcctPoint3d start,
        OcctPoint3d middle,
        OcctPoint3d end) =>
        CopyPropertiesTo(
            new CadArcEntity(start, middle, end));

    internal CadPolylineEntity CreatePolyline(
        IEnumerable<OcctPoint3d> points) =>
        CopyPropertiesTo(
            new CadPolylineEntity(points, closed: false));

    internal CadPathEntity CreatePath(
        IEnumerable<CadEntity> segments) =>
        CopyPropertiesTo(
            new CadPathEntity(segments));

    public override CadEntity Duplicate() => CopyPropertiesTo(new CadLineEntity(_start, _end));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadLineEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _start = value._start;
        _end = value._end;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        if (displacement.X == 0.0 && displacement.Y == 0.0 && displacement.Z == 0.0) return;
        _start = Translated(_start, displacement);
        _end = Translated(_end, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        _start = CadTransformMath.RotatePoint(_start, center, axis, angleDegrees);
        _end = CadTransformMath.RotatePoint(_end, center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        _start = CadTransformMath.ScalePoint(_start, center, factor);
        _end = CadTransformMath.ScalePoint(_end, center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private void SetStart(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z));
        var value = new OcctPoint3d(x, y, z);
        if (value.DistanceTo(_end) <= 1e-12) throw new ArgumentException("Line endpoints must be distinct.");
        SetGeometry(ref _start, value);
    }

    private void SetEnd(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z));
        var value = new OcctPoint3d(x, y, z);
        if (value.DistanceTo(_start) <= 1e-12) throw new ArgumentException("Line endpoints must be distinct.");
        SetGeometry(ref _end, value);
    }

    private static void ValidatePoint(OcctPoint3d point, string name)
    {
        if (!point.IsFinite) throw new ArgumentOutOfRangeException(name, "Point must be finite.");
    }

    private static OcctPoint3d Midpoint(OcctPoint3d a, OcctPoint3d b) =>
        new((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5, (a.Z + b.Z) * 0.5);

    internal static JsonObject WriteGeometry(CadLineEntity entity) =>
        new()
        {
            ["start"] = CadEntityJson.Point(entity.Start),
            ["end"] = CadEntityJson.Point(entity.End)
        };

    internal static CadLineEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "start"),
            CadEntityJson.ReadPoint(data, "end"));
}
