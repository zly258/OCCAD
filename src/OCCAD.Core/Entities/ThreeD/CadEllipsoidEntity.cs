using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadEllipsoidEntity : CadEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _zAxis;
    private double _xRadius;
    private double _yRadius;
    private double _zRadius;

    public CadEllipsoidEntity(
        OcctPoint3d center,
        double xRadius,
        double yRadius,
        double zRadius)
        : this(
            center,
            OcctVector3d.UnitX,
            OcctVector3d.UnitY,
            OcctVector3d.UnitZ,
            xRadius,
            yRadius,
            zRadius)
    {
    }

    internal CadEllipsoidEntity(
        OcctPoint3d center,
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        OcctVector3d zAxis,
        double xRadius,
        double yRadius,
        double zRadius) : base("Ellipsoid")
    {
        if (!center.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(center));

        ValidateFrame(xAxis, yAxis, zAxis);
        ValidateRadii(xRadius, yRadius, zRadius);

        _center = center;
        _xAxis = xAxis.Normalized();
        _yAxis = yAxis.Normalized();
        _zAxis = zAxis.Normalized();
        _xRadius = xRadius;
        _yRadius = yRadius;
        _zRadius = zRadius;
    }

    [Browsable(false)] public OcctPoint3d Center => _center;
    [Browsable(false)] public OcctVector3d XAxis => _xAxis;
    [Browsable(false)] public OcctVector3d YAxis => _yAxis;
    [Browsable(false)] public OcctVector3d ZAxis => _zAxis;

    [Category("Geometry")]
    public double X
    {
        get => _center.X;
        set => SetCenter(value, _center.Y, _center.Z);
    }

    [Category("Geometry")]
    public double Y
    {
        get => _center.Y;
        set => SetCenter(_center.X, value, _center.Z);
    }

    [Category("Geometry")]
    public double Z
    {
        get => _center.Z;
        set => SetCenter(_center.X, _center.Y, value);
    }

    [Category("Geometry")]
    public double XRadius
    {
        get => _xRadius;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _xRadius, value);
        }
    }

    [Category("Geometry")]
    public double YRadius
    {
        get => _yRadius;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _yRadius, value);
        }
    }

    [Category("Geometry")]
    public double ZRadius
    {
        get => _zRadius;
        set
        {
            ValidatePositive(value, nameof(value));
            SetGeometry(ref _zRadius, value);
        }
    }

    [Category("Measurement"), ReadOnly(true)]
    public double Volume => 4.0 * Math.PI * _xRadius * _yRadius * _zRadius / 3.0;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        using var model = new OcctModelingSession();
        var shape = model.MakeEllipsoid(
            OcctPoint3d.Origin,
            _xRadius,
            _yRadius,
            _zRadius);

        if (CadTransformMath.TryGetAxisAngle(
                _xAxis,
                _yAxis,
                _zAxis,
                out var axis,
                out var angle))
        {
            shape = model.Rotate(
                shape,
                OcctPoint3d.Origin,
                axis,
                angle);
        }

        shape = model.Translate(
            shape,
            _center - OcctPoint3d.Origin);
        return engine.CreateShapeFromModel(model, shape);
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
        [new(this, _center, CadSnapType.Center, 0)];

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var negativeX = new OcctVector3d(-_xAxis.X, -_xAxis.Y, -_xAxis.Z);
        var negativeY = new OcctVector3d(-_yAxis.X, -_yAxis.Y, -_yAxis.Z);
        var negativeZ = new OcctVector3d(-_zAxis.X, -_zAxis.Y, -_zAxis.Z);

        return
        [
            new(this, 0, _center, Kind: CadGripKind.Center),
            new(
                this,
                1,
                _center + _xAxis * _xRadius,
                new CadGripWorkPlane(_center, _xAxis, _yAxis, true, 0.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                2,
                _center - _xAxis * _xRadius,
                new CadGripWorkPlane(_center, negativeX, _yAxis, true, 0.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                3,
                _center + _yAxis * _yRadius,
                new CadGripWorkPlane(_center, _yAxis, _zAxis, true, 0.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                4,
                _center - _yAxis * _yRadius,
                new CadGripWorkPlane(_center, negativeY, _zAxis, true, 0.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                5,
                _center + _zAxis * _zRadius,
                new CadGripWorkPlane(_center, _zAxis, _xAxis, true, 0.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius),
            new(
                this,
                6,
                _center - _zAxis * _zRadius,
                new CadGripWorkPlane(_center, negativeZ, _xAxis, true, 0.0),
                _center,
                CadPrecisionInputKind.Length,
                CadGripKind.Radius)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        if (index == 0)
        {
            _center = targetPoint;
        }
        else if (index is >= 1 and <= 6)
        {
            var delta = CadTransformMath.Between(_center, targetPoint);
            var axis = index <= 2
                ? _xAxis
                : index <= 4
                    ? _yAxis
                    : _zAxis;
            var radius = Math.Abs(CadTransformMath.Dot(delta, axis));
            if (radius <= 1e-9)
                return;

            if (index <= 2)
                _xRadius = radius;
            else if (index <= 4)
                _yRadius = radius;
            else
                _zRadius = radius;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadEllipsoidEntity(
                _center,
                _xAxis,
                _yAxis,
                _zAxis,
                _xRadius,
                _yRadius,
                _zRadius));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadEllipsoidEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _center = value._center;
        _xAxis = value._xAxis;
        _yAxis = value._yAxis;
        _zAxis = value._zAxis;
        _xRadius = value._xRadius;
        _yRadius = value._yRadius;
        _zRadius = value._zRadius;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        _center = Translated(_center, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _center = CadTransformMath.RotatePoint(
            _center,
            center,
            axis,
            angleDegrees);
        _xAxis = CadTransformMath.RotateVector(
            _xAxis,
            axis,
            angleDegrees).Normalized();
        _yAxis = CadTransformMath.RotateVector(
            _yAxis,
            axis,
            angleDegrees).Normalized();
        _zAxis = CadTransformMath.RotateVector(
            _zAxis,
            axis,
            angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        _center = CadTransformMath.ScalePoint(_center, center, factor);
        _xRadius *= factor;
        _yRadius *= factor;
        _zRadius *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    private static void ValidateRadii(
        double xRadius,
        double yRadius,
        double zRadius)
    {
        ValidatePositive(xRadius, nameof(xRadius));
        ValidatePositive(yRadius, nameof(yRadius));
        ValidatePositive(zRadius, nameof(zRadius));
    }

    private static void ValidateFrame(
        OcctVector3d xAxis,
        OcctVector3d yAxis,
        OcctVector3d zAxis)
    {
        if (!xAxis.TryNormalize(out xAxis) ||
            !yAxis.TryNormalize(out yAxis) ||
            !zAxis.TryNormalize(out zAxis) ||
            Math.Abs(xAxis.Dot(yAxis)) > 1e-8 ||
            Math.Abs(xAxis.Dot(zAxis)) > 1e-8 ||
            Math.Abs(yAxis.Dot(zAxis)) > 1e-8 ||
            xAxis.Cross(yAxis).Dot(zAxis) < 0.999999)
        {
            throw new ArgumentException(
                "Axes must form a right-handed orthonormal frame.");
        }
    }

    internal static JsonObject WriteGeometry(CadEllipsoidEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["xAxis"] = CadEntityJson.Vector(entity.XAxis),
            ["yAxis"] = CadEntityJson.Vector(entity.YAxis),
            ["zAxis"] = CadEntityJson.Vector(entity.ZAxis),
            ["xRadius"] = entity.XRadius,
            ["yRadius"] = entity.YRadius,
            ["zRadius"] = entity.ZRadius
        };

    internal static CadEllipsoidEntity ReadGeometry(JsonObject data) =>
        new(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "xAxis"),
            CadEntityJson.ReadVector(data, "yAxis"),
            CadEntityJson.ReadVector(data, "zAxis"),
            CadEntityJson.ReadDouble(data, "xRadius"),
            CadEntityJson.ReadDouble(data, "yRadius"),
            CadEntityJson.ReadDouble(data, "zRadius"));
}
