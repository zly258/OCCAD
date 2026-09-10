using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadLoftEntity : CadFeatureEntity
{
    private readonly List<CadEntity> _sections;
    private readonly List<Guid?> _sectionSourceIds;
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

        _sectionSourceIds =
            Enumerable.Repeat<Guid?>(
                null,
                _sections.Count)
            .ToList();

        _makeSolid = makeSolid;
        _ruled = ruled;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Geometry"), ReadOnly(true)]
    public int SectionCount => _sections.Count;

    public override IReadOnlyList<CadFeatureInputDescriptor> Inputs =>
        _sections
            .Select((section, index) =>
                _sectionSourceIds[index] is { } sourceId
                    ? SourceInput(
                        $"Section{index + 1}",
                        section,
                        sourceId)
                    : CapturedInput(
                        $"Section{index + 1}",
                        section))
            .ToArray();

    internal void BindSources(
        IReadOnlyList<CadEntity> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.Count != _sections.Count ||
            sections.Any(section =>
                !CadPlanarProfileGeometry.IsWireProfile(section)))
        {
            throw new ArgumentException(
                "Loft source sections are invalid.",
                nameof(sections));
        }

        for (var index = 0;
             index < sections.Count;
             index++)
        {
            _sectionSourceIds[index] =
                sections[index].Id;
        }
    }

    internal override bool RefreshSourceReferences(
        CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_sectionSourceIds.Any(static id => id is null))
            return false;

        var next = new List<CadEntity>(_sections.Count);
        foreach (var sourceId in _sectionSourceIds)
        {
            var source =
                document.FindById(sourceId!.Value);
            if (source is null ||
                !CadPlanarProfileGeometry.IsWireProfile(source))
                return false;

            next.Add(
                CadPlanarProfileGeometry
                    .WireProfileSnapshot(source));
        }

        _sections.Clear();
        _sections.AddRange(next);
        RaiseGeometryChanged(nameof(Inputs));
        return true;
    }

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

    protected override OcctShape BuildFeatureResult(OcctEngine engine)
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
        _sectionSourceIds[index] = null;
        _sections[index].Translate(
            CadTransformMath.Between(
                center,
                targetPoint));
        RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate()
    {
        var copy = new CadLoftEntity(
            _sections,
            _makeSolid,
            _ruled);

        for (var index = 0;
             index < _sectionSourceIds.Count;
             index++)
        {
            copy._sectionSourceIds[index] =
                _sectionSourceIds[index];
        }

        return CopyPropertiesTo(copy);
    }

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
        _sectionSourceIds.Clear();
        _sectionSourceIds.AddRange(
            value._sectionSourceIds);
        _makeSolid = value._makeSolid;
        _ruled = value._ruled;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ClearSectionSourceReferences();
        foreach (var section in _sections)
            section.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        ClearSectionSourceReferences();
        foreach (var section in _sections)
            section.Rotate(center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        ClearSectionSourceReferences();
        foreach (var section in _sections)
            section.Scale(center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private void ClearSectionSourceReferences()
    {
        for (var index = 0;
             index < _sectionSourceIds.Count;
             index++)
        {
            _sectionSourceIds[index] = null;
        }
    }

    internal static JsonObject WriteGeometry(
        CadLoftEntity entity)
    {
        var sections = new JsonArray();
        foreach (var section in entity._sections)
            sections.Add(
                CadPlanarProfileGeometry.Write(section));

        var sourceIds = new JsonArray();
        foreach (var sourceId in entity._sectionSourceIds)
            sourceIds.Add(
                sourceId is { } value
                    ? JsonValue.Create(value)
                    : null);

        return new JsonObject
        {
            ["sections"] = sections,
            ["sectionSourceIds"] = sourceIds,
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

        var entity = new CadLoftEntity(
            sections,
            CadEntityJson.ReadBool(data, "makeSolid"),
            CadEntityJson.ReadBool(data, "ruled"));

        if (data["sectionSourceIds"] is JsonArray sourceIds &&
            sourceIds.Count == entity._sectionSourceIds.Count)
        {
            for (var index = 0;
                 index < sourceIds.Count;
                 index++)
            {
                entity._sectionSourceIds[index] =
                    sourceIds[index] is null
                        ? null
                        : sourceIds[index]!.GetValue<Guid>();
            }
        }

        return entity;
    }
}
