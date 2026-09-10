using OcctNet;

namespace OCCAD;

internal static class CadOffsetGeometry
{
    public static bool IsSupported(CadEntity entity) =>
        entity is CadLineEntity
            or CadCircleEntity
            or CadArcEntity
            or CadRectangleEntity
            or CadRegularPolygonEntity
            or CadPolylineEntity;

    public static bool TryOffset(
        CadEntity entity,
        OcctPoint3d pickPoint,
        double distance,
        CadWorkPlane workPlane,
        out CadEntity? result)
    {
        result = null;
        if (!double.IsFinite(distance) || distance <= 1e-6 || !pickPoint.IsFinite)
            return false;

        var planeNormal = workPlane.Normal.Normalized();

        switch (entity)
        {
            case CadLineEntity line:
            {
                var start = line.ToWorldPoint(line.Start);
                var end = line.ToWorldPoint(line.End);
                var dir = end - start;
                if (!dir.TryNormalize(out var lineUnit))
                    return false;

                var perp = planeNormal.Cross(lineUnit);
                if (!perp.TryNormalize(out var perpUnit))
                    return false;

                var sideDot = (pickPoint - start).Dot(perpUnit);
                var sideSign = sideDot >= 0.0 ? 1.0 : -1.0;
                var displacement = perpUnit * (distance * sideSign);

                result = new CadLineEntity(start + displacement, end + displacement);
                return true;
            }

            case CadCircleEntity circle:
            {
                var center = circle.ToWorldPoint(circle.Center);
                var normal = circle.ToWorldVector(circle.Normal).Normalized();
                var distFromCenter = (pickPoint - center).Length;

                double newRadius;
                if (distFromCenter >= circle.Radius)
                {
                    newRadius = circle.Radius + distance;
                }
                else
                {
                    newRadius = circle.Radius - distance;
                    if (newRadius <= 1e-4)
                        return false;
                }

                result = new CadCircleEntity(center, normal, newRadius);
                return true;
            }

            case CadArcEntity arc:
            {
                var start = arc.ToWorldPoint(arc.Start);
                var mid = arc.ToWorldPoint(arc.Middle);
                var end = arc.ToWorldPoint(arc.End);

                if (!TryComputeCircle3P(start, mid, end, out var center, out var normal, out var radius))
                    return false;

                var distFromCenter = (pickPoint - center).Length;
                double newRadius;
                if (distFromCenter >= radius)
                {
                    newRadius = radius + distance;
                }
                else
                {
                    newRadius = radius - distance;
                    if (newRadius <= 1e-4)
                        return false;
                }

                var scale = newRadius / radius;
                var newStart = center + (start - center) * scale;
                var newMid = center + (mid - center) * scale;
                var newEnd = center + (end - center) * scale;

                result = new CadArcEntity(newStart, newMid, newEnd);
                return true;
            }

            case CadRectangleEntity rect:
            {
                var center = rect.ToWorldPoint(rect.Center);
                var xAxis = rect.ToWorldVector(rect.XAxis).Normalized();
                var yAxis = rect.ToWorldVector(rect.YAxis).Normalized();

                var rel = pickPoint - center;
                var u = Math.Abs(rel.Dot(xAxis));
                var v = Math.Abs(rel.Dot(yAxis));

                var isOutside = u > rect.Width / 2.0 || v > rect.Height / 2.0;
                double newWidth, newHeight;
                if (isOutside)
                {
                    newWidth = rect.Width + 2.0 * distance;
                    newHeight = rect.Height + 2.0 * distance;
                }
                else
                {
                    newWidth = rect.Width - 2.0 * distance;
                    newHeight = rect.Height - 2.0 * distance;
                    if (newWidth <= 1e-4 || newHeight <= 1e-4)
                        return false;
                }

                result = new CadRectangleEntity(center, xAxis, yAxis, newWidth, newHeight);
                return true;
            }

            case CadRegularPolygonEntity poly:
            {
                var center = poly.ToWorldPoint(poly.Center);
                var normal = poly.ToWorldVector(poly.Normal).Normalized();
                var xAxis = poly.ToWorldVector(poly.XAxis).Normalized();

                var distFromCenter = (pickPoint - center).Length;
                double newRadius;
                if (distFromCenter >= poly.Radius)
                {
                    newRadius = poly.Radius + distance;
                }
                else
                {
                    newRadius = poly.Radius - distance;
                    if (newRadius <= 1e-4)
                        return false;
                }

                result = new CadRegularPolygonEntity(center, normal, xAxis, newRadius, poly.Sides);
                return true;
            }

            case CadPolylineEntity polyline:
            {
                var pts = polyline.Points.Select(polyline.ToWorldPoint).ToList();
                if (pts.Count < 2)
                    return false;

                var count = pts.Count;
                var isClosed = polyline.Closed;
                var segCount = isClosed ? count : count - 1;

                // Determine side using pickPoint relative to first segment
                var firstStart = pts[0];
                var firstDir = (pts[1] - firstStart).Normalized();
                var firstPerp = planeNormal.Cross(firstDir).Normalized();
                var sideSign = (pickPoint - firstStart).Dot(firstPerp) >= 0.0 ? 1.0 : -1.0;

                // Shift each segment
                var offsetSegs = new List<(OcctPoint3d S, OcctPoint3d E, OcctVector3d Dir)>();
                for (var i = 0; i < segCount; i++)
                {
                    var s = pts[i];
                    var e = pts[(i + 1) % count];
                    var dir = e - s;
                    if (!dir.TryNormalize(out var udir))
                        udir = OcctVector3d.UnitX;
                    var perp = planeNormal.Cross(udir).Normalized() * (distance * sideSign);
                    offsetSegs.Add((s + perp, e + perp, udir));
                }

                var newPoints = new List<OcctPoint3d>();
                if (!isClosed)
                {
                    newPoints.Add(offsetSegs[0].S);
                    for (var i = 0; i < offsetSegs.Count - 1; i++)
                    {
                        var seg1 = offsetSegs[i];
                        var seg2 = offsetSegs[i + 1];
                        if (TryIntersect2DLines(seg1.S, seg1.Dir, seg2.S, seg2.Dir, planeNormal, out var inter))
                            newPoints.Add(inter);
                        else
                            newPoints.Add(seg1.E);
                    }
                    newPoints.Add(offsetSegs[^1].E);
                }
                else
                {
                    for (var i = 0; i < offsetSegs.Count; i++)
                    {
                        var prev = offsetSegs[(i - 1 + offsetSegs.Count) % offsetSegs.Count];
                        var curr = offsetSegs[i];
                        if (TryIntersect2DLines(prev.S, prev.Dir, curr.S, curr.Dir, planeNormal, out var inter))
                            newPoints.Add(inter);
                        else
                            newPoints.Add(curr.S);
                    }
                }

                result = new CadPolylineEntity(newPoints, isClosed);
                return true;
            }

            default:
                return false;
        }
    }

    private static bool TryIntersect2DLines(
        OcctPoint3d p1,
        OcctVector3d d1,
        OcctPoint3d p2,
        OcctVector3d d2,
        OcctVector3d normal,
        out OcctPoint3d intersection)
    {
        intersection = default;
        var cross = d1.Cross(d2);
        var denom = cross.Dot(normal);
        if (Math.Abs(denom) < 1e-7)
            return false; // parallel

        var diff = p2 - p1;
        var t = diff.Cross(d2).Dot(normal) / denom;
        intersection = p1 + d1 * t;
        return true;
    }

    private static bool TryComputeCircle3P(
        OcctPoint3d p1,
        OcctPoint3d p2,
        OcctPoint3d p3,
        out OcctPoint3d center,
        out OcctVector3d normal,
        out double radius)
    {
        center = default;
        normal = default;
        radius = 0.0;

        var v1 = p2 - p1;
        var v2 = p3 - p1;
        var n = v1.Cross(v2);
        if (!n.TryNormalize(out normal))
            return false;

        var mid1 = p1 + v1 * 0.5;
        var mid2 = p1 + v2 * 0.5;
        var perp1 = normal.Cross(v1).Normalized();
        var perp2 = normal.Cross(v2).Normalized();

        if (!TryIntersect2DLines(mid1, perp1, mid2, perp2, normal, out center))
            return false;

        radius = (p1 - center).Length;
        return double.IsFinite(radius) && radius > 1e-6;
    }
}
