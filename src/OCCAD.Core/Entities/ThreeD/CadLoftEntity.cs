using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadLoftEntity : CadEntity
{
    private readonly List<CadEntity> _sections;
    private bool _makeSolid;
    private bool _ruled;

    public CadLoftEntity(
        IEnumerable<CadEntity> sections,
        bool makeSolid = true,
        bool ruled = false) : base("Loft")
    {
        ArgumentNullException.ThrowIfNull(sections);

        _sections = sections
            .Select(CadPlanarProfileGeometry.WireProfileSnapshot)
            .ToList();

        if (_sections.Count < 2)
            throw new ArgumentException(
                "Loft requires at least two wire sections.",
                nameof(sections));

        _makeSolid = makeSolid;
        _ruled = ruled;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Geometry"), ReadOnly(true)]
    public int SectionCount => _sections.Count;

    [Category("Geometry")]
    public bool MakeSolid
    {
        get => _makeSolid;
        set => SetGeometry(ref _makeSolid, value);
    }

    [Category("Geometry")]
    public bool Ruled
    {
        get => _ruled;
        set => SetGeometry(ref _ruled, value);
    }

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var wires = new List<OcctShape>(_sections.Count);
        try
        {
            foreach (var section in _sections)
                wires.Add(section.BuildShape(engine));

            return engine.Loft(
                wires,
                makeSolid: _makeSolid,
                ruled: _ruled,
                tolerance: 1e-6,
                hideInputs: true);
        }
        finally
        {
            foreach (var wire in wires)
            {
                if (engine.ContainsObject(wire.Id))
                    engine.Delete(wire);
            }
        }
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var result = new List<CadSnapPoint>(_sections.Count);
        for (var index = 0; index < _sections.Count; index++)
        {
            result.Add(
                new CadSnapPoint(
                    this,
                    CadPlanarProfileGeometry.Center(_sections[index]),
                    CadSnapType.Center,
                    index));
        }

        return result;
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var result = new List<CadGripPoint>(_sections.Count);
        for (var index = 0; index < _sections.Count; index++)
        {
            result.Add(
                new CadGripPoint(
                    this,
                    index,
                    CadPlanarProfileGeometry.Center(_sections[index]),
                    Kind: CadGripKind.Center));
        }

        return result;
    }

    public override void MoveGrip(
        int index,
        OcctPoint3d targetPoint)
    {
        if ((uint)index >= (uint)_sections.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var center =
            CadPlanarProfileGeometry.Center(_sections[index]);
        _sections[index].Translate(
            CadTransformMath.Between(
                center,
                targetPoint));
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadLoftEntity(
                _sections,
                _makeSolid,
                _ruled));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadLoftEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _sections.Clear();
        _sections.AddRange(
            value._sections.Select(
                CadPlanarProfileGeometry.WireProfileSnapshot));
        _makeSolid = value._makeSolid;
        _ruled = value._ruled;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        foreach (var section in _sections)
            section.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        foreach (var section in _sections)
            section.Rotate(center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        foreach (var section in _sections)
            section.Scale(center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadLoftEntity entity)
    {
        var sections = new JsonArray();
        foreach (var section in entity._sections)
            sections.Add(CadPlanarProfileGeometry.Write(section));

        return new JsonObject
        {
            ["sections"] = sections,
            ["makeSolid"] = entity._makeSolid,
            ["ruled"] = entity._ruled
        };
    }

    internal static CadLoftEntity ReadGeometry(JsonObject data)
    {
        var sectionData =
            data["sections"] as JsonArray ??
            throw new FormatException(
                "Loft sections are missing.");

        var sections = sectionData
            .Select(node =>
                CadPlanarProfileGeometry.Read(
                    node as JsonObject ??
                    throw new FormatException(
                        "Loft section is invalid.")))
            .ToArray();

        return new CadLoftEntity(
            sections,
            CadEntityJson.ReadBool(data, "makeSolid"),
            CadEntityJson.ReadBool(data, "ruled"));
    }
}
