using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadCircleEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private double _radius;

    public CadCircleEntity(OcctPoint3d center, OcctVector3d normal, double radius) : base("Circle")
    {
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        if (!normal.TryNormalize(out _normal)) throw new ArgumentOutOfRangeException(nameof(normal));
        ValidatePositive(radius, nameof(radius));
        _center = center;
        _radius = radius;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d Normal => _normal;

    [Category("Orientation"), ReadOnly(true)] public double NormalX => _normal.X;
    [Category("Orientation"), ReadOnly(true)] public double NormalY => _normal.Y;
    [Category("Orientation"), ReadOnly(true)] public double NormalZ => _normal.Z;

    [Category("Geometry")] public double CenterX { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double CenterY { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double CenterZ { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }

    [Category("Geometry")]
    public double Radius
    {
        get => _radius;
        set { ValidatePositive(value, nameof(value)); SetGeometry(ref _radius, value); }
    }

    [Category("Geometry"), ReadOnly(true)] public double Diameter => _radius * 2.0;

    [Category("Measurement"), ReadOnly(true)]
    public double Circumference => 2.0 * Math.PI * _radius;

    [Category("Measurement"), ReadOnly(true)]
    public double Area => Math.PI * _radius * _radius;

    internal override OcctShape BuildShape(OcctEngine engine) => engine.MakeCircle(_center, _normal, _radius);

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        return CadPrecisionSnapGeometry.TryCreateCircle(
            this,
            0,
            Center,
            Normal,
            Radius,
            workPlane,
            out var curve)
            ? [curve]
            : Array.Empty<CadSnapCurve>();
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var (xAxis, yAxis) = PlaneAxes(_normal);
        var plane = new CadSnapWorkPlane(_center, xAxis, yAxis);
        return
        [
            new(this, _center, CadSnapType.Center, 0, plane),
            new(this, _center + xAxis * _radius, CadSnapType.Quadrant, 1, plane),
            new(this, _center + yAxis * _radius, CadSnapType.Quadrant, 2, plane),
            new(this, _center - xAxis * _radius, CadSnapType.Quadrant, 3, plane),
            new(this, _center - yAxis * _radius, CadSnapType.Quadrant, 4, plane)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var (xAxis, yAxis) = PlaneAxes(_normal);
        var positiveX = new CadGripWorkPlane(_center, xAxis, yAxis, true, 0.0);
        var negativeX = new CadGripWorkPlane(_center, xAxis, yAxis, true, 180.0);
        var positiveY = new CadGripWorkPlane(_center, xAxis, yAxis, true, 90.0);
        var negativeY = new CadGripWorkPlane(_center, xAxis, yAxis, true, -90.0);

        return
        [
            new(this, 0, _center, Kind: CadGripKind.Center),
            new(this, 1, _center + xAxis * _radius, positiveX, _center, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 2, _center - xAxis * _radius, negativeX, _center, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 3, _center + yAxis * _radius, positiveY, _center, CadPrecisionInputKind.Length, CadGripKind.Radius),
            new(this, 4, _center - yAxis * _radius, negativeY, _center, CadPrecisionInputKind.Length, CadGripKind.Radius)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        if (index == 0)
        {
            if (_center == targetPoint) return;
            _center = targetPoint;
        }
        else if (index is >= 1 and <= 4)
        {
            var radius = DistanceInPlane(targetPoint);
            if (radius <= 1e-12) return;
            _radius = radius;
        }
        else throw new ArgumentOutOfRangeException(nameof(index));

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    internal OcctPoint3d PointAtAngle(
        double angleRadians)
    {
        var (xAxis, yAxis) = PlaneAxes(_normal);
        return _center +
               xAxis * (Math.Cos(angleRadians) * _radius) +
               yAxis * (Math.Sin(angleRadians) * _radius);
    }

    internal CadArcEntity CreateArc(
        OcctPoint3d start,
        OcctPoint3d middle,
        OcctPoint3d end)
    {
        var arc = new CadArcEntity(start, middle, end);
        CopyPropertiesTo(arc);
        return arc;
    }

    public override CadEntity Duplicate() => CopyPropertiesTo(new CadCircleEntity(_center, _normal, _radius));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadCircleEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _center = value._center;
        _normal = value._normal;
        _radius = value._radius;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        if (displacement.X == 0.0 && displacement.Y == 0.0 && displacement.Z == 0.0) return;
        _center = Translated(_center, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        _center = CadTransformMath.RotatePoint(_center, center, axis, angleDegrees);
        _normal = CadTransformMath.RotateVector(_normal, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _center = CadTransformMath.ScalePoint(_center, center, factor);
        _radius *= factor;
        RaiseGeometryChanged(nameof(Scale));
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

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    internal static (OcctVector3d XAxis, OcctVector3d YAxis) PlaneAxes(OcctVector3d normal) =>
        CadTransformMath.PerpendicularAxes(normal);

    internal static JsonObject WriteGeometry(CadCircleEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["normal"] = CadEntityJson.Vector(entity.Normal),
            ["radius"] = entity.Radius
        };

    internal static CadCircleEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "normal"),
            CadEntityJson.ReadDouble(data, "radius"));
}
