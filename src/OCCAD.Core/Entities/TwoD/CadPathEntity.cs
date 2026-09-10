using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadPathEntity : CadEntity
{
    private const double JoinTolerance = 1e-7;
    private readonly List<CadPathSegment> _segments;

    public CadPathEntity(
        IEnumerable<CadEntity> segments)
        : this(
            ConvertSegments(segments))
    {
    }

    public CadPathEntity(
        IEnumerable<CadPathSegment> segments)
        : base("Path")
    {
        ArgumentNullException.ThrowIfNull(segments);
        _segments = segments.ToList();
        ValidateChain(_segments);
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)]
    public IReadOnlyList<CadPathSegment> Segments =>
        _segments;

    [Category("Geometry"), ReadOnly(true)]
    public int SegmentCount => _segments.Count;

    [Category("Geometry"), ReadOnly(true)]
    public bool Closed =>
        Start.DistanceTo(End) <= JoinTolerance;

    [Browsable(false)]
    public OcctPoint3d Start =>
        _segments[0].Start;

    [Browsable(false)]
    public OcctPoint3d End =>
        _segments[^1].End;

    [Category("Measurement"), ReadOnly(true)]
    public double Length =>
        _segments.Sum(static segment =>
            segment.Length);

    internal override OcctShape BuildShape(
        OcctEngine engine)
    {
        using var model = new OcctModelingSession();
        var edges = new List<OcctModelShape>(
            _segments.Count);

        foreach (var segment in _segments)
        {
            edges.Add(
                segment switch
                {
                    CadLineSegment line =>
                        model.MakeLine(
                            line.Start,
                            line.End),
                    CadArcSegment arc =>
                        model.MakeArc(
                            arc.Start,
                            arc.Middle,
                            arc.End),
                    _ => throw new InvalidOperationException(
                        "Path contains an unsupported segment.")
                });
        }

        var wire = model.MakeWire(edges);
        return engine.CreateShapeFromModel(
            model,
            wire);
    }

    internal override IReadOnlyList<CadSnapCurve>
        GetPrecisionSnapCurves(
            CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);

        var result = new List<CadSnapCurve>();
        foreach (var segment in _segments)
        {
            var transient = segment.ToEntity();
            foreach (var curve in transient
                         .GetPrecisionSnapCurves(workPlane))
            {
                result.Add(
                    curve with
                    {
                        Entity = this,
                        Index = result.Count
                    });
            }
        }

        return result;
    }

    public bool TryGetSegmentInfo(
        int segmentIndex,
        out CadPathSegmentInfo info)
    {
        if ((uint)segmentIndex >=
            (uint)_segments.Count)
        {
            info = default;
            return false;
        }

        var segment = _segments[segmentIndex];
        info = segment switch
        {
            CadLineSegment line =>
                new CadPathSegmentInfo(
                    segmentIndex,
                    CadPathSegmentType.Line,
                    line.Start,
                    line.End,
                    line.Length),

            CadArcSegment arc =>
                new CadPathSegmentInfo(
                    segmentIndex,
                    CadPathSegmentType.Arc,
                    arc.Start,
                    arc.End,
                    arc.ArcLength,
                    arc.Middle,
                    arc.Radius),

            _ => default
        };

        return segment is
            CadLineSegment or
            CadArcSegment;
    }

    public override IReadOnlyList<CadSnapPoint>
        GetSnapPoints()
    {
        var result = new List<CadSnapPoint>();

        foreach (var segment in _segments)
        {
            var transient = segment.ToEntity();
            foreach (var snap in transient.GetSnapPoints())
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

        return result;
    }

    public override IReadOnlyList<CadGripPoint>
        GetGripPoints()
    {
        var result = new List<CadGripPoint>
        {
            new(
                this,
                0,
                ApproximateCenter(),
                Kind: CadGripKind.Center)
        };

        var vertexBindings =
            EditableVertexBindings();

        foreach (var binding in vertexBindings)
        {
            var sourceGrip =
                ResolveConstraintGrip(binding);

            result.Add(
                new CadGripPoint(
                    this,
                    result.Count,
                    binding.Position,
                    sourceGrip?.WorkPlane,
                    sourceGrip?.ConstraintOrigin,
                    sourceGrip?.PrecisionInputs ??
                        CadPrecisionInputKind
                            .LengthAndAngle,
                    CadGripKind.Vertex));
        }

        foreach (var binding in ArcMiddleBindings())
        {
            var arc =
                ((CadArcSegment)_segments[
                    binding.SegmentIndex])
                .AsArc();

            var sourceGrip = arc
                .GetGripPoints()
                .First(static grip =>
                    grip.Index == 3);

            result.Add(
                new CadGripPoint(
                    this,
                    result.Count,
                    binding.Position,
                    sourceGrip.WorkPlane,
                    arc.Center,
                    CadPrecisionInputKind.Length,
                    CadGripKind.Radius));
        }

        return result;
    }

    public override void MoveGrip(
        int index,
        OcctPoint3d targetPoint)
    {
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(
                nameof(targetPoint));

        if (index == 0)
        {
            var center = ApproximateCenter();
            var displacement =
                CadTransformMath.Between(
                    center,
                    targetPoint);

            if (displacement.LengthSquared <= 1e-24)
                return;

            Translate(displacement);
            return;
        }

        var vertexBindings =
            EditableVertexBindings();
        var vertexIndex = index - 1;

        if ((uint)vertexIndex <
            (uint)vertexBindings.Count)
        {
            MoveVertexGrip(
                vertexBindings[vertexIndex],
                targetPoint);
            return;
        }

        var arcBindings = ArcMiddleBindings();
        var arcIndex =
            vertexIndex - vertexBindings.Count;

        if ((uint)arcIndex >=
            (uint)arcBindings.Count)
            throw new ArgumentOutOfRangeException(
                nameof(index));

        MoveArcMiddleGrip(
            arcBindings[arcIndex],
            targetPoint);
    }

    private void MoveVertexGrip(
        PathVertexBinding binding,
        OcctPoint3d targetPoint)
    {
        var updated = _segments.ToList();
        var resolvedTarget = targetPoint;

        var previousArcIndex =
            binding.PreviousSegmentIndex;
        var nextArcIndex =
            binding.NextSegmentIndex;

        if (previousArcIndex is { } previousIndex &&
            nextArcIndex is { } nextIndex &&
            updated[previousIndex] is
                CadArcSegment previousSegment &&
            updated[nextIndex] is
                CadArcSegment nextSegment)
        {
            var previousArc =
                previousSegment.AsArc();
            var nextArc =
                nextSegment.AsArc();

            if (!TryResolveArcArcJunction(
                    previousArc,
                    nextArc,
                    targetPoint,
                    out resolvedTarget))
            {
                throw new InvalidOperationException(
                    "The two fixed arc circles do not have a valid common junction.");
            }

            previousArc.MoveGrip(
                2,
                resolvedTarget);
            nextArc.MoveGrip(
                1,
                resolvedTarget);

            if (previousArc.End.DistanceTo(
                    nextArc.Start) >
                JoinTolerance)
            {
                throw new InvalidOperationException(
                    "Arc junction solve did not preserve path continuity.");
            }

            updated[previousIndex] =
                CadPathSegment.FromEntity(
                    previousArc,
                    applyPlacement: false);
            updated[nextIndex] =
                CadPathSegment.FromEntity(
                    nextArc,
                    applyPlacement: false);
        }
        else if (
            previousArcIndex is { } previousOnly &&
            updated[previousOnly] is
                CadArcSegment previousOnlySegment)
        {
            var previousOnlyArc =
                previousOnlySegment.AsArc();
            previousOnlyArc.MoveGrip(
                2,
                resolvedTarget);
            resolvedTarget =
                previousOnlyArc.End;
            updated[previousOnly] =
                CadPathSegment.FromEntity(
                    previousOnlyArc,
                    applyPlacement: false);
        }
        else if (
            nextArcIndex is { } nextOnly &&
            updated[nextOnly] is
                CadArcSegment nextOnlySegment)
        {
            var nextOnlyArc =
                nextOnlySegment.AsArc();
            nextOnlyArc.MoveGrip(
                1,
                resolvedTarget);
            resolvedTarget = nextOnlyArc.Start;
            updated[nextOnly] =
                CadPathSegment.FromEntity(
                    nextOnlyArc,
                    applyPlacement: false);
        }

        if (binding.PreviousSegmentIndex is
                { } previousLineIndex &&
            updated[previousLineIndex] is
                CadLineSegment previousLine)
        {
            updated[previousLineIndex] =
                new CadLineSegment(
                    previousLine.Start,
                    resolvedTarget);
        }

        if (binding.NextSegmentIndex is
                { } nextLineIndex &&
            updated[nextLineIndex] is
                CadLineSegment nextLine)
        {
            updated[nextLineIndex] =
                new CadLineSegment(
                    resolvedTarget,
                    nextLine.End);
        }

        ReplaceSegments(
            updated,
            nameof(MoveGrip));
    }

    private void MoveArcMiddleGrip(
        PathArcGripBinding binding,
        OcctPoint3d targetPoint)
    {
        var updated = _segments.ToList();
        var arc =
            (CadArcSegment)updated[
                binding.SegmentIndex];

        updated[binding.SegmentIndex] =
            new CadArcSegment(
                arc.Start,
                targetPoint,
                arc.End);

        ReplaceSegments(
            updated,
            nameof(MoveGrip));
    }

    private void ReplaceSegments(
        IReadOnlyList<CadPathSegment> updated,
        string propertyName)
    {
        ValidateChain(updated);
        _segments.Clear();
        _segments.AddRange(updated);
        RaiseGeometryChanged(propertyName);
    }

    private CadGripPoint? ResolveConstraintGrip(
        PathVertexBinding binding)
    {
        if (binding.PreviousSegmentIndex is
                { } previousIndex &&
            _segments[previousIndex] is
                CadArcSegment previousArc)
        {
            return previousArc
                .AsArc()
                .GetGripPoints()
                .FirstOrDefault(
                    static grip =>
                        grip.Index == 2);
        }

        if (binding.NextSegmentIndex is
                { } nextIndex &&
            _segments[nextIndex] is
                CadArcSegment nextArc)
        {
            return nextArc
                .AsArc()
                .GetGripPoints()
                .FirstOrDefault(
                    static grip =>
                        grip.Index == 1);
        }

        return null;
    }

    private IReadOnlyList<PathVertexBinding>
        EditableVertexBindings()
    {
        var result =
            new List<PathVertexBinding>();
        var count = _segments.Count;

        if (!Closed)
        {
            result.Add(
                new PathVertexBinding(
                    _segments[0].Start,
                    PreviousSegmentIndex: null,
                    NextSegmentIndex: 0));

            for (var vertex = 1;
                 vertex < count;
                 vertex++)
            {
                var previous =
                    _segments[vertex - 1];
                var next =
                    _segments[vertex];

                result.Add(
                    new PathVertexBinding(
                        previous.End,
                        vertex - 1,
                        vertex));
            }

            result.Add(
                new PathVertexBinding(
                    _segments[^1].End,
                    count - 1,
                    NextSegmentIndex: null));

            return result;
        }

        for (var vertex = 0;
             vertex < count;
             vertex++)
        {
            var previousIndex =
                vertex == 0
                    ? count - 1
                    : vertex - 1;

            result.Add(
                new PathVertexBinding(
                    _segments[previousIndex].End,
                    previousIndex,
                    vertex));
        }

        return result;
    }

    private IReadOnlyList<PathArcGripBinding>
        ArcMiddleBindings()
    {
        var result =
            new List<PathArcGripBinding>();

        for (var index = 0;
             index < _segments.Count;
             index++)
        {
            if (_segments[index] is
                CadArcSegment arc)
            {
                result.Add(
                    new PathArcGripBinding(
                        index,
                        arc.Middle));
            }
        }

        return result;
    }

    private static bool TryResolveArcArcJunction(
        CadArcEntity first,
        CadArcEntity second,
        OcctPoint3d target,
        out OcctPoint3d result)
    {
        result = default;

        var firstNormal =
            first.Normal.Normalized();
        var secondNormal =
            second.Normal.Normalized();

        if (Math.Abs(
                CadTransformMath.Dot(
                    firstNormal,
                    secondNormal)) <
            1.0 - 1e-8)
            return false;

        var centerDelta =
            CadTransformMath.Between(
                first.Center,
                second.Center);
        var axial =
            CadTransformMath.Dot(
                centerDelta,
                firstNormal);

        if (Math.Abs(axial) > JoinTolerance)
            return false;

        var planar = new OcctVector3d(
            centerDelta.X -
                firstNormal.X * axial,
            centerDelta.Y -
                firstNormal.Y * axial,
            centerDelta.Z -
                firstNormal.Z * axial);

        var distanceSquared =
            planar.LengthSquared;

        if (distanceSquared <=
            JoinTolerance * JoinTolerance)
            return false;

        var distance =
            Math.Sqrt(distanceSquared);
        var firstRadius = first.Radius;
        var secondRadius = second.Radius;

        if (distance >
                firstRadius +
                secondRadius +
                JoinTolerance ||
            distance <
                Math.Abs(
                    firstRadius -
                    secondRadius) -
                JoinTolerance)
            return false;

        var direction =
            planar * (1.0 / distance);

        var along =
            (firstRadius * firstRadius -
             secondRadius * secondRadius +
             distanceSquared) /
            (2.0 * distance);

        var heightSquared =
            firstRadius * firstRadius -
            along * along;

        if (heightSquared <
            -JoinTolerance * JoinTolerance)
            return false;

        var height =
            Math.Sqrt(
                Math.Max(
                    0.0,
                    heightSquared));

        var basePoint =
            first.Center +
            direction * along;

        if (height <= JoinTolerance)
        {
            result = basePoint;
            return result.IsFinite;
        }

        var perpendicular =
            firstNormal
                .Cross(direction)
                .Normalized();

        var firstCandidate =
            basePoint +
            perpendicular * height;
        var secondCandidate =
            basePoint -
            perpendicular * height;

        result =
            firstCandidate.DistanceTo(target) <=
            secondCandidate.DistanceTo(target)
                ? firstCandidate
                : secondCandidate;

        return result.IsFinite;
    }

    private OcctPoint3d ApproximateCenter()
    {
        var points = _segments
            .SelectMany(
                static segment =>
                    segment switch
                    {
                        CadLineSegment line =>
                            new[]
                            {
                                line.Start,
                                line.End
                            },
                        CadArcSegment arc =>
                            new[]
                            {
                                arc.Start,
                                arc.Middle,
                                arc.End
                            },
                        _ =>
                            Array.Empty<OcctPoint3d>()
                    })
            .ToArray();

        var x = 0.0;
        var y = 0.0;
        var z = 0.0;

        foreach (var point in points)
        {
            x += point.X;
            y += point.Y;
            z += point.Z;
        }

        var scale = 1.0 / points.Length;
        return new OcctPoint3d(
            x * scale,
            y * scale,
            z * scale);
    }

    internal void SplitSegment(
        int segmentIndex,
        OcctPoint3d point)
    {
        if ((uint)segmentIndex >=
            (uint)_segments.Count)
            throw new ArgumentOutOfRangeException(
                nameof(segmentIndex));
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(
                nameof(point));

        var source =
            _segments[segmentIndex];

        if (!source.TryClosestParameter(
                point,
                out var parameter) ||
            parameter <= 1e-7 ||
            parameter >= 1.0 - 1e-7)
        {
            throw new InvalidOperationException(
                "Split point must be inside the Path segment.");
        }

        var parts = source.Split(parameter);
        if (parts.Count != 2)
            throw new InvalidOperationException(
                "Path segment split must return two segments.");

        var updated = _segments.ToList();
        updated.RemoveAt(segmentIndex);
        updated.Insert(
            segmentIndex,
            parts[1]);
        updated.Insert(
            segmentIndex,
            parts[0]);

        ReplaceSegments(
            updated,
            nameof(SplitSegment));
    }

    public void Reverse()
    {
        var updated = _segments
            .AsEnumerable()
            .Reverse()
            .Select(
                static segment =>
                    segment.Reverse())
            .ToArray();

        ReplaceSegments(
            updated,
            nameof(Reverse));
    }

    public void Close()
    {
        if (Closed)
            return;

        var updated = _segments.ToList();
        updated.Add(
            new CadLineSegment(
                End,
                Start));

        ReplaceSegments(
            updated,
            nameof(Close));
    }

    public void Open()
    {
        if (!Closed)
            return;

        if (_segments.Count <= 1)
            throw new InvalidOperationException(
                "A closed Path requires at least two segments to open.");

        var updated = _segments
            .Take(_segments.Count - 1)
            .ToArray();

        if (updated.Length == 0 ||
            updated[0].Start.DistanceTo(
                updated[^1].End) <=
            JoinTolerance)
        {
            throw new InvalidOperationException(
                "The Path cannot be opened by removing its closing segment.");
        }

        ReplaceSegments(
            updated,
            nameof(Open));
    }

    internal CadPathEntity CopyWithSegments(
        IEnumerable<CadEntity> segments) =>
        CopyPropertiesTo(
            new CadPathEntity(segments));

    internal CadPathEntity CopyWithSegments(
        IEnumerable<CadPathSegment> segments) =>
        CopyPropertiesTo(
            new CadPathEntity(segments));

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadPathEntity(_segments));

    public override void RestoreGeometry(
        CadEntity snapshot)
    {
        if (snapshot is not CadPathEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _segments.Clear();
        _segments.AddRange(value._segments);
        RaiseGeometryChanged(
            nameof(RestoreGeometry));
    }

    public override void Translate(
        OcctVector3d displacement)
    {
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(
                nameof(displacement));

        ReplaceSegments(
            _segments
                .Select(segment =>
                    segment.Translate(
                        displacement))
                .ToArray(),
            nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        ReplaceSegments(
            _segments
                .Select(segment =>
                    segment.Rotate(
                        center,
                        axis,
                        angleDegrees))
                .ToArray(),
            nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);

        ReplaceSegments(
            _segments
                .Select(segment =>
                    segment.Scale(
                        center,
                        factor))
                .ToArray(),
            nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadPathEntity entity)
    {
        var segments = new JsonArray();

        foreach (var segment in entity._segments)
        {
            segments.Add(
                segment switch
                {
                    CadLineSegment line =>
                        new JsonObject
                        {
                            ["type"] = "line",
                            ["start"] =
                                CadEntityJson.Point(
                                    line.Start),
                            ["end"] =
                                CadEntityJson.Point(
                                    line.End)
                        },

                    CadArcSegment arc =>
                        new JsonObject
                        {
                            ["type"] = "arc",
                            ["start"] =
                                CadEntityJson.Point(
                                    arc.Start),
                            ["middle"] =
                                CadEntityJson.Point(
                                    arc.Middle),
                            ["end"] =
                                CadEntityJson.Point(
                                    arc.End)
                        },

                    _ => throw new InvalidOperationException(
                        "Path contains an unsupported segment.")
                });
        }

        return new JsonObject
        {
            ["segments"] = segments
        };
    }

    internal static CadPathEntity ReadGeometry(
        JsonObject data)
    {
        var values =
            data["segments"] as JsonArray ??
            throw new FormatException(
                "Path segments are missing.");

        var segments = values
            .Select(node =>
            {
                var item =
                    node as JsonObject ??
                    throw new FormatException(
                        "Path segment is invalid.");

                var type =
                    item["type"]?.GetValue<string>() ??
                    throw new FormatException(
                        "Path segment type is missing.");

                // Accept both the new compact segment form and version-1
                // nested entity geometry without a migration layer.
                if (item["geometry"] is
                    JsonObject legacyGeometry)
                {
                    return CadPathSegment.FromEntity(
                        type.ToLowerInvariant() switch
                        {
                            "line" =>
                                CadLineEntity.ReadGeometry(
                                    legacyGeometry),
                            "arc" =>
                                CadArcEntity.ReadGeometry(
                                    legacyGeometry),
                            _ => throw new FormatException(
                                $"Unsupported path segment type '{type}'.")
                        },
                        applyPlacement: false);
                }

                return type.ToLowerInvariant() switch
                {
                    "line" =>
                        new CadLineSegment(
                            CadEntityJson.ReadPoint(
                                item,
                                "start"),
                            CadEntityJson.ReadPoint(
                                item,
                                "end")),

                    "arc" =>
                        new CadArcSegment(
                            CadEntityJson.ReadPoint(
                                item,
                                "start"),
                            CadEntityJson.ReadPoint(
                                item,
                                "middle"),
                            CadEntityJson.ReadPoint(
                                item,
                                "end")),

                    _ => throw new FormatException(
                        $"Unsupported path segment type '{type}'.")
                };
            })
            .ToArray();

        return new CadPathEntity(segments);
    }

    internal static CadEntity SnapshotSegment(
        CadPathSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return segment.ToEntity();
    }

    internal static CadEntity SnapshotSegment(
        CadEntity segment) =>
        segment switch
        {
            CadLineEntity line => line.Duplicate(),
            CadArcEntity arc => arc.Duplicate(),
            _ => throw new ArgumentException(
                "Path segments must be lines or arcs.",
                nameof(segment))
        };

    internal static OcctPoint3d SegmentStart(
        CadPathSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return segment.Start;
    }

    internal static OcctPoint3d SegmentStart(
        CadEntity segment) =>
        segment switch
        {
            CadLineEntity line => line.Start,
            CadArcEntity arc => arc.Start,
            _ => throw new ArgumentException(
                "Unsupported path segment.",
                nameof(segment))
        };

    internal static OcctPoint3d SegmentEnd(
        CadPathSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return segment.End;
    }

    internal static OcctPoint3d SegmentEnd(
        CadEntity segment) =>
        segment switch
        {
            CadLineEntity line => line.End,
            CadArcEntity arc => arc.End,
            _ => throw new ArgumentException(
                "Unsupported path segment.",
                nameof(segment))
        };

    private static IEnumerable<CadPathSegment>
        ConvertSegments(
            IEnumerable<CadEntity> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return segments.Select(
            static segment =>
                CadPathSegment.FromEntity(
                    segment,
                    applyPlacement: true));
    }

    private readonly record struct PathVertexBinding(
        OcctPoint3d Position,
        int? PreviousSegmentIndex,
        int? NextSegmentIndex);

    private readonly record struct PathArcGripBinding(
        int SegmentIndex,
        OcctPoint3d Position);

    private static void ValidateChain(
        IReadOnlyList<CadPathSegment> segments)
    {
        if (segments.Count == 0)
            throw new ArgumentException(
                "Path requires at least one segment.",
                nameof(segments));

        for (var index = 1;
             index < segments.Count;
             index++)
        {
            if (segments[index - 1]
                    .End
                    .DistanceTo(
                        segments[index].Start) >
                JoinTolerance)
            {
                throw new ArgumentException(
                    "Path segments must form one continuous ordered chain.",
                    nameof(segments));
            }
        }
    }
}
