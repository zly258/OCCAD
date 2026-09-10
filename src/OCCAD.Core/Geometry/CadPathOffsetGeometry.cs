using OcctNet;

namespace OCCAD;

internal static class CadPathOffsetGeometry
{
    private const double Tolerance = 1e-7;

    internal static bool TryOffset(
        CadPathEntity source,
        double signedDistance,
        out CadPathEntity result)
    {
        ArgumentNullException.ThrowIfNull(source);
        result = null!;

        if (!double.IsFinite(signedDistance) ||
            Math.Abs(signedDistance) <= Tolerance)
            return false;

        try
        {
            using var model = new OcctModelingSession();
            var inputEdges = new List<OcctModelShape>(
                source.Segments.Count);

            foreach (var segment in source.Segments)
            {
                inputEdges.Add(
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

            var inputWire =
                model.MakeWire(inputEdges);
            var offsetShape =
                model.OffsetWire(
                    inputWire,
                    signedDistance,
                    openResult: !source.Closed);

            OcctModelShape outputWire;
            var outputType =
                model.GetShapeType(offsetShape);
            if (outputType == OcctShapeType.Wire)
            {
                outputWire = offsetShape;
            }
            else
            {
                var wires =
                    model.GetWires(offsetShape);
                if (wires.Count != 1)
                    return false;
                outputWire = wires[0];
            }

            var output = new List<CadEntity>();
            foreach (var edge in model.GetWireEdges(outputWire))
            {
                if (!TryDecodeEdge(
                        model,
                        edge,
                        output))
                    return false;
            }

            if (output.Count == 0)
                return false;

            result =
                source.CopyWithSegments(output);
            return result.Closed == source.Closed;
        }
        catch (Exception exception)
            when (IsRecoverable(exception))
        {
            result = null!;
            return false;
        }
    }

    private static bool TryDecodeEdge(
        OcctModelingSession model,
        OcctModelShape edge,
        List<CadEntity> output)
    {
        var reversed =
            model.GetShapeOrientation(edge) ==
            OcctModelOrientation.Reversed;

        switch (model.GetEdgeCurveType(edge))
        {
            case OcctCurveType.Line:
            {
                var line =
                    model.GetLineGeometry(edge);
                var start =
                    PointAt(
                        line,
                        reversed
                            ? line.LastParameter
                            : line.FirstParameter);
                var end =
                    PointAt(
                        line,
                        reversed
                            ? line.FirstParameter
                            : line.LastParameter);
                if (start.DistanceTo(end) <= Tolerance)
                    return false;

                output.Add(
                    new CadLineEntity(
                        start,
                        end));
                return true;
            }

            case OcctCurveType.Circle:
            {
                var circle =
                    model.GetCircleGeometry(edge);
                var span =
                    circle.LastParameter -
                    circle.FirstParameter;
                if (!double.IsFinite(span) ||
                    span <= Tolerance ||
                    span > Math.PI * 2.0 + Tolerance)
                    return false;

                var startParameter =
                    reversed
                        ? circle.LastParameter
                        : circle.FirstParameter;
                var signedSpan =
                    reversed
                        ? -span
                        : span;

                if (span >=
                    Math.PI * 2.0 -
                    Tolerance)
                {
                    var middleParameter =
                        startParameter +
                        signedSpan * 0.5;
                    AddArc(
                        output,
                        circle,
                        startParameter,
                        startParameter +
                            signedSpan * 0.25,
                        middleParameter);
                    AddArc(
                        output,
                        circle,
                        middleParameter,
                        middleParameter +
                            signedSpan * 0.25,
                        startParameter +
                            signedSpan);
                    return true;
                }

                AddArc(
                    output,
                    circle,
                    startParameter,
                    startParameter +
                        signedSpan * 0.5,
                    startParameter +
                        signedSpan);
                return true;
            }

            default:
                return false;
        }
    }

    private static void AddArc(
        List<CadEntity> output,
        OcctCircleGeometry circle,
        double startParameter,
        double middleParameter,
        double endParameter)
    {
        output.Add(
            new CadArcEntity(
                PointAt(circle, startParameter),
                PointAt(circle, middleParameter),
                PointAt(circle, endParameter)));
    }

    private static OcctPoint3d PointAt(
        OcctLineGeometry line,
        double parameter) =>
        line.Origin +
        line.Direction * parameter;

    private static OcctPoint3d PointAt(
        OcctCircleGeometry circle,
        double parameter)
    {
        var xAxis =
            circle.XDirection.Normalized();
        var yAxis =
            circle.Normal
                .Normalized()
                .Cross(xAxis)
                .Normalized();

        return circle.Center +
               xAxis *
                   (Math.Cos(parameter) *
                    circle.Radius) +
               yAxis *
                   (Math.Sin(parameter) *
                    circle.Radius);
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
