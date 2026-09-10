using OcctNet;

namespace OCCAD;

public static class CadSubshapeReferenceResolver
{
    private const double MeasureTolerance = 1e-7;
    private const double AnchorTolerance = 1e-6;

    public static CadSubshapeReference Capture(
        OcctEngine engine,
        CadSubobjectSelection selection)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!selection.IsValid)
            throw new ArgumentException(
                "Subobject selection is invalid.",
                nameof(selection));

        var fallback = CaptureFallback(
            engine,
            selection.Entity,
            selection.SubshapeType,
            selection.SubshapeIndex);

        return CadSubshapeReference.Create(
            selection.Entity,
            selection.SubshapeType,
            selection.SubshapeIndex,
            fallback);
    }

    public static bool TryResolve(
        OcctEngine engine,
        CadEntity entity,
        CadSubshapeReference reference,
        out int index)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(entity);

        index = -1;
        if (reference.EntityId != entity.Id ||
            entity.ViewerShape is not { } owner ||
            !engine.ContainsObject(owner.Id))
        {
            return false;
        }

        var count = engine.GetTopologyCount(
            owner,
            reference.ShapeType);
        if (count <= 0)
            return false;

        if (reference.Fallback is null)
        {
            if (reference.Index >= count)
                return false;

            index = reference.Index;
            return true;
        }

        var expected = reference.Fallback.Value;

        if (reference.Index < count &&
            TryCaptureFallback(
                engine,
                entity,
                reference.ShapeType,
                reference.Index,
                out var current) &&
            Matches(expected, current))
        {
            index = reference.Index;
            return true;
        }

        var bestIndex = -1;
        var bestScore = double.PositiveInfinity;

        for (var candidateIndex = 0;
             candidateIndex < count;
             candidateIndex++)
        {
            if (!TryCaptureFallback(
                    engine,
                    entity,
                    reference.ShapeType,
                    candidateIndex,
                    out var candidate))
            {
                continue;
            }

            if (!string.Equals(
                    expected.GeometryKind,
                    candidate.GeometryKind,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var score = Score(
                expected,
                candidate);

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestIndex = candidateIndex;
        }

        if (bestIndex < 0 ||
            bestScore > 1.0)
        {
            return false;
        }

        index = bestIndex;
        return true;
    }

    public static CadSubshapeReference Resolve(
        OcctEngine engine,
        CadEntity entity,
        CadSubshapeReference reference)
    {
        if (!TryResolve(
                engine,
                entity,
                reference,
                out var index))
        {
            throw new InvalidOperationException(
                "Subshape reference cannot be resolved on the current entity topology.");
        }

        return new CadSubshapeReference(
            entity.Id,
            reference.ShapeType,
            index,
            reference.Fallback);
    }

    private static CadSubshapeFallback CaptureFallback(
        OcctEngine engine,
        CadEntity entity,
        OcctShapeType shapeType,
        int index)
    {
        if (!TryCaptureFallback(
                engine,
                entity,
                shapeType,
                index,
                out var fallback))
        {
            throw new InvalidOperationException(
                "Subshape geometry signature cannot be captured.");
        }

        return fallback;
    }

    private static bool TryCaptureFallback(
        OcctEngine engine,
        CadEntity entity,
        OcctShapeType shapeType,
        int index,
        out CadSubshapeFallback fallback)
    {
        fallback = default;

        var owner = entity.ViewerShape;
        if (owner is null ||
            !engine.ContainsObject(owner.Id) ||
            index < 0)
        {
            return false;
        }

        try
        {
            switch (shapeType)
            {
                case OcctShapeType.Vertex:
                {
                    if (index >= engine.GetTopologyCount(
                            owner,
                            OcctShapeType.Vertex))
                    {
                        return false;
                    }

                    var point = engine.GetVertexPoint(
                        owner,
                        index);

                    fallback = new CadSubshapeFallback(
                        "Vertex",
                        entity.ToLocalPoint(point),
                        0.0);
                    return true;
                }

                case OcctShapeType.Edge:
                    return TryCaptureEdge(
                        engine,
                        entity,
                        owner,
                        index,
                        out fallback);

                case OcctShapeType.Face:
                    return TryCaptureFace(
                        engine,
                        entity,
                        owner,
                        index,
                        out fallback);

                default:
                    return false;
            }
        }
        catch (Exception exception)
            when (IsRecoverable(exception))
        {
            fallback = default;
            return false;
        }
    }

    private static bool TryCaptureEdge(
        OcctEngine engine,
        CadEntity entity,
        OcctShape owner,
        int index,
        out CadSubshapeFallback fallback)
    {
        fallback = default;
        if (index >= engine.GetTopologyCount(
                owner,
                OcctShapeType.Edge))
        {
            return false;
        }

        var temporary = engine.GetSubshapeAt(
            owner,
            OcctShapeType.Edge,
            index);

        try
        {
            var curveType =
                engine.GetEdgeCurveType(temporary);
            var properties =
                engine.GetShapeLinearProperties(
                    temporary);

            fallback = new CadSubshapeFallback(
                curveType.ToString(),
                entity.ToLocalPoint(
                    properties.CenterOfMass),
                Math.Max(0.0, properties.Mass));
            return true;
        }
        finally
        {
            DeleteTemporary(
                engine,
                temporary);
        }
    }

    private static bool TryCaptureFace(
        OcctEngine engine,
        CadEntity entity,
        OcctShape owner,
        int index,
        out CadSubshapeFallback fallback)
    {
        fallback = default;
        if (index >= engine.GetTopologyCount(
                owner,
                OcctShapeType.Face))
        {
            return false;
        }

        var temporary = engine.GetSubshapeAt(
            owner,
            OcctShapeType.Face,
            index);

        try
        {
            var surfaceType =
                engine.GetFaceSurfaceType(temporary);
            var properties =
                engine.GetShapeSurfaceProperties(
                    temporary);

            fallback = new CadSubshapeFallback(
                surfaceType.ToString(),
                entity.ToLocalPoint(
                    properties.CenterOfMass),
                Math.Max(0.0, properties.Mass));
            return true;
        }
        finally
        {
            DeleteTemporary(
                engine,
                temporary);
        }
    }

    private static bool Matches(
        CadSubshapeFallback expected,
        CadSubshapeFallback candidate) =>
        string.Equals(
            expected.GeometryKind,
            candidate.GeometryKind,
            StringComparison.OrdinalIgnoreCase) &&
        Score(expected, candidate) <= 1.0;

    private static double Score(
        CadSubshapeFallback expected,
        CadSubshapeFallback candidate)
    {
        var measureScale =
            Math.Max(
                1.0,
                Math.Abs(expected.Measure));
        var anchorScale =
            Math.Max(
                1.0,
                Math.Sqrt(measureScale));

        var measureError =
            Math.Abs(
                candidate.Measure -
                expected.Measure) /
            (MeasureTolerance * measureScale);

        var anchorError =
            candidate.Anchor
                .DistanceTo(expected.Anchor) /
            (AnchorTolerance * anchorScale);

        return Math.Max(
            measureError,
            anchorError);
    }

    private static void DeleteTemporary(
        OcctEngine engine,
        OcctShape shape)
    {
        try
        {
            if (engine.ContainsObject(shape.Id))
                engine.Delete(shape);
        }
        catch (Exception exception)
            when (IsRecoverable(exception))
        {
        }
    }

    private static bool IsRecoverable(
        Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
