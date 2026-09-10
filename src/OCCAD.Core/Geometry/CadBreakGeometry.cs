using OcctNet;

namespace OCCAD;

internal static class CadBreakGeometry
{
    private const double Tolerance = 1e-9;

    internal static bool TryBreak(
        CadEntity entity,
        OcctPoint3d firstPoint,
        OcctPoint3d secondPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];
        var working =
            entity.CreateWorldGeometrySnapshot();
        if (!CadPlanarCurveIntersections.IsBoundaryOnPlane(
                working,
                plane))
            return false;

        return working switch
        {
            CadLineEntity line =>
                TryBreakLine(
                    line,
                    firstPoint,
                    secondPoint,
                    plane,
                    out replacements),
            CadArcEntity arc =>
                TryBreakArc(
                    arc,
                    firstPoint,
                    secondPoint,
                    out replacements),
            CadCircleEntity circle =>
                TryBreakCircle(
                    circle,
                    firstPoint,
                    secondPoint,
                    plane,
                    out replacements),
            CadPolylineEntity polyline =>
                TryBreakPolyline(
                    polyline,
                    firstPoint,
                    secondPoint,
                    plane,
                    out replacements),
            CadPathEntity path =>
                TryBreakPath(
                    path,
                    firstPoint,
                    secondPoint,
                    plane,
                    out replacements),
            _ => false
        };
    }

    private static bool TryBreakLine(
        CadLineEntity line,
        OcctPoint3d firstPoint,
        OcctPoint3d secondPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];
        var first = ParameterOnLine(
            line,
            firstPoint,
            plane);
        var second = ParameterOnLine(
            line,
            secondPoint,
            plane);
        var low = Math.Min(first, second);
        var high = Math.Max(first, second);

        if (high - low <= Tolerance)
            return false;

        var result = new List<CadEntity>(2);
        if (low > Tolerance)
            result.Add(
                CreateLinePart(
                    line,
                    0.0,
                    low));
        if (high < 1.0 - Tolerance)
            result.Add(
                CreateLinePart(
                    line,
                    high,
                    1.0));

        replacements = result.ToArray();
        return true;
    }

    private static bool TryBreakArc(
        CadArcEntity arc,
        OcctPoint3d firstPoint,
        OcctPoint3d secondPoint,
        out CadEntity[] replacements)
    {
        replacements = [];
        if (!arc.TryClosestParameter(
                firstPoint,
                out var first) ||
            !arc.TryClosestParameter(
                secondPoint,
                out var second))
            return false;

        var low = Math.Min(first, second);
        var high = Math.Max(first, second);
        if (high - low <= Tolerance)
            return false;

        var result = new List<CadEntity>(2);
        if (low > Tolerance)
            result.Add(
                arc.CopyWithParameterRange(
                    0.0,
                    low));
        if (high < 1.0 - Tolerance)
            result.Add(
                arc.CopyWithParameterRange(
                    high,
                    1.0));

        replacements = result.ToArray();
        return true;
    }

    private static bool TryBreakCircle(
        CadCircleEntity circle,
        OcctPoint3d firstPoint,
        OcctPoint3d secondPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];

        var first = Angle(
            circle.Center,
            firstPoint,
            plane);
        var second = Angle(
            circle.Center,
            secondPoint,
            plane);

        var removeSweep = second - first;
        if (removeSweep < 0.0)
            removeSweep += Math.PI * 2.0;
        if (removeSweep <= Tolerance ||
            removeSweep >= Math.PI * 2.0 - Tolerance)
            return false;

        var keepSweep =
            Math.PI * 2.0 -
            removeSweep;
        if (keepSweep <= Tolerance)
            return false;

        var keepStart = second;
        var start =
            circle.PointAtAngle(keepStart);
        var middle =
            circle.PointAtAngle(
                keepStart +
                keepSweep * 0.5);
        var end =
            circle.PointAtAngle(
                keepStart +
                keepSweep);

        replacements =
        [
            circle.CreateArc(
                start,
                middle,
                end)
        ];
        return true;
    }

    private static bool TryBreakPolyline(
        CadPolylineEntity polyline,
        OcctPoint3d firstPoint,
        OcctPoint3d secondPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];

        if (!CadPolylinePathGeometry.TryClosestPosition(
                polyline,
                firstPoint,
                plane,
                out var first) ||
            !CadPolylinePathGeometry.TryClosestPosition(
                polyline,
                secondPoint,
                plane,
                out var second))
            return false;

        if (!polyline.Closed)
        {
            var low = first.Value <= second.Value
                ? first
                : second;
            var high = first.Value <= second.Value
                ? second
                : first;

            if (high.Value - low.Value <= Tolerance)
                return false;

            var result = new List<CadEntity>(2);

            var start = new CadPolylinePathPosition(
                0,
                0.0);
            var end = new CadPolylinePathPosition(
                CadPolylinePathGeometry.SegmentCount(polyline) - 1,
                1.0);

            var left = CadPolylinePathGeometry.BuildForwardPath(
                polyline,
                start,
                low,
                allowWrap: false);
            if (left.Count >= 2)
                result.Add(
                    polyline.CopyWithPoints(
                        left,
                        closed: false));

            var right = CadPolylinePathGeometry.BuildForwardPath(
                polyline,
                high,
                end,
                allowWrap: false);
            if (right.Count >= 2)
                result.Add(
                    polyline.CopyWithPoints(
                        right,
                        closed: false));

            replacements = result.ToArray();
            return true;
        }

        var segmentCount =
            CadPolylinePathGeometry.SegmentCount(polyline);
        var forward = second.Value - first.Value;
        if (forward <= Tolerance)
            forward += segmentCount;
        if (forward <= Tolerance ||
            forward >= segmentCount - Tolerance)
            return false;

        var keep = CadPolylinePathGeometry.BuildForwardPath(
            polyline,
            second,
            first,
            allowWrap: true);
        if (keep.Count < 2)
            return false;

        replacements =
        [
            polyline.CopyWithPoints(
                keep,
                closed: false)
        ];
        return true;
    }

    private static bool TryBreakPath(
        CadPathEntity path,
        OcctPoint3d firstPoint,
        OcctPoint3d secondPoint,
        CadWorkPlane plane,
        out CadEntity[] replacements)
    {
        replacements = [];

        if (!CadPathCurveGeometry.TryClosestPosition(
                path,
                firstPoint,
                plane,
                out var first) ||
            !CadPathCurveGeometry.TryClosestPosition(
                path,
                secondPoint,
                plane,
                out var second))
            return false;

        if (!path.Closed)
        {
            var low =
                first.Value <= second.Value
                    ? first
                    : second;
            var high =
                first.Value <= second.Value
                    ? second
                    : first;

            if (high.Value -
                low.Value <=
                Tolerance)
                return false;

            var result = new List<CadEntity>(2);
            var start =
                new CadPathPosition(0, 0.0);
            var end =
                new CadPathPosition(
                    path.Segments.Count - 1,
                    1.0);

            var left =
                CadPathCurveGeometry.BuildForwardSegments(
                    path,
                    start,
                    low,
                    allowWrap: false);
            if (left.Count > 0)
                result.Add(
                    path.CopyWithSegments(left));

            var right =
                CadPathCurveGeometry.BuildForwardSegments(
                    path,
                    high,
                    end,
                    allowWrap: false);
            if (right.Count > 0)
                result.Add(
                    path.CopyWithSegments(right));

            replacements = result.ToArray();
            return true;
        }

        var segmentCount =
            path.Segments.Count;
        var forward =
            second.Value -
            first.Value;
        if (forward <= Tolerance)
            forward += segmentCount;
        if (forward <= Tolerance ||
            forward >=
                segmentCount -
                Tolerance)
            return false;

        var keep =
            CadPathCurveGeometry.BuildForwardSegments(
                path,
                second,
                first,
                allowWrap: true);
        if (keep.Count == 0)
            return false;

        replacements =
        [
            path.CopyWithSegments(keep)
        ];
        return true;
    }

    private static double ParameterOnLine(
        CadLineEntity line,
        OcctPoint3d point,
        CadWorkPlane plane)
    {
        var start =
            plane.WorldToLocal(line.Start);
        var end =
            plane.WorldToLocal(line.End);
        var value =
            plane.WorldToLocal(point);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared =
            dx * dx + dy * dy;
        if (lengthSquared <= Tolerance * Tolerance)
            return 0.0;

        return Math.Clamp(
            ((value.X - start.X) * dx +
             (value.Y - start.Y) * dy) /
            lengthSquared,
            0.0,
            1.0);
    }

    private static CadLineEntity CreateLinePart(
        CadLineEntity line,
        double startParameter,
        double endParameter)
    {
        var copy =
            (CadLineEntity)line.Duplicate();
        copy.MoveGrip(
            0,
            PointAt(
                line,
                startParameter));
        copy.MoveGrip(
            2,
            PointAt(
                line,
                endParameter));
        return copy;
    }

    private static OcctPoint3d PointAt(
        CadLineEntity line,
        double parameter) =>
        line.Start +
        CadTransformMath.Between(
            line.Start,
            line.End) * parameter;

    private static double Angle(
        OcctPoint3d center,
        OcctPoint3d point,
        CadWorkPlane plane)
    {
        var c = plane.WorldToLocal(center);
        var p = plane.WorldToLocal(point);
        var value =
            Math.Atan2(
                p.Y - c.Y,
                p.X - c.X);
        return value < 0.0
            ? value + Math.PI * 2.0
            : value;
    }
}
