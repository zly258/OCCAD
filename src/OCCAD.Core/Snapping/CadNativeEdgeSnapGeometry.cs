using OcctNet;

namespace OCCAD;

/// <summary>
/// Viewer-independent numerical helpers for BRep edge snapping. These use only
/// the stable edge evaluation/projection contract so CAD interaction is not
/// coupled to optional Viewer-side intersection/tangency helpers.
/// </summary>
internal static class CadNativeEdgeSnapGeometry
{
    private const int TangentSamples = 64;
    private const int IntersectionSamples = 32;
    private const double PlaneTolerance = 1e-6;
    private const double GeometryTolerance = 1e-8;

    internal static IReadOnlyList<OcctPoint3d> TangentPoints(
        OcctEngine engine,
        OcctShape edge,
        CadWorkPlane workPlane,
        OcctPoint3d reference)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!reference.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(reference));

        var samples = new TangentSample[TangentSamples + 1];
        for (var index = 0; index <= TangentSamples; index++)
        {
            var parameter = (double)index / TangentSamples;
            samples[index] = EvaluateTangentFunction(
                engine,
                edge,
                workPlane,
                reference,
                parameter);
        }

        var result = new List<OcctPoint3d>();

        for (var index = 0; index < TangentSamples; index++)
        {
            var left = samples[index];
            var right = samples[index + 1];

            if (left.IsCandidate)
                AddUnique(result, left.Evaluation.Point);

            if (!left.IsUsable || !right.IsUsable)
                continue;

            if (Math.Sign(left.Value) == Math.Sign(right.Value))
                continue;

            var root = RefineTangentRoot(
                engine,
                edge,
                workPlane,
                reference,
                left.Parameter,
                right.Parameter,
                left.Value,
                right.Value);

            if (root is { } point)
                AddUnique(result, point);
        }

        if (samples[^1].IsCandidate)
            AddUnique(result, samples[^1].Evaluation.Point);

        // A repeated root does not change sign. Catch those by refining local
        // minima of |f(t)| around sampled stations.
        for (var index = 1; index < TangentSamples; index++)
        {
            var previous = samples[index - 1];
            var current = samples[index];
            var next = samples[index + 1];
            if (!previous.IsUsable ||
                !current.IsUsable ||
                !next.IsUsable)
                continue;

            var magnitude = Math.Abs(current.Value);
            if (magnitude > Math.Abs(previous.Value) ||
                magnitude > Math.Abs(next.Value))
                continue;

            var root = RefineTangentMinimum(
                engine,
                edge,
                workPlane,
                reference,
                previous.Parameter,
                next.Parameter);

            if (root is { } point)
                AddUnique(result, point);
        }

        return result;
    }

    internal static IReadOnlyList<OcctPoint3d> Intersections(
        OcctEngine engine,
        OcctShape first,
        OcctShape second,
        double tolerance = 1e-7)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!double.IsFinite(tolerance) || tolerance <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));

        var firstSamples = SampleEdge(
            engine,
            first,
            IntersectionSamples);
        var secondSamples = SampleEdge(
            engine,
            second,
            IntersectionSamples);

        var result = new List<OcctPoint3d>();
        for (var firstIndex = 0;
             firstIndex < IntersectionSamples;
             firstIndex++)
        {
            var a0 = firstSamples[firstIndex];
            var a1 = firstSamples[firstIndex + 1];
            var firstLength = a0.DistanceTo(a1);

            for (var secondIndex = 0;
                 secondIndex < IntersectionSamples;
                 secondIndex++)
            {
                var b0 = secondSamples[secondIndex];
                var b1 = secondSamples[secondIndex + 1];
                var secondLength = b0.DistanceTo(b1);

                var seedTolerance = Math.Max(
                    tolerance * 10.0,
                    Math.Max(firstLength, secondLength) * 0.08);

                if (!BoundsOverlap(
                        a0,
                        a1,
                        b0,
                        b1,
                        seedTolerance))
                    continue;

                if (!TryClosestPointsOnSegments(
                        a0,
                        a1,
                        b0,
                        b1,
                        out var firstPoint,
                        out var secondPoint))
                    continue;

                if (firstPoint.DistanceTo(secondPoint) >
                    seedTolerance)
                    continue;

                var seed = Midpoint(
                    firstPoint,
                    secondPoint);

                if (TryRefineIntersection(
                        engine,
                        first,
                        second,
                        seed,
                        tolerance,
                        out var intersection))
                {
                    AddUnique(
                        result,
                        intersection,
                        Math.Max(tolerance * 10.0, 1e-8));
                }
            }
        }

        return result;
    }

    private static TangentSample EvaluateTangentFunction(
        OcctEngine engine,
        OcctShape edge,
        CadWorkPlane plane,
        OcctPoint3d reference,
        double parameter)
    {
        var evaluation =
            engine.EvaluateEdge(edge, parameter);

        var axial =
            Math.Abs(
                (evaluation.Point - plane.Origin)
                .Dot(plane.Normal));
        var tangentAxial =
            Math.Abs(
                evaluation.Tangent
                .Normalized()
                .Dot(plane.Normal));

        if (axial > PlaneTolerance ||
            tangentAxial > PlaneTolerance)
        {
            return new TangentSample(
                parameter,
                evaluation,
                0.0,
                IsUsable: false,
                IsCandidate: false);
        }

        var radial = evaluation.Point - reference;
        var radialLength = radial.Length;
        if (radialLength <= GeometryTolerance)
        {
            return new TangentSample(
                parameter,
                evaluation,
                0.0,
                IsUsable: true,
                IsCandidate: false);
        }

        var tangent = evaluation.Tangent.Normalized();
        var value = radial.Dot(tangent);
        var threshold =
            GeometryTolerance *
            Math.Max(1.0, radialLength);

        return new TangentSample(
            parameter,
            evaluation,
            value,
            IsUsable: true,
            IsCandidate:
                Math.Abs(value) <= threshold);
    }

    private static OcctPoint3d? RefineTangentRoot(
        OcctEngine engine,
        OcctShape edge,
        CadWorkPlane plane,
        OcctPoint3d reference,
        double left,
        double right,
        double leftValue,
        double rightValue)
    {
        for (var iteration = 0;
             iteration < 48;
             iteration++)
        {
            var middle = (left + right) * 0.5;
            var sample = EvaluateTangentFunction(
                engine,
                edge,
                plane,
                reference,
                middle);

            if (!sample.IsUsable)
                return null;

            if (sample.IsCandidate ||
                right - left <= 1e-12)
                return ValidateTangent(
                    sample,
                    reference)
                    ? sample.Evaluation.Point
                    : null;

            if (Math.Sign(leftValue) ==
                Math.Sign(sample.Value))
            {
                left = middle;
                leftValue = sample.Value;
            }
            else
            {
                right = middle;
                rightValue = sample.Value;
            }
        }

        var finalSample = EvaluateTangentFunction(
            engine,
            edge,
            plane,
            reference,
            (left + right) * 0.5);

        return ValidateTangent(
            finalSample,
            reference)
            ? finalSample.Evaluation.Point
            : null;
    }

    private static OcctPoint3d? RefineTangentMinimum(
        OcctEngine engine,
        OcctShape edge,
        CadWorkPlane plane,
        OcctPoint3d reference,
        double left,
        double right)
    {
        for (var iteration = 0;
             iteration < 32;
             iteration++)
        {
            var third = (right - left) / 3.0;
            var firstParameter = left + third;
            var secondParameter = right - third;

            var first = EvaluateTangentFunction(
                engine,
                edge,
                plane,
                reference,
                firstParameter);
            var second = EvaluateTangentFunction(
                engine,
                edge,
                plane,
                reference,
                secondParameter);

            if (!first.IsUsable || !second.IsUsable)
                return null;

            if (Math.Abs(first.Value) <=
                Math.Abs(second.Value))
                right = secondParameter;
            else
                left = firstParameter;
        }

        var sample = EvaluateTangentFunction(
            engine,
            edge,
            plane,
            reference,
            (left + right) * 0.5);

        return ValidateTangent(
            sample,
            reference)
            ? sample.Evaluation.Point
            : null;
    }

    private static bool ValidateTangent(
        TangentSample sample,
        OcctPoint3d reference)
    {
        if (!sample.IsUsable)
            return false;

        var radial =
            sample.Evaluation.Point -
            reference;
        var radialLength = radial.Length;
        if (radialLength <= GeometryTolerance)
            return false;

        var tangent =
            sample.Evaluation.Tangent
            .Normalized();

        return Math.Abs(
                   radial.Dot(tangent)) <=
               1e-7 *
               Math.Max(1.0, radialLength);
    }

    private static OcctPoint3d[] SampleEdge(
        OcctEngine engine,
        OcctShape edge,
        int segmentCount)
    {
        var result =
            new OcctPoint3d[segmentCount + 1];

        for (var index = 0;
             index <= segmentCount;
             index++)
        {
            result[index] =
                engine.EvaluateEdge(
                    edge,
                    (double)index /
                    segmentCount).Point;
        }

        return result;
    }

    private static bool TryRefineIntersection(
        OcctEngine engine,
        OcctShape first,
        OcctShape second,
        OcctPoint3d seed,
        double tolerance,
        out OcctPoint3d point)
    {
        var current = seed;

        for (var iteration = 0;
             iteration < 16;
             iteration++)
        {
            var onFirst =
                engine.ProjectPointToEdge(
                    first,
                    current);
            var onSecond =
                engine.ProjectPointToEdge(
                    second,
                    onFirst.Point);
            var backOnFirst =
                engine.ProjectPointToEdge(
                    first,
                    onSecond.Point);

            var distance =
                backOnFirst.Point
                    .DistanceTo(onSecond.Point);

            current = Midpoint(
                backOnFirst.Point,
                onSecond.Point);

            if (distance <= tolerance)
            {
                point = current;
                return true;
            }
        }

        var finalFirst =
            engine.ProjectPointToEdge(
                first,
                current);
        var finalSecond =
            engine.ProjectPointToEdge(
                second,
                finalFirst.Point);

        if (finalFirst.Point.DistanceTo(
                finalSecond.Point) <=
            tolerance * 5.0)
        {
            point = Midpoint(
                finalFirst.Point,
                finalSecond.Point);
            return true;
        }

        point = default;
        return false;
    }

    private static bool BoundsOverlap(
        OcctPoint3d a0,
        OcctPoint3d a1,
        OcctPoint3d b0,
        OcctPoint3d b1,
        double tolerance) =>
        Math.Max(a0.X, a1.X) + tolerance >=
            Math.Min(b0.X, b1.X) &&
        Math.Max(b0.X, b1.X) + tolerance >=
            Math.Min(a0.X, a1.X) &&
        Math.Max(a0.Y, a1.Y) + tolerance >=
            Math.Min(b0.Y, b1.Y) &&
        Math.Max(b0.Y, b1.Y) + tolerance >=
            Math.Min(a0.Y, a1.Y) &&
        Math.Max(a0.Z, a1.Z) + tolerance >=
            Math.Min(b0.Z, b1.Z) &&
        Math.Max(b0.Z, b1.Z) + tolerance >=
            Math.Min(a0.Z, a1.Z);

    private static bool TryClosestPointsOnSegments(
        OcctPoint3d p1,
        OcctPoint3d q1,
        OcctPoint3d p2,
        OcctPoint3d q2,
        out OcctPoint3d first,
        out OcctPoint3d second)
    {
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r = p1 - p2;
        var a = d1.Dot(d1);
        var e = d2.Dot(d2);
        var f = d2.Dot(r);

        double s;
        double t;

        if (a <= GeometryTolerance &&
            e <= GeometryTolerance)
        {
            first = p1;
            second = p2;
            return true;
        }

        if (a <= GeometryTolerance)
        {
            s = 0.0;
            t = Math.Clamp(
                f / e,
                0.0,
                1.0);
        }
        else
        {
            var c = d1.Dot(r);
            if (e <= GeometryTolerance)
            {
                t = 0.0;
                s = Math.Clamp(
                    -c / a,
                    0.0,
                    1.0);
            }
            else
            {
                var b = d1.Dot(d2);
                var denominator =
                    a * e - b * b;

                s = Math.Abs(denominator) >
                    GeometryTolerance
                    ? Math.Clamp(
                        (b * f - c * e) /
                        denominator,
                        0.0,
                        1.0)
                    : 0.0;

                t = (b * s + f) / e;

                if (t < 0.0)
                {
                    t = 0.0;
                    s = Math.Clamp(
                        -c / a,
                        0.0,
                        1.0);
                }
                else if (t > 1.0)
                {
                    t = 1.0;
                    s = Math.Clamp(
                        (b - c) / a,
                        0.0,
                        1.0);
                }
            }
        }

        first = p1 + d1 * s;
        second = p2 + d2 * t;
        return first.IsFinite &&
               second.IsFinite;
    }

    private static OcctPoint3d Midpoint(
        OcctPoint3d first,
        OcctPoint3d second) =>
        new(
            (first.X + second.X) * 0.5,
            (first.Y + second.Y) * 0.5,
            (first.Z + second.Z) * 0.5);

    private static void AddUnique(
        ICollection<OcctPoint3d> values,
        OcctPoint3d candidate,
        double tolerance = 1e-7)
    {
        foreach (var existing in values)
        {
            if (existing.DistanceTo(candidate) <=
                tolerance)
                return;
        }

        values.Add(candidate);
    }

    private readonly record struct TangentSample(
        double Parameter,
        OcctEdgeEvaluation Evaluation,
        double Value,
        bool IsUsable,
        bool IsCandidate);
}
