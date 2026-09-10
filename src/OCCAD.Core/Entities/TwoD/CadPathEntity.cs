using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadPathEntity : CadEntity
{
    private const double JoinTolerance = 1e-7;
    private readonly List<CadEntity> _segments;

    public CadPathEntity(
        IEnumerable<CadEntity> segments) : base("Path")
    {
        ArgumentNullException.ThrowIfNull(segments);
        _segments = segments
            .Select(SnapshotSegment)
            .ToList();

        ValidateChain(_segments);
        DisplayMode = OcctDisplayMode.Wireframe;
    }

    [Browsable(false)]
    public IReadOnlyList<CadEntity> Segments => _segments;

    [Category("Geometry"), ReadOnly(true)]
    public int SegmentCount => _segments.Count;

    [Category("Geometry"), ReadOnly(true)]
    public bool Closed =>
        Start.DistanceTo(End) <= JoinTolerance;

    [Browsable(false)]
    public OcctPoint3d Start =>
        SegmentStart(_segments[0]);

    [Browsable(false)]
    public OcctPoint3d End =>
        SegmentEnd(_segments[^1]);

    [Category("Measurement"), ReadOnly(true)]
    public double Length =>
        _segments.Sum(static segment =>
            segment switch
            {
                CadLineEntity line => line.Length,
                CadArcEntity arc => arc.ArcLength,
                _ => 0.0
            });

    internal override OcctShape BuildShape(
        OcctEngine engine)
    {
        using var model = new OcctModelingSession();
        var edges = new List<OcctModelShape>(_segments.Count);

        foreach (var segment in _segments)
        {
            edges.Add(
                segment switch
                {
                    CadLineEntity line =>
                        model.MakeLine(line.Start, line.End),
                    CadArcEntity arc =>
                        model.MakeArc(
                            arc.Start,
                            arc.Middle,
                            arc.End),
                    _ => throw new InvalidOperationException(
                        "Path contains an unsupported segment.")
                });
        }

        var wire = model.MakeWire(edges);
        return engine.CreateShapeFromModel(model, wire);
    }

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);

        var result = new List<CadSnapCurve>();
        foreach (var segment in _segments)
        {
            foreach (var curve in segment.GetPrecisionSnapCurves(workPlane))
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

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var result = new List<CadSnapPoint>();
        for (var segmentIndex = 0;
             segmentIndex < _segments.Count;
             segmentIndex++)
        {
            foreach (var snap in _segments[segmentIndex].GetSnapPoints())
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

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var result = new List<CadGripPoint>
        {
            new(
                this,
                0,
                ApproximateCenter(),
                Kind: CadGripKind.Center)
        };

        var vertexBindings = EditableVertexBindings();
        foreach (var binding in vertexBindings)
        {
            var sourceGrip = ResolveConstraintGrip(binding);
            result.Add(
                new CadGripPoint(
                    this,
                    result.Count,
                    binding.Position,
                    sourceGrip?.WorkPlane,
                    sourceGrip?.ConstraintOrigin,
                    sourceGrip?.PrecisionInputs ??
                        CadPrecisionInputKind.LengthAndAngle,
                    CadGripKind.Vertex));
        }

        foreach (var binding in ArcMiddleBindings())
        {
            var arc = (CadArcEntity)_segments[binding.SegmentIndex];
            var sourceGrip = arc.GetGripPoints()
                .First(static grip => grip.Index == 3);
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
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        if (index == 0)
        {
            var center = ApproximateCenter();
            var displacement = CadTransformMath.Between(center, targetPoint);
            if (displacement.LengthSquared <= 1e-24)
                return;

            Translate(displacement);
            return;
        }

        var vertexBindings = EditableVertexBindings();
        var vertexIndex = index - 1;
        if ((uint)vertexIndex < (uint)vertexBindings.Count)
        {
            MoveVertexGrip(vertexBindings[vertexIndex], targetPoint);
            return;
        }

        var arcBindings = ArcMiddleBindings();
        var arcIndex = vertexIndex - vertexBindings.Count;
        if ((uint)arcIndex >= (uint)arcBindings.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        MoveArcMiddleGrip(arcBindings[arcIndex], targetPoint);
    }

    private void MoveVertexGrip(
        PathVertexBinding binding,
        OcctPoint3d targetPoint)
    {
        var updated = _segments
            .Select(SnapshotSegment)
            .ToList();

        var resolvedTarget = targetPoint;
        var previousArcIndex = binding.PreviousSegmentIndex;
        var nextArcIndex = binding.NextSegmentIndex;

        if (previousArcIndex is { } previousIndex &&
            nextArcIndex is { } nextIndex &&
            updated[previousIndex] is CadArcEntity previousArc &&
            updated[nextIndex] is CadArcEntity nextArc)
        {
            if (!TryResolveArcArcJunction(
                    previousArc,
                    nextArc,
                    targetPoint,
                    out resolvedTarget))
            {
                throw new InvalidOperationException(
                    "The two fixed arc circles do not have a valid common junction.");
            }

            previousArc.MoveGrip(2, resolvedTarget);
            nextArc.MoveGrip(1, resolvedTarget);

            if (previousArc.End.DistanceTo(nextArc.Start) > JoinTolerance)
            {
                throw new InvalidOperationException(
                    "Arc junction solve did not preserve path continuity.");
            }
        }
        else if (previousArcIndex is { } previousOnly &&
                 updated[previousOnly] is CadArcEntity previousArc)
        {
            previousArc.MoveGrip(2, resolvedTarget);
            resolvedTarget = previousArc.End;
        }
        else if (nextArcIndex is { } nextOnly &&
                 updated[nextOnly] is CadArcEntity nextArc)
        {
            nextArc.MoveGrip(1, resolvedTarget);
            resolvedTarget = nextArc.Start;
        }

        if (binding.PreviousSegmentIndex is { } previousLineIndex &&
            updated[previousLineIndex] is CadLineEntity previousLine)
        {
            updated[previousLineIndex] =
                previousLine.CreateLine(
                    previousLine.Start,
                    resolvedTarget);
        }

        if (binding.NextSegmentIndex is { } nextLineIndex &&
            updated[nextLineIndex] is CadLineEntity nextLine)
        {
            updated[nextLineIndex] =
                nextLine.CreateLine(
                    resolvedTarget,
                    nextLine.End);
        }

        ReplaceSegments(updated, nameof(MoveGrip));
    }

    private void MoveArcMiddleGrip(
        PathArcGripBinding binding,
        OcctPoint3d targetPoint)
    {
        var updated = _segments
            .Select(SnapshotSegment)
            .ToList();
        var arc = (CadArcEntity)updated[binding.SegmentIndex];

        // Reconstructing from fixed endpoints and the requested middle point is
        // the Path-level constraint solve. The adjacent junctions remain exactly
        // where they were; an invalid/collinear request is rejected by CadArcEntity
        // and therefore by GripEditTool without changing the real Path.
        updated[binding.SegmentIndex] =
            arc.CreateArc(
                arc.Start,
                targetPoint,
                arc.End);

        ReplaceSegments(updated, nameof(MoveGrip));
    }

    private void ReplaceSegments(
        IReadOnlyList<CadEntity> updated,
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
        if (binding.PreviousSegmentIndex is { } previousIndex &&
            _segments[previousIndex] is CadArcEntity previousArc)
        {
            return previousArc.GetGripPoints()
                .FirstOrDefault(static grip => grip.Index == 2);
        }

        if (binding.NextSegmentIndex is { } nextIndex &&
            _segments[nextIndex] is CadArcEntity nextArc)
        {
            return nextArc.GetGripPoints()
                .FirstOrDefault(static grip => grip.Index == 1);
        }

        return null;
    }

    private IReadOnlyList<PathVertexBinding> EditableVertexBindings()
    {
        var result = new List<PathVertexBinding>();
        var count = _segments.Count;

        if (!Closed)
        {
            if (SupportsStartVertex(_segments[0]))
            {
                result.Add(
                    new PathVertexBinding(
                        SegmentStart(_segments[0]),
                        PreviousSegmentIndex: null,
                        NextSegmentIndex: 0));
            }

            for (var vertex = 1; vertex < count; vertex++)
            {
                var previous = _segments[vertex - 1];
                var next = _segments[vertex];

                if (!SupportsSharedVertex(previous, next))
                    continue;

                result.Add(
                    new PathVertexBinding(
                        SegmentEnd(previous),
                        vertex - 1,
                        vertex));
            }

            if (SupportsEndVertex(_segments[^1]))
            {
                result.Add(
                    new PathVertexBinding(
                        SegmentEnd(_segments[^1]),
                        count - 1,
                        NextSegmentIndex: null));
            }

            return result;
        }

        for (var vertex = 0; vertex < count; vertex++)
        {
            var previousIndex = vertex == 0 ? count - 1 : vertex - 1;
            var previous = _segments[previousIndex];
            var next = _segments[vertex];

            if (!SupportsSharedVertex(previous, next))
                continue;

            result.Add(
                new PathVertexBinding(
                    SegmentEnd(previous),
                    previousIndex,
                    vertex));
        }

        return result;
    }

    private IReadOnlyList<PathArcGripBinding> ArcMiddleBindings()
    {
        var result = new List<PathArcGripBinding>();
        for (var index = 0; index < _segments.Count; index++)
        {
            if (_segments[index] is CadArcEntity arc)
                result.Add(new PathArcGripBinding(index, arc.Middle));
        }
        return result;
    }

    private static bool SupportsStartVertex(CadEntity segment) =>
        segment is CadLineEntity or CadArcEntity;

    private static bool SupportsEndVertex(CadEntity segment) =>
        segment is CadLineEntity or CadArcEntity;

    private static bool SupportsSharedVertex(
        CadEntity previous,
        CadEntity next) =>
        previous is CadLineEntity or CadArcEntity &&
        next is CadLineEntity or CadArcEntity;

    private static bool TryResolveArcArcJunction(
        CadArcEntity first,
        CadArcEntity second,
        OcctPoint3d target,
        out OcctPoint3d result)
    {
        result = default;
        var firstNormal = first.Normal.Normalized();
        var secondNormal = second.Normal.Normalized();
        if (Math.Abs(CadTransformMath.Dot(firstNormal, secondNormal)) < 1.0 - 1e-8)
            return false;

        var centerDelta = CadTransformMath.Between(first.Center, second.Center);
        var axial = CadTransformMath.Dot(centerDelta, firstNormal);
        if (Math.Abs(axial) > JoinTolerance)
            return false;

        var planar = new OcctVector3d(
            centerDelta.X - firstNormal.X * axial,
            centerDelta.Y - firstNormal.Y * axial,
            centerDelta.Z - firstNormal.Z * axial);
        var distanceSquared = planar.LengthSquared;
        if (distanceSquared <= JoinTolerance * JoinTolerance)
            return false;

        var distance = Math.Sqrt(distanceSquared);
        var firstRadius = first.Radius;
        var secondRadius = second.Radius;
        if (distance > firstRadius + secondRadius + JoinTolerance ||
            distance < Math.Abs(firstRadius - secondRadius) - JoinTolerance)
            return false;

        var direction = planar * (1.0 / distance);
        var along =
            (firstRadius * firstRadius -
             secondRadius * secondRadius +
             distanceSquared) /
            (2.0 * distance);
        var heightSquared =
            firstRadius * firstRadius - along * along;
        if (heightSquared < -JoinTolerance * JoinTolerance)
            return false;

        var height = Math.Sqrt(Math.Max(0.0, heightSquared));
        var basePoint = first.Center + direction * along;
        if (height <= JoinTolerance)
        {
            result = basePoint;
            return result.IsFinite;
        }

        var perpendicular = firstNormal.Cross(direction).Normalized();
        var firstCandidate = basePoint + perpendicular * height;
        var secondCandidate = basePoint - perpendicular * height;
        result = firstCandidate.DistanceTo(target) <= secondCandidate.DistanceTo(target)
            ? firstCandidate
            : secondCandidate;
        return result.IsFinite;
    }

    private OcctPoint3d ApproximateCenter()
    {
        var points = _segments
            .SelectMany(static segment =>
                segment switch
                {
                    CadLineEntity line => new[] { line.Start, line.End },
                    CadArcEntity arc => new[] { arc.Start, arc.Middle, arc.End },
                    _ => Array.Empty<OcctPoint3d>()
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

    internal CadPathEntity CopyWithSegments(
        IEnumerable<CadEntity> segments) =>
        CopyPropertiesTo(new CadPathEntity(segments));

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadPathEntity(_segments));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadPathEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _segments.Clear();
        _segments.AddRange(value._segments.Select(SnapshotSegment));
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        foreach (var segment in _segments)
            segment.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        foreach (var segment in _segments)
            segment.Rotate(center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        foreach (var segment in _segments)
            segment.Scale(center, factor);
        RaiseGeometryChanged(nameof(Scale));
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
                    CadLineEntity line =>
                        new JsonObject
                        {
                            ["type"] = "line",
                            ["geometry"] = CadLineEntity.WriteGeometry(line)
                        },
                    CadArcEntity arc =>
                        new JsonObject
                        {
                            ["type"] = "arc",
                            ["geometry"] = CadArcEntity.WriteGeometry(arc)
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
            throw new FormatException("Path segments are missing.");

        var segments = values
            .Select(node =>
            {
                var item = node as JsonObject ??
                    throw new FormatException("Path segment is invalid.");
                var type = item["type"]?.GetValue<string>() ??
                    throw new FormatException("Path segment type is missing.");
                var geometry = item["geometry"] as JsonObject ??
                    throw new FormatException("Path segment geometry is missing.");

                return type.ToLowerInvariant() switch
                {
                    "line" => CadLineEntity.ReadGeometry(geometry),
                    "arc" => CadArcEntity.ReadGeometry(geometry),
                    _ => throw new FormatException(
                        $"Unsupported path segment type '{type}'.")
                };
            })
            .Cast<CadEntity>()
            .ToArray();

        return new CadPathEntity(segments);
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
        CadEntity segment) =>
        segment switch
        {
            CadLineEntity line => line.End,
            CadArcEntity arc => arc.End,
            _ => throw new ArgumentException(
                "Unsupported path segment.",
                nameof(segment))
        };

    private readonly record struct PathVertexBinding(
        OcctPoint3d Position,
        int? PreviousSegmentIndex,
        int? NextSegmentIndex);

    private readonly record struct PathArcGripBinding(
        int SegmentIndex,
        OcctPoint3d Position);

    private static void ValidateChain(
        IReadOnlyList<CadEntity> segments)
    {
        if (segments.Count == 0)
            throw new ArgumentException(
                "Path requires at least one segment.",
                nameof(segments));

        for (var index = 1; index < segments.Count; index++)
        {
            if (SegmentEnd(segments[index - 1])
                    .DistanceTo(SegmentStart(segments[index])) >
                JoinTolerance)
            {
                throw new ArgumentException(
                    "Path segments must form one continuous ordered chain.",
                    nameof(segments));
            }
        }
    }
}
