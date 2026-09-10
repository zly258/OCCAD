using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadPointEntity : CadEntity
{
    private OcctPoint3d _position;

    public CadPointEntity(OcctPoint3d position) : base("Point")
    {
        if (!position.IsFinite) throw new ArgumentOutOfRangeException(nameof(position));
        _position = position;
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Point => _position;
    [Category("Geometry")] public double X { get => _position.X; set => SetPosition(value, _position.Y, _position.Z); }
    [Category("Geometry")] public double Y { get => _position.Y; set => SetPosition(_position.X, value, _position.Z); }
    [Category("Geometry")] public double Z { get => _position.Z; set => SetPosition(_position.X, _position.Y, value); }

    internal override OcctShape BuildShape(OcctEngine engine) => engine.MakeVertex(_position);

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
        [new(this, _position, CadSnapType.Node, 0)];

    public override IReadOnlyList<CadGripPoint> GetGripPoints() =>
        [new(this, 0, _position)];

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(targetPoint));
        SetGeometry(ref _position, targetPoint, nameof(MoveGrip));
    }

    public override CadEntity Duplicate() => CopyPropertiesTo(new CadPointEntity(_position));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadPointEntity value) throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));
        _position = value._position;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        _position = Translated(_position, displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        _position = CadTransformMath.RotatePoint(_position, center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        _position = CadTransformMath.ScalePoint(_position, center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private void SetPosition(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _position, new OcctPoint3d(x, y, z));
    }

    internal static JsonObject WriteGeometry(CadPointEntity entity) =>
        new() { ["position"] = CadEntityJson.Point(entity.Point) };

    internal static CadPointEntity ReadGeometry(JsonObject data) =>
        new(CadEntityJson.ReadPoint(data, "position"));
}
