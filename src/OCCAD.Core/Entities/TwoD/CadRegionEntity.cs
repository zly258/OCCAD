using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadRegionEntity : CadEntity
{
    private CadEntity _outer;
    private readonly List<CadEntity> _holes;

    public CadRegionEntity(
        CadEntity outer,
        IEnumerable<CadEntity>? holes = null) : base("Region")
    {
        if (!CadPlanarProfileGeometry.IsSource(outer))
            throw new ArgumentException(
                "Region outer boundary must be a closed planar profile.",
                nameof(outer));

        var outerSnapshot =
            outer.CreateWorldGeometrySnapshot();
        var values = (holes?.ToArray() ?? Array.Empty<CadEntity>())
            .Select(static hole =>
                hole.CreateWorldGeometrySnapshot())
            .ToArray();
        if (!CadPlanarProfileGeometry.AreCoplanar(
                outerSnapshot,
                values))
            throw new ArgumentException(
                "Region boundaries must be coplanar.",
                nameof(holes));

        _outer = outerSnapshot;
        _holes = values.ToList();
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Browsable(false)]
    public string ProfileType => _outer.EntityType;

    [Category("Geometry"), ReadOnly(true)]
    public int HoleCount => _holes.Count;

    internal CadEntity OuterSnapshot() =>
        _outer.Duplicate();

    internal IReadOnlyList<CadEntity> HoleSnapshots() =>
        _holes
            .Select(static hole => hole.Duplicate())
            .ToArray();

    internal CadEntity ProfileSnapshot() =>
        OuterSnapshot();

    internal override OcctShape BuildShape(OcctEngine engine) =>
        CadPlanarProfileGeometry.BuildFace(engine, this);

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var result = new List<CadSnapPoint>();

        AppendSnaps(_outer);
        foreach (var hole in _holes)
            AppendSnaps(hole);

        return result;

        void AppendSnaps(CadEntity source)
        {
            foreach (var snap in source.GetSnapPoints())
            {
                result.Add(
                    new CadSnapPoint(
                        this,
                        snap.Position,
                        snap.Type,
                        result.Count,
                        snap.WorkPlane));
            }
        }
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var center =
            CadPlanarProfileGeometry.Center(_outer);
        return
        [
            new(
                this,
                0,
                center,
                Kind: CadGripKind.Center)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        if (index != 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var center =
            CadPlanarProfileGeometry.Center(_outer);
        Translate(
            CadTransformMath.Between(
                center,
                targetPoint));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadRegionEntity(
                _outer,
                _holes));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadRegionEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _outer = value._outer.Duplicate();
        _holes.Clear();
        _holes.AddRange(
            value._holes.Select(
                static hole => hole.Duplicate()));
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        _outer.Translate(displacement);
        foreach (var hole in _holes)
            hole.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _outer.Rotate(center, axis, angleDegrees);
        foreach (var hole in _holes)
            hole.Rotate(center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        _outer.Scale(center, factor);
        foreach (var hole in _holes)
            hole.Scale(center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadRegionEntity entity)
    {
        var holes = new JsonArray();
        foreach (var hole in entity._holes)
            holes.Add(CadPlanarProfileGeometry.Write(hole));

        return new JsonObject
        {
            ["outer"] =
                CadPlanarProfileGeometry.Write(entity._outer),
            ["holes"] = holes
        };
    }

    internal static CadRegionEntity ReadGeometry(JsonObject data)
    {
        var outer =
            CadPlanarProfileGeometry.Read(
                data["outer"] as JsonObject ??
                throw new FormatException(
                    "Region outer profile is missing."));

        var holes = data["holes"] is JsonArray values
            ? values
                .Select(node =>
                    CadPlanarProfileGeometry.Read(
                        node as JsonObject ??
                        throw new FormatException(
                            "Region hole profile is invalid.")))
                .ToArray()
            : Array.Empty<CadEntity>();

        return new CadRegionEntity(
            outer,
            holes);
    }
}
