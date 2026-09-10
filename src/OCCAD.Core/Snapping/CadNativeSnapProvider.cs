using OcctNet;

namespace OCCAD;

/// <summary>
/// Exact OCCT topology snap provider. This class owns native subshape queries
/// and temporary edge copies; CadSnapManager remains responsible for candidate
/// ranking, hysteresis, cycling and active snap state.
/// </summary>
internal sealed class CadNativeSnapProvider
{
    private readonly CadDocument _document;

    public CadNativeSnapProvider(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public IReadOnlyList<CadSnapPoint> GetCandidates(
        OcctEngine engine,
        int x,
        int y,
        OcctPoint3d queryPoint,
        CadWorkPlane workPlane,
        OcctPoint3d? reference,
        CadSnapType effectiveModes)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(workPlane);

        var wantsVertex =
            (effectiveModes &
             (CadSnapType.Endpoint |
              CadSnapType.Vertex)) != 0;
        var wantsEdge =
            (effectiveModes &
             (CadSnapType.Endpoint |
              CadSnapType.Midpoint |
              CadSnapType.Center |
              CadSnapType.Nearest |
              CadSnapType.Intersection |
              CadSnapType.Perpendicular |
              CadSnapType.Tangent)) != 0;

        if (!wantsVertex && !wantsEdge)
            return Array.Empty<CadSnapPoint>();

        var shapeTypes = new List<OcctShapeType>(2);
        if (wantsVertex)
            shapeTypes.Add(OcctShapeType.Vertex);
        if (wantsEdge)
            shapeTypes.Add(OcctShapeType.Edge);

        var hits = engine.DetectAt(
            x,
            y,
            new OcctDetectionFilter
            {
                ShapeTypes = shapeTypes,
                IncludeWholeObjects = false
            },
            maxHits: 32);

        if (hits.Count == 0)
            return Array.Empty<CadSnapPoint>();

        var result = new List<CadSnapPoint>();
        var edgeHits = new List<NativeEdgeHit>();
        foreach (var hit in hits)
        {
            var entity = _document.FindByViewerObject(hit.Owner);
            if (entity is null ||
                !_document.IsEntitySelectable(entity))
                continue;

            if (hit.SubshapeType == OcctShapeType.Vertex &&
                wantsVertex)
            {
                var type =
                    (effectiveModes & CadSnapType.Endpoint) != 0
                        ? CadSnapType.Endpoint
                        : CadSnapType.Vertex;

                result.Add(
                    new CadSnapPoint(
                        entity,
                        hit.Point,
                        type,
                        hit.SubshapeIndex));
                continue;
            }

            if (hit.SubshapeType != OcctShapeType.Edge ||
                !wantsEdge ||
                hit.SubshapeIndex < 0 ||
                hit.Owner is not OcctShape owner)
                continue;

            edgeHits.Add(
                new NativeEdgeHit(
                    owner,
                    entity,
                    hit.SubshapeIndex));

            AddExactEdgeCandidates(
                result,
                engine,
                owner,
                entity,
                hit.SubshapeIndex,
                queryPoint,
                workPlane,
                reference,
                effectiveModes);
        }

        if ((effectiveModes & CadSnapType.Intersection) != 0 &&
            edgeHits.Count > 1)
        {
            AddExactEdgeIntersections(
                result,
                engine,
                edgeHits);
        }

        return result;
    }

    private static void AddExactEdgeCandidates(
        List<CadSnapPoint> result,
        OcctEngine engine,
        OcctShape owner,
        CadEntity entity,
        int edgeIndex,
        OcctPoint3d queryPoint,
        CadWorkPlane workPlane,
        OcctPoint3d? reference,
        CadSnapType modes)
    {
        if ((modes & CadSnapType.Endpoint) != 0)
        {
            var endpoints = engine.GetEdgeEndpoints(owner, edgeIndex);
            AddUnique(
                result,
                new CadSnapPoint(
                    entity,
                    endpoints.Start,
                    CadSnapType.Endpoint,
                    edgeIndex));
            AddUnique(
                result,
                new CadSnapPoint(
                    entity,
                    endpoints.End,
                    CadSnapType.Endpoint,
                    edgeIndex));
        }

        if ((modes & CadSnapType.Midpoint) != 0)
        {
            var midpoint = engine.EvaluateEdge(owner, edgeIndex, 0.5);
            AddUnique(
                result,
                new CadSnapPoint(
                    entity,
                    midpoint.Point,
                    CadSnapType.Midpoint,
                    edgeIndex));
        }

        var needsCopiedEdge =
            (modes &
             (CadSnapType.Center |
              CadSnapType.Nearest |
              CadSnapType.Perpendicular |
              CadSnapType.Tangent)) != 0;
        if (!needsCopiedEdge)
            return;

        var edge = engine.GetSubshapeAt(
            owner,
            OcctShapeType.Edge,
            edgeIndex);
        try
        {
            if ((modes & CadSnapType.Nearest) != 0)
            {
                var projection =
                    engine.ProjectPointToEdge(edge, queryPoint);
                AddUnique(
                    result,
                    new CadSnapPoint(
                        entity,
                        projection.Point,
                        CadSnapType.Nearest,
                        edgeIndex));
            }

            if ((modes & CadSnapType.Perpendicular) != 0 &&
                reference is { } referencePoint)
            {
                var projection =
                    engine.ProjectPointToEdge(edge, referencePoint);
                var delta = projection.Point - referencePoint;
                var tangent = projection.Tangent;
                var scale = Math.Max(
                    1.0,
                    Math.Sqrt(delta.LengthSquared));
                var perpendicularError =
                    Math.Abs(delta.Dot(tangent));

                if (perpendicularError <= 1e-7 * scale)
                {
                    AddUnique(
                        result,
                        new CadSnapPoint(
                            entity,
                            projection.Point,
                            CadSnapType.Perpendicular,
                            edgeIndex));
                }
            }

            if ((modes & CadSnapType.Tangent) != 0 &&
                reference is { } tangentReference)
            {
                foreach (var tangent in
                         CadNativeEdgeSnapGeometry.TangentPoints(
                             engine,
                             edge,
                             workPlane,
                             tangentReference))
                {
                    AddUnique(
                        result,
                        new CadSnapPoint(
                            entity,
                            tangent,
                            CadSnapType.Tangent,
                            edgeIndex));
                }
            }

            if ((modes & CadSnapType.Center) != 0 &&
                engine.GetEdgeCurveType(edge) == OcctCurveType.Circle &&
                TryGetCircularEdgeFrame(
                    engine,
                    owner,
                    edgeIndex,
                    out var circle))
            {
                AddUnique(
                    result,
                    new CadSnapPoint(
                        entity,
                        circle.Center,
                        CadSnapType.Center,
                        edgeIndex));
            }
        }
        finally
        {
            TryDeleteTemporaryShape(engine, edge);
        }
    }

    private static void AddExactEdgeIntersections(
        List<CadSnapPoint> result,
        OcctEngine engine,
        IReadOnlyList<NativeEdgeHit> edgeHits)
    {
        var unique = edgeHits
            .GroupBy(
                static hit =>
                    (hit.Owner.Id, hit.EdgeIndex))
            .Select(static group => group.First())
            .ToArray();
        if (unique.Length < 2)
            return;

        var copies = new OcctShape[unique.Length];
        try
        {
            for (var index = 0; index < unique.Length; index++)
            {
                copies[index] = engine.GetSubshapeAt(
                    unique[index].Owner,
                    OcctShapeType.Edge,
                    unique[index].EdgeIndex);
            }

            for (var firstIndex = 0;
                 firstIndex < unique.Length;
                 firstIndex++)
            {
                for (var secondIndex = firstIndex + 1;
                     secondIndex < unique.Length;
                     secondIndex++)
                {
                    foreach (var intersection in
                             CadNativeEdgeSnapGeometry.Intersections(
                                 engine,
                                 copies[firstIndex],
                                 copies[secondIndex]))
                    {
                        AddUnique(
                            result,
                            new CadSnapPoint(
                                unique[firstIndex].Entity,
                                intersection,
                                CadSnapType.Intersection,
                                unique[firstIndex].EdgeIndex));
                    }
                }
            }
        }
        finally
        {
            foreach (var copy in copies)
            {
                if (copy.Id > 0)
                    TryDeleteTemporaryShape(engine, copy);
            }
        }
    }

    private static bool TryGetCircularEdgeFrame(
        OcctEngine engine,
        OcctShape owner,
        int edgeIndex,
        out CircularEdgeFrame result)
    {
        result = default;

        var first = engine.EvaluateEdge(owner, edgeIndex, 0.0).Point;
        var second = engine.EvaluateEdge(owner, edgeIndex, 1.0 / 3.0).Point;
        var third = engine.EvaluateEdge(owner, edgeIndex, 2.0 / 3.0).Point;

        var a = second - first;
        var b = third - first;
        var normalRaw = a.Cross(b);
        var normalSquared = normalRaw.LengthSquared;
        if (normalSquared <= 1e-24)
            return false;

        var aSquared = a.LengthSquared;
        var bSquared = b.LengthSquared;
        var offset =
            (b.Cross(normalRaw) * aSquared +
             normalRaw.Cross(a) * bSquared) *
            (1.0 / (2.0 * normalSquared));
        var center = first + offset;
        var radius = center.DistanceTo(first);
        if (!center.IsFinite ||
            !double.IsFinite(radius) ||
            radius <= 1e-12 ||
            !normalRaw.TryNormalize(out var normal))
            return false;

        var axes = CadCircleEntity.PlaneAxes(normal);
        result = new CircularEdgeFrame(
            center,
            normal,
            axes.XAxis,
            axes.YAxis,
            radius);
        return true;
    }

    private static void AddUnique(
        List<CadSnapPoint> result,
        CadSnapPoint candidate)
    {
        if (!candidate.Position.IsFinite)
            return;

        if (result.Any(existing =>
                existing.Type == candidate.Type &&
                existing.Position.DistanceTo(candidate.Position) <= 1e-8))
            return;

        result.Add(candidate);
    }

    private static void TryDeleteTemporaryShape(
        OcctEngine engine,
        OcctShape shape)
    {
        try
        {
            if (engine.ContainsObject(shape.Id))
                engine.Delete(shape);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private readonly record struct NativeEdgeHit(
        OcctShape Owner,
        CadEntity Entity,
        int EdgeIndex);

    private readonly record struct CircularEdgeFrame(
        OcctPoint3d Center,
        OcctVector3d Normal,
        OcctVector3d XAxis,
        OcctVector3d YAxis,
        double Radius);
}
