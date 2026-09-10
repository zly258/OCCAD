using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadSphereEntity : CadEntity
{
    private OcctPoint3d _center;
    private double _radius;

    public CadSphereEntity(OcctPoint3d center, double radius) : base("Sphere")
    {
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        ValidatePositive(radius, nameof(radius));
        _center = center;
        _radius = radius;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Category("Geometry")] public double X { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }
    [Category("Geometry")] public double Y { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }
    [Category("Geometry")] public double Z { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }
    [Category("Geometry")] public double Radius { get => _radius; set { ValidatePositive(value, nameof(value)); SetGeometry(ref _radius, value); } }

    [Category("Geometry"), ReadOnly(true)]
    public double Diameter => 2.0 * _radius;

    [Category("Measurement"), ReadOnly(true)]
    public double SurfaceArea => 4.0 * Math.PI * _radius * _radius;

    [Category("Measurement"), ReadOnly(true)]
    public double Volume => 4.0 * Math.PI * _radius * _radius * _radius / 3.0;

    internal override OcctShape BuildShape(OcctEngine engine) => engine.MakeSphere(_radius, _center.X, _center.Y, _center.Z);

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
    [
        new(this, _center, CadSnapType.Center, 0),
        new(this, _center + OcctVector3d.UnitX * _radius, CadSnapType.Quadrant, 1),
        new(this, _center + OcctVector3d.UnitY * _radius, CadSnapType.Quadrant, 2),
        new(this, _center - OcctVector3d.UnitX * _radius, CadSnapType.Quadrant, 3),
        new(this, _center - OcctVector3d.UnitY * _radius, CadSnapType.Quadrant, 4),
        new(this, _center + OcctVector3d.UnitZ * _radius, CadSnapType.Quadrant, 5),
        new(this, _center - OcctVector3d.UnitZ * _radius, CadSnapType.Quadrant, 6)
    ];

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var plane = new CadGripWorkPlane(_center, OcctVector3d.UnitX, OcctVector3d.UnitY);
        return
        [
            new(this, 0, _center),
            new(this, 1, _center + OcctVector3d.UnitX * _radius, plane, _center, CadPrecisionInputKind.Length),
            new(this, 2, _center + OcctVector3d.UnitY * _radius, plane, _center, CadPrecisionInputKind.Length),
            new(this, 3, _center - OcctVector3d.UnitX * _radius, plane, _center, CadPrecisionInputKind.Length),
            new(this, 4, _center - OcctVector3d.UnitY * _radius, plane, _center, CadPrecisionInputKind.Length)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        if (index == 0)
        {
            _center = targetPoint;
        }
        else if (index is >= 1 and <= 4)
        {
            var radius = _center.DistanceTo(targetPoint);
            if (radius <= 1e-9) return;
            _radius = radius;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() => CopyPropertiesTo(new CadSphereEntity(_center, _radius));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadSphereEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _center = value._center;
        _radius = value._radius;
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
        ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    internal static JsonObject WriteGeometry(CadSphereEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["radius"] = entity.Radius
        };

    internal static CadSphereEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadDouble(data, "radius"));
}
