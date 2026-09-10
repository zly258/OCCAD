using OcctNet;

namespace OCCAD;

internal enum CadSnapCurveKind
{
    Segment,
    Circle,
    Arc,
    Ellipse
}

internal readonly record struct CadSnapCurve(
    CadEntity Entity,
    int Index,
    CadSnapCurveKind Kind,
    CadPlanePoint Start,
    CadPlanePoint End,
    CadPlanePoint Center,
    double Radius,
    double StartAngle,
    double SweepAngle,
    double SecondaryRadius = 0.0,
    double AxisAngle = 0.0)
{
    public bool ContainsAngle(double angle, double tolerance = 1e-9)
    {
        if (Kind != CadSnapCurveKind.Arc) return true;
        var relative = CadPrecisionSnapGeometry.NormalizePositive(angle - StartAngle);
        if (SweepAngle >= 0.0)
            return relative <= SweepAngle + tolerance;

        var clockwise = CadPrecisionSnapGeometry.NormalizePositive(StartAngle - angle);
        return clockwise <= -SweepAngle + tolerance;
    }

    public bool ContainsPoint(CadPlanePoint point, double tolerance = 1e-8)
    {
        if (Kind == CadSnapCurveKind.Segment)
            return CadPrecisionSnapGeometry.PointOnSegment(point, Start, End, tolerance);

        if (Kind == CadSnapCurveKind.Ellipse)
        {
            var local = CadPrecisionSnapGeometry.ToEllipseLocal(this, point);
            var value =
                local.X * local.X / (Radius * Radius) +
                local.Y * local.Y / (SecondaryRadius * SecondaryRadius);
            return Math.Abs(value - 1.0) <= Math.Max(1e-10, tolerance * 10.0);
        }

        var dx = point.X - Center.X;
        var dy = point.Y - Center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (Math.Abs(distance - Radius) > tolerance) return false;
        if (Kind != CadSnapCurveKind.Arc) return true;
        return ContainsAngle(Math.Atan2(dy, dx));
    }
}

internal static class CadPrecisionSnapGeometry
{
    private const double PlaneTolerance = 1e-7;
    private const double GeometryTolerance = 1e-9;

    public static IReadOnlyList<CadSnapPoint> GetCandidates(
        CadDocument document,
        CadWorkPlane workPlane,
        OcctPoint3d queryPoint,
        OcctPoint3d? reference,
        CadSnapType modes,
        IReadOnlyCollection<CadEntity>? candidates = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!queryPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(queryPoint));

        const CadSnapType precisionModes = CadSnapType.Nearest | CadSnapType.Intersection |
            CadSnapType.Perpendicular | CadSnapType.Tangent;
        if ((modes & precisionModes) == 0) return Array.Empty<CadSnapPoint>();

        var curves = BuildCurves(
            document,
            workPlane,
            candidates ?? document.Entities);
        if (curves.Count == 0) return Array.Empty<CadSnapPoint>();

        var result = new List<CadSnapPoint>();
        var query = workPlane.WorldToLocal(queryPoint);

        if ((modes & CadSnapType.Nearest) != 0)
        {
            foreach (var curve in curves)
                if (TryNearest(curve, query, out var nearest))
                    AddUnique(result, new CadSnapPoint(
                        curve.Entity,
                        workPlane.LocalToWorld(nearest),
                        CadSnapType.Nearest,
                        curve.Index));
        }

        if (reference is { } referencePoint)
        {
            var localReference = workPlane.WorldToLocal(referencePoint);

            if ((modes & CadSnapType.Perpendicular) != 0)
            {
                foreach (var curve in curves)
                {
                    foreach (var perpendicular in PerpendicularPoints(curve, localReference))
                        AddUnique(result, new CadSnapPoint(
                            curve.Entity,
                            workPlane.LocalToWorld(perpendicular),
                            CadSnapType.Perpendicular,
                            curve.Index));
                }
            }

            if ((modes & CadSnapType.Tangent) != 0)
            {
                foreach (var curve in curves)
                {
                    foreach (var tangent in TangentPoints(curve, localReference))
                        AddUnique(result, new CadSnapPoint(
                            curve.Entity,
                            workPlane.LocalToWorld(tangent),
                            CadSnapType.Tangent,
                            curve.Index));
                }
            }
        }

        if ((modes & CadSnapType.Intersection) != 0)
        {
            for (var firstIndex = 0; firstIndex < curves.Count; firstIndex++)
            {
                var first = curves[firstIndex];
                for (var secondIndex = firstIndex + 1; secondIndex < curves.Count; secondIndex++)
                {
                    var second = curves[secondIndex];

                    foreach (var intersection in Intersections(first, second))
                    {
                        AddUnique(result, new CadSnapPoint(
                            first.Entity,
                            workPlane.LocalToWorld(intersection),
                            CadSnapType.Intersection,
                            first.Index));
                    }
                }
            }
        }

        return result;
    }

    public static double NormalizePositive(double angle)
    {
        const double full = Math.PI * 2.0;
        var value = angle % full;
        return value < 0.0 ? value + full : value;
    }

    public static bool PointOnSegment(
        CadPlanePoint point,
        CadPlanePoint start,
        CadPlanePoint end,
        double tolerance)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= GeometryTolerance) return false;

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
        if (t < -tolerance || t > 1.0 + tolerance) return false;

        var px = start.X + dx * Math.Clamp(t, 0.0, 1.0);
        var py = start.Y + dy * Math.Clamp(t, 0.0, 1.0);
        return DistanceSquared(point, new CadPlanePoint(px, py)) <= tolerance * tolerance;
    }

    internal static CadPlanePoint ToEllipseLocal(CadSnapCurve ellipse, CadPlanePoint point)
    {
        var dx = point.X - ellipse.Center.X;
        var dy = point.Y - ellipse.Center.Y;
        var cosine = Math.Cos(ellipse.AxisAngle);
        var sine = Math.Sin(ellipse.AxisAngle);
        return new CadPlanePoint(
            dx * cosine + dy * sine,
            -dx * sine + dy * cosine);
    }

    private static CadPlanePoint FromEllipseLocal(CadSnapCurve ellipse, CadPlanePoint point)
    {
        var cosine = Math.Cos(ellipse.AxisAngle);
        var sine = Math.Sin(ellipse.AxisAngle);
        return new CadPlanePoint(
            ellipse.Center.X + point.X * cosine - point.Y * sine,
            ellipse.Center.Y + point.X * sine + point.Y * cosine);
    }

    private static List<CadSnapCurve> BuildCurves(
        CadDocument document,
        CadWorkPlane workPlane,
        IEnumerable<CadEntity> entities)
    {
        var result = new List<CadSnapCurve>();

        foreach (var entity in entities)
        {
            if (!document.IsEntitySelectable(entity))
                continue;

            result.AddRange(entity.GetPrecisionSnapCurves(workPlane));
        }

        return result;
    }

    internal static bool TryCreateSegment(
        CadEntity entity,
        int index,
        OcctPoint3d start,
        OcctPoint3d end,
        CadWorkPlane workPlane,
        out CadSnapCurve curve)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(workPlane);
        curve = default;
        if (!PointOnPlane(start, workPlane) ||
            !PointOnPlane(end, workPlane))
            return false;

        curve = new CadSnapCurve(
            entity,
            index,
            CadSnapCurveKind.Segment,
            workPlane.WorldToLocal(start),
            workPlane.WorldToLocal(end),
            default,
            0.0,
            0.0,
            0.0);
        return true;
    }

    internal static bool TryCreateCircle(
        CadEntity entity,
        int index,
        OcctPoint3d center,
        OcctVector3d normal,
        double radius,
        CadWorkPlane workPlane,
        out CadSnapCurve curve)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(workPlane);
        curve = default;
        if (!double.IsFinite(radius) ||
            radius <= GeometryTolerance ||
            !IsPlaneCurve(center, normal, workPlane))
            return false;

        curve = new CadSnapCurve(
            entity,
            index,
            CadSnapCurveKind.Circle,
            default,
            default,
            workPlane.WorldToLocal(center),
            radius,
            0.0,
            Math.PI * 2.0);
        return true;
    }

    internal static bool TryCreateEllipse(
        CadEllipseEntity ellipse,
        CadWorkPlane workPlane,
        out CadSnapCurve curve)
    {
        ArgumentNullException.ThrowIfNull(ellipse);
        ArgumentNullException.ThrowIfNull(workPlane);
        curve = default;
        if (!IsPlaneCurve(ellipse.Center, ellipse.Normal, workPlane) ||
            !double.IsFinite(ellipse.MajorRadius) ||
            !double.IsFinite(ellipse.MinorRadius) ||
            ellipse.MajorRadius <= GeometryTolerance ||
            ellipse.MinorRadius <= GeometryTolerance)
            return false;

        var center = workPlane.WorldToLocal(ellipse.Center);
        var axisPoint = workPlane.WorldToLocal(ellipse.Center + ellipse.XAxis);
        var dx = axisPoint.X - center.X;
        var dy = axisPoint.Y - center.Y;
        if (dx * dx + dy * dy <= GeometryTolerance * GeometryTolerance)
            return false;

        curve = new CadSnapCurve(
            ellipse,
            0,
            CadSnapCurveKind.Ellipse,
            default,
            default,
            center,
            ellipse.MajorRadius,
            0.0,
            Math.PI * 2.0,
            ellipse.MinorRadius,
            Math.Atan2(dy, dx));
        return true;
    }

    internal static bool TryCreateArc(
        CadArcEntity arc,
        CadWorkPlane workPlane,
        out CadSnapCurve curve)
    {
        curve = default;
        if (!PointOnPlane(arc.Start, workPlane) ||
            !PointOnPlane(arc.Middle, workPlane) ||
            !PointOnPlane(arc.End, workPlane))
            return false;

        var start = workPlane.WorldToLocal(arc.Start);
        var middle = workPlane.WorldToLocal(arc.Middle);
        var end = workPlane.WorldToLocal(arc.End);
        if (!TryCircumcircle(start, middle, end, out var center, out var radius)) return false;

        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        var middleAngle = Math.Atan2(middle.Y - center.Y, middle.X - center.X);
        var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
        var ccwSweep = NormalizePositive(endAngle - startAngle);
        var middleCcw = NormalizePositive(middleAngle - startAngle);
        var sweep = middleCcw <= ccwSweep + GeometryTolerance
            ? ccwSweep
            : ccwSweep - Math.PI * 2.0;

        curve = new CadSnapCurve(
            arc,
            0,
            CadSnapCurveKind.Arc,
            start,
            end,
            center,
            radius,
            startAngle,
            sweep);
        return true;
    }

    private static bool TryNearest(CadSnapCurve curve, CadPlanePoint query, out CadPlanePoint point)
    {
        if (curve.Kind == CadSnapCurveKind.Segment)
        {
            point = ProjectToSegment(query, curve.Start, curve.End);
            return true;
        }

        if (curve.Kind == CadSnapCurveKind.Ellipse)
            return TryNearestEllipse(curve, query, out point);

        var dx = query.X - curve.Center.X;
        var dy = query.Y - curve.Center.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= GeometryTolerance)
        {
            point = default;
            return false;
        }

        point = new CadPlanePoint(
            curve.Center.X + dx * curve.Radius / length,
            curve.Center.Y + dy * curve.Radius / length);

        if (curve.Kind != CadSnapCurveKind.Arc ||
            curve.ContainsAngle(Math.Atan2(point.Y - curve.Center.Y, point.X - curve.Center.X)))
            return true;

        point = DistanceSquared(query, curve.Start) <= DistanceSquared(query, curve.End)
            ? curve.Start
            : curve.End;
        return true;
    }

    private static bool TryNearestEllipse(
        CadSnapCurve ellipse,
        CadPlanePoint query,
        out CadPlanePoint point)
    {
        var local = ToEllipseLocal(ellipse, query);
        var a = ellipse.Radius;
        var b = ellipse.SecondaryRadius;
        if (Math.Abs(local.X) <= GeometryTolerance &&
            Math.Abs(local.Y) <= GeometryTolerance)
        {
            point = FromEllipseLocal(ellipse, new CadPlanePoint(0.0, b));
            return true;
        }

        var initial = Math.Atan2(a * local.Y, b * local.X);
        var bestDistance = double.PositiveInfinity;
        var best = default(CadPlanePoint);
        var found = false;

        for (var seed = 0; seed < 4; seed++)
        {
            var parameter = RefineEllipseParameter(
                a,
                b,
                local,
                initial + seed * Math.PI * 0.5);
            if (!double.IsFinite(parameter)) continue;

            var candidate = new CadPlanePoint(
                a * Math.Cos(parameter),
                b * Math.Sin(parameter));
            var distance = DistanceSquared(candidate, local);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
            found = true;
        }

        if (!found)
        {
            point = default;
            return false;
        }

        point = FromEllipseLocal(ellipse, best);
        return true;
    }

    private static double RefineEllipseParameter(
        double a,
        double b,
        CadPlanePoint query,
        double parameter)
    {
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var cosine = Math.Cos(parameter);
            var sine = Math.Sin(parameter);
            var px = a * cosine;
            var py = b * sine;
            var tx = -a * sine;
            var ty = b * cosine;
            var rx = px - query.X;
            var ry = py - query.Y;
            var function = rx * tx + ry * ty;
            if (Math.Abs(function) <= 1e-13 * Math.Max(1.0, a * a + b * b))
                break;

            var derivative =
                tx * tx + ty * ty -
                a * cosine * rx -
                b * sine * ry;
            if (Math.Abs(derivative) <= 1e-15)
                break;

            var step = function / derivative;
            if (!double.IsFinite(step))
                return double.NaN;
            step = Math.Clamp(step, -Math.PI * 0.25, Math.PI * 0.25);
            parameter -= step;
            if (Math.Abs(step) <= 1e-13)
                break;
        }

        return NormalizePositive(parameter);
    }

    private static IEnumerable<CadPlanePoint> PerpendicularPoints(
        CadSnapCurve curve,
        CadPlanePoint reference)
    {
        if (curve.Kind == CadSnapCurveKind.Segment)
        {
            var projected = ProjectToInfiniteLine(reference, curve.Start, curve.End, out var t);
            if (t >= -GeometryTolerance && t <= 1.0 + GeometryTolerance)
                yield return projected;
            yield break;
        }

        if (curve.Kind == CadSnapCurveKind.Ellipse)
        {
            if (TryNearestEllipse(curve, reference, out var nearest))
                yield return nearest;
            yield break;
        }

        var dx = reference.X - curve.Center.X;
        var dy = reference.Y - curve.Center.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= GeometryTolerance) yield break;

        var ux = dx / length;
        var uy = dy / length;
        var first = new CadPlanePoint(
            curve.Center.X + ux * curve.Radius,
            curve.Center.Y + uy * curve.Radius);
        var second = new CadPlanePoint(
            curve.Center.X - ux * curve.Radius,
            curve.Center.Y - uy * curve.Radius);

        if (curve.Kind != CadSnapCurveKind.Arc || curve.ContainsPoint(first))
            yield return first;
        if (curve.Kind != CadSnapCurveKind.Arc || curve.ContainsPoint(second))
            yield return second;
    }

    private static IEnumerable<CadPlanePoint> TangentPoints(
        CadSnapCurve curve,
        CadPlanePoint reference)
    {
        if (curve.Kind == CadSnapCurveKind.Segment) yield break;
        if (curve.Kind == CadSnapCurveKind.Ellipse)
        {
            foreach (var tangent in EllipseTangentPoints(curve, reference))
                yield return tangent;
            yield break;
        }

        var dx = reference.X - curve.Center.X;
        var dy = reference.Y - curve.Center.Y;
        var distanceSquared = dx * dx + dy * dy;
        var radiusSquared = curve.Radius * curve.Radius;
        if (distanceSquared <= radiusSquared + GeometryTolerance) yield break;

        var baseFactor = radiusSquared / distanceSquared;
        var offsetFactor = curve.Radius * Math.Sqrt(distanceSquared - radiusSquared) / distanceSquared;
        var baseX = curve.Center.X + dx * baseFactor;
        var baseY = curve.Center.Y + dy * baseFactor;

        var first = new CadPlanePoint(
            baseX - dy * offsetFactor,
            baseY + dx * offsetFactor);
        var second = new CadPlanePoint(
            baseX + dy * offsetFactor,
            baseY - dx * offsetFactor);

        if (curve.Kind != CadSnapCurveKind.Arc || curve.ContainsPoint(first))
            yield return first;
        if (curve.Kind != CadSnapCurveKind.Arc || curve.ContainsPoint(second))
            yield return second;
    }

    private static IEnumerable<CadPlanePoint> EllipseTangentPoints(
        CadSnapCurve ellipse,
        CadPlanePoint reference)
    {
        var local = ToEllipseLocal(ellipse, reference);
        var u = local.X / ellipse.Radius;
        var v = local.Y / ellipse.SecondaryRadius;
        var d2 = u * u + v * v;
        if (d2 <= 1.0 + GeometryTolerance) yield break;

        var baseX = u / d2;
        var baseY = v / d2;
        var scale = Math.Sqrt(d2 - 1.0) / d2;
        var one = new CadPlanePoint(
            ellipse.Radius * (baseX - v * scale),
            ellipse.SecondaryRadius * (baseY + u * scale));
        var two = new CadPlanePoint(
            ellipse.Radius * (baseX + v * scale),
            ellipse.SecondaryRadius * (baseY - u * scale));
        yield return FromEllipseLocal(ellipse, one);
        yield return FromEllipseLocal(ellipse, two);
    }

    private static IEnumerable<CadPlanePoint> Intersections(CadSnapCurve first, CadSnapCurve second)
    {
        if (first.Kind == CadSnapCurveKind.Segment && second.Kind == CadSnapCurveKind.Segment)
            return SegmentSegment(first, second);

        if (first.Kind == CadSnapCurveKind.Ellipse || second.Kind == CadSnapCurveKind.Ellipse)
        {
            if (first.Kind == CadSnapCurveKind.Segment && second.Kind == CadSnapCurveKind.Ellipse)
                return SegmentEllipse(first, second);
            if (second.Kind == CadSnapCurveKind.Segment && first.Kind == CadSnapCurveKind.Ellipse)
                return SegmentEllipse(second, first);
            return Array.Empty<CadPlanePoint>();
        }

        if (first.Kind == CadSnapCurveKind.Segment)
            return SegmentCircle(first, second);

        if (second.Kind == CadSnapCurveKind.Segment)
            return SegmentCircle(second, first);

        return CircleCircle(first, second);
    }

    private static IEnumerable<CadPlanePoint> SegmentSegment(CadSnapCurve first, CadSnapCurve second)
    {
        var p = first.Start;
        var r = new CadPlanePoint(first.End.X - first.Start.X, first.End.Y - first.Start.Y);
        var q = second.Start;
        var s = new CadPlanePoint(second.End.X - second.Start.X, second.End.Y - second.Start.Y);
        var cross = Cross(r, s);
        if (Math.Abs(cross) <= GeometryTolerance) yield break;

        var qp = new CadPlanePoint(q.X - p.X, q.Y - p.Y);
        var t = Cross(qp, s) / cross;
        var u = Cross(qp, r) / cross;
        if (t < -GeometryTolerance || t > 1.0 + GeometryTolerance ||
            u < -GeometryTolerance || u > 1.0 + GeometryTolerance)
            yield break;

        yield return new CadPlanePoint(p.X + r.X * t, p.Y + r.Y * t);
    }

    private static IEnumerable<CadPlanePoint> SegmentEllipse(
        CadSnapCurve segment,
        CadSnapCurve ellipse)
    {
        var start = ToEllipseLocal(ellipse, segment.Start);
        var end = ToEllipseLocal(ellipse, segment.End);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var a2 = ellipse.Radius * ellipse.Radius;
        var b2 = ellipse.SecondaryRadius * ellipse.SecondaryRadius;
        var qa = dx * dx / a2 + dy * dy / b2;
        if (qa <= GeometryTolerance) yield break;
        var qb = 2.0 * (start.X * dx / a2 + start.Y * dy / b2);
        var qc = start.X * start.X / a2 + start.Y * start.Y / b2 - 1.0;
        var discriminant = qb * qb - 4.0 * qa * qc;
        if (discriminant < -GeometryTolerance) yield break;

        if (Math.Abs(discriminant) <= GeometryTolerance)
        {
            var t = -qb / (2.0 * qa);
            if (t >= -GeometryTolerance && t <= 1.0 + GeometryTolerance)
                yield return FromEllipseLocal(
                    ellipse,
                    new CadPlanePoint(start.X + dx * t, start.Y + dy * t));
            yield break;
        }

        var root = Math.Sqrt(Math.Max(0.0, discriminant));
        var t1 = (-qb - root) / (2.0 * qa);
        var t2 = (-qb + root) / (2.0 * qa);
        if (t1 >= -GeometryTolerance && t1 <= 1.0 + GeometryTolerance)
            yield return FromEllipseLocal(
                ellipse,
                new CadPlanePoint(start.X + dx * t1, start.Y + dy * t1));
        if (t2 >= -GeometryTolerance && t2 <= 1.0 + GeometryTolerance)
            yield return FromEllipseLocal(
                ellipse,
                new CadPlanePoint(start.X + dx * t2, start.Y + dy * t2));
    }

    private static IEnumerable<CadPlanePoint> SegmentCircle(CadSnapCurve segment, CadSnapCurve circle)
    {
        var dx = segment.End.X - segment.Start.X;
        var dy = segment.End.Y - segment.Start.Y;
        var fx = segment.Start.X - circle.Center.X;
        var fy = segment.Start.Y - circle.Center.Y;

        var a = dx * dx + dy * dy;
        if (a <= GeometryTolerance) yield break;
        var b = 2.0 * (fx * dx + fy * dy);
        var c = fx * fx + fy * fy - circle.Radius * circle.Radius;
        var discriminant = b * b - 4.0 * a * c;
        if (discriminant < -GeometryTolerance) yield break;

        if (Math.Abs(discriminant) <= GeometryTolerance)
        {
            var t = -b / (2.0 * a);
            if (t >= -GeometryTolerance && t <= 1.0 + GeometryTolerance)
            {
                var point = new CadPlanePoint(segment.Start.X + dx * t, segment.Start.Y + dy * t);
                if (circle.ContainsPoint(point)) yield return point;
            }
            yield break;
        }

        var root = Math.Sqrt(Math.Max(0.0, discriminant));
        var t1 = (-b - root) / (2.0 * a);
        var t2 = (-b + root) / (2.0 * a);
        if (t1 >= -GeometryTolerance && t1 <= 1.0 + GeometryTolerance)
        {
            var point = new CadPlanePoint(segment.Start.X + dx * t1, segment.Start.Y + dy * t1);
            if (circle.ContainsPoint(point)) yield return point;
        }
        if (t2 >= -GeometryTolerance && t2 <= 1.0 + GeometryTolerance)
        {
            var point = new CadPlanePoint(segment.Start.X + dx * t2, segment.Start.Y + dy * t2);
            if (circle.ContainsPoint(point)) yield return point;
        }
    }

    private static IEnumerable<CadPlanePoint> CircleCircle(CadSnapCurve first, CadSnapCurve second)
    {
        var dx = second.Center.X - first.Center.X;
        var dy = second.Center.Y - first.Center.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d <= GeometryTolerance) yield break;
        if (d > first.Radius + second.Radius + GeometryTolerance) yield break;
        if (d < Math.Abs(first.Radius - second.Radius) - GeometryTolerance) yield break;

        var a = (first.Radius * first.Radius - second.Radius * second.Radius + d * d) / (2.0 * d);
        var hSquared = first.Radius * first.Radius - a * a;
        if (hSquared < -GeometryTolerance) yield break;

        var px = first.Center.X + a * dx / d;
        var py = first.Center.Y + a * dy / d;
        if (Math.Abs(hSquared) <= GeometryTolerance)
        {
            var tangent = new CadPlanePoint(px, py);
            if (first.ContainsPoint(tangent) && second.ContainsPoint(tangent))
                yield return tangent;
            yield break;
        }

        var h = Math.Sqrt(Math.Max(0.0, hSquared));
        var rx = -dy * h / d;
        var ry = dx * h / d;
        var one = new CadPlanePoint(px + rx, py + ry);
        var two = new CadPlanePoint(px - rx, py - ry);
        if (first.ContainsPoint(one) && second.ContainsPoint(one)) yield return one;
        if (first.ContainsPoint(two) && second.ContainsPoint(two)) yield return two;
    }

    private static CadPlanePoint ProjectToSegment(
        CadPlanePoint point,
        CadPlanePoint start,
        CadPlanePoint end)
    {
        var projected = ProjectToInfiniteLine(point, start, end, out var t);
        if (t <= 0.0) return start;
        if (t >= 1.0) return end;
        return projected;
    }

    private static CadPlanePoint ProjectToInfiniteLine(
        CadPlanePoint point,
        CadPlanePoint start,
        CadPlanePoint end,
        out double t)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= GeometryTolerance)
        {
            t = 0.0;
            return start;
        }

        t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
        return new CadPlanePoint(start.X + dx * t, start.Y + dy * t);
    }

    private static bool TryCircumcircle(
        CadPlanePoint a,
        CadPlanePoint b,
        CadPlanePoint c,
        out CadPlanePoint center,
        out double radius)
    {
        var d = 2.0 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (Math.Abs(d) <= GeometryTolerance)
        {
            center = default;
            radius = 0.0;
            return false;
        }

        var aa = a.X * a.X + a.Y * a.Y;
        var bb = b.X * b.X + b.Y * b.Y;
        var cc = c.X * c.X + c.Y * c.Y;
        center = new CadPlanePoint(
            (aa * (b.Y - c.Y) + bb * (c.Y - a.Y) + cc * (a.Y - b.Y)) / d,
            (aa * (c.X - b.X) + bb * (a.X - c.X) + cc * (b.X - a.X)) / d);
        radius = Math.Sqrt(DistanceSquared(center, a));
        return radius > GeometryTolerance;
    }

    private static bool IsPlaneCurve(
        OcctPoint3d center,
        OcctVector3d normal,
        CadWorkPlane workPlane)
    {
        if (!PointOnPlane(center, workPlane) || !normal.TryNormalize(out var n)) return false;
        return Math.Abs(Math.Abs(n.Dot(workPlane.Normal)) - 1.0) <= 1e-7;
    }

    private static bool PointOnPlane(OcctPoint3d point, CadWorkPlane workPlane) =>
        Math.Abs((point - workPlane.Origin).Dot(workPlane.Normal)) <= PlaneTolerance;

    private static double Cross(CadPlanePoint left, CadPlanePoint right) =>
        left.X * right.Y - left.Y * right.X;

    private static double DistanceSquared(CadPlanePoint left, CadPlanePoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return dx * dx + dy * dy;
    }

    private static void AddUnique(ICollection<CadSnapPoint> values, CadSnapPoint candidate)
    {
        foreach (var existing in values)
        {
            if (existing.Type == candidate.Type &&
                existing.Position.DistanceTo(candidate.Position) <= 1e-8)
                return;
        }
        values.Add(candidate);
    }
}
