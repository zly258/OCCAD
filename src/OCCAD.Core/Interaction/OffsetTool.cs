using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class OffsetTool : CadSelectionTransformToolBase, ICadPointInputTool
{
    private const double PlaneTolerance = 1e-6;
    private const double DistanceTolerance = 1e-9;

    private double? _distance;

    public override string Id => "offset";
    public override string DisplayName => "Offset";

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Offset",
            [
                new CadOptionalDoubleToolParameterDescriptor(
                    "Distance",
                    "Distance",
                    _distance,
                    DistanceTolerance,
                    double.MaxValue)
            ]);

    protected override void OnActivated()
    {
        SetSelectionFilter(
            new CadSelectionFilter(
                "offset.curves",
                static entity =>
                    entity is CadLineEntity or
                    CadPolylineEntity or
                    CadCircleEntity or
                    CadArcEntity));
        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        SetStageLocalized(
            0,
            "Cad.Prompt.offset.Side",
            "Offset: specify side and distance [Esc cancel]",
            CadPrecisionInputKind.None);
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect || Entities.Count == 0)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            ShowPreview(
                Context.ResolvePoint(input.X, input.Y).Point);
            return true;
        }

        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Left)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(input.X, input.Y).Point);
    }

    protected override bool CanCommitCurrentStageCore => true;

    protected override bool OnCommitCurrentStage(
        CadPointerPosition pointer)
    {
        if (State != CadToolState.Drawing)
            return false;

        return AcceptPoint(
            Context.ResolvePoint(pointer.X, pointer.Y).Point);
    }

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive &&
        State == CadToolState.Drawing &&
        AcceptPoint(point);

    protected override bool OnSetParameter(string id, string value)
    {
        if (!id.Equals("Distance", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(value))
        {
            _distance = null;
            RefreshPreviewFromLastPointer();
            NotifyUpdated();
            return true;
        }

        if (!TryPositive(value, out var distance))
            return false;

        _distance = distance;
        RefreshPreviewFromLastPointer();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState() =>
        _distance = null;

    private bool AcceptPoint(OcctPoint3d point)
    {
        if (!TryCreateOffsets(point, out var offsets))
        {
            Context.Preview.Clear();
            SetPromptLocalized(
                "Cad.Prompt.offset.Invalid",
                "Offset: selected curves must lie on the current drawing plane and produce valid offset geometry.");
            return true;
        }

        Context.Workspace.AddGeneratedEntities(offsets, "Offset");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void ShowPreview(OcctPoint3d point)
    {
        if (TryCreateOffsets(point, out var offsets))
            Context.Preview.Show(offsets);
        else
            Context.Preview.Clear();
    }

    private bool TryCreateOffsets(
        OcctPoint3d sidePoint,
        out CadEntity[] offsets)
    {
        var result = new List<CadEntity>(Entities.Count);
        foreach (var entity in Entities)
        {
            if (!TryCreateOffset(entity, sidePoint, out var offset))
            {
                offsets = [];
                return false;
            }

            result.Add(offset);
        }

        offsets = result.ToArray();
        return offsets.Length > 0;
    }

    private bool TryCreateOffset(
        CadEntity entity,
        OcctPoint3d sidePoint,
        out CadEntity offset) =>
        entity switch
        {
            CadLineEntity line =>
                TryCreateLineOffset(line, sidePoint, out offset),
            CadPolylineEntity polyline =>
                TryCreatePolylineOffset(polyline, sidePoint, out offset),
            CadCircleEntity circle =>
                TryCreateCircleOffset(circle, sidePoint, out offset),
            CadArcEntity arc =>
                TryCreateArcOffset(arc, sidePoint, out offset),
            _ => Fail(out offset)
        };

    private bool TryCreateLineOffset(
        CadLineEntity line,
        OcctPoint3d sidePoint,
        out CadEntity offset)
    {
        if (!IsOnPlane(line.Start, line.End))
            return Fail(out offset);

        var start = Context.WorkPlane.WorldToLocal(line.Start);
        var end = Context.WorkPlane.WorldToLocal(line.End);
        var cursor = Context.WorkPlane.WorldToLocal(sidePoint);
        if (!TrySegmentNormal(start, end, out var nx, out var ny))
            return Fail(out offset);

        var midpointX = (start.X + end.X) * 0.5;
        var midpointY = (start.Y + end.Y) * 0.5;
        var side =
            (cursor.X - midpointX) * nx +
            (cursor.Y - midpointY) * ny;
        if (!TrySignedDistance(side, out var signedDistance))
            return Fail(out offset);

        var displacement =
            Context.WorkPlane.XAxis * (nx * signedDistance) +
            Context.WorkPlane.YAxis * (ny * signedDistance);

        var copy = line.Duplicate();
        copy.Translate(displacement);
        offset = copy;
        return true;
    }

    private bool TryCreateCircleOffset(
        CadCircleEntity circle,
        OcctPoint3d sidePoint,
        out CadEntity offset)
    {
        if (!IsCircleOnPlane(circle.Center, circle.Normal))
            return Fail(out offset);

        var localCenter = Context.WorkPlane.WorldToLocal(circle.Center);
        var localCursor = Context.WorkPlane.WorldToLocal(sidePoint);
        var dx = localCursor.X - localCenter.X;
        var dy = localCursor.Y - localCenter.Y;
        var radial = Math.Sqrt(dx * dx + dy * dy);
        var side = radial - circle.Radius;
        if (!TrySignedDistance(side, out var signedDistance))
            return Fail(out offset);

        var radius = circle.Radius + signedDistance;
        if (radius <= DistanceTolerance)
            return Fail(out offset);

        var copy = (CadCircleEntity)circle.Duplicate();
        copy.Radius = radius;
        offset = copy;
        return true;
    }

    private bool TryCreateArcOffset(
        CadArcEntity arc,
        OcctPoint3d sidePoint,
        out CadEntity offset)
    {
        if (!IsCircleOnPlane(arc.Center, arc.Normal))
            return Fail(out offset);

        var localCenter = Context.WorkPlane.WorldToLocal(arc.Center);
        var localCursor = Context.WorkPlane.WorldToLocal(sidePoint);
        var dx = localCursor.X - localCenter.X;
        var dy = localCursor.Y - localCenter.Y;
        var radial = Math.Sqrt(dx * dx + dy * dy);
        var side = radial - arc.Radius;
        if (!TrySignedDistance(side, out var signedDistance))
            return Fail(out offset);

        var radius = arc.Radius + signedDistance;
        if (radius <= DistanceTolerance)
            return Fail(out offset);

        var copy = (CadArcEntity)arc.Duplicate();
        copy.Radius = radius;
        offset = copy;
        return true;
    }

    private bool TryCreatePolylineOffset(
        CadPolylineEntity polyline,
        OcctPoint3d sidePoint,
        out CadEntity offset)
    {
        var plane = Context.WorkPlane;
        if (polyline.Points.Any(point => !IsOnPlane(point)))
            return Fail(out offset);

        var points = polyline.Points
            .Select(plane.WorldToLocal)
            .ToArray();
        var segmentCount = polyline.Closed
            ? points.Length
            : points.Length - 1;
        if (segmentCount <= 0)
            return Fail(out offset);

        var normals = new (double X, double Y)[segmentCount];
        for (var index = 0; index < segmentCount; index++)
        {
            var next = (index + 1) % points.Length;
            if (!TrySegmentNormal(
                    points[index],
                    points[next],
                    out var nx,
                    out var ny))
                return Fail(out offset);
            normals[index] = (nx, ny);
        }

        var cursor = plane.WorldToLocal(sidePoint);
        var nearestSegment = 0;
        var nearestDistance = double.MaxValue;
        for (var index = 0; index < segmentCount; index++)
        {
            var next = (index + 1) % points.Length;
            var distance = DistanceSquaredToSegment(
                cursor,
                points[index],
                points[next]);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSegment = index;
            }
        }

        var segmentStart = points[nearestSegment];
        var segmentEnd = points[(nearestSegment + 1) % points.Length];
        var midpointX = (segmentStart.X + segmentEnd.X) * 0.5;
        var midpointY = (segmentStart.Y + segmentEnd.Y) * 0.5;
        var nearestNormal = normals[nearestSegment];
        var side =
            (cursor.X - midpointX) * nearestNormal.X +
            (cursor.Y - midpointY) * nearestNormal.Y;
        if (!TrySignedDistance(side, out var signedDistance))
            return Fail(out offset);

        var shiftedStarts = new CadPlanePoint[segmentCount];
        var shiftedEnds = new CadPlanePoint[segmentCount];
        for (var index = 0; index < segmentCount; index++)
        {
            var next = (index + 1) % points.Length;
            var normal = normals[index];
            var ox = normal.X * signedDistance;
            var oy = normal.Y * signedDistance;
            shiftedStarts[index] =
                new CadPlanePoint(points[index].X + ox, points[index].Y + oy);
            shiftedEnds[index] =
                new CadPlanePoint(points[next].X + ox, points[next].Y + oy);
        }

        var result = new CadPlanePoint[points.Length];
        if (polyline.Closed)
        {
            for (var index = 0; index < points.Length; index++)
            {
                var previous = (index - 1 + segmentCount) % segmentCount;
                var current = index % segmentCount;
                if (!TryLineIntersection(
                        shiftedStarts[previous],
                        shiftedEnds[previous],
                        shiftedStarts[current],
                        shiftedEnds[current],
                        out result[index]))
                    return Fail(out offset);
            }
        }
        else
        {
            result[0] = shiftedStarts[0];
            result[^1] = shiftedEnds[^1];
            for (var index = 1; index < points.Length - 1; index++)
            {
                if (!TryLineIntersection(
                        shiftedStarts[index - 1],
                        shiftedEnds[index - 1],
                        shiftedStarts[index],
                        shiftedEnds[index],
                        out result[index]))
                    return Fail(out offset);
            }
        }

        var copy = (CadPolylineEntity)polyline.Duplicate();
        for (var index = 0; index < result.Length; index++)
            copy.MoveGrip(index, plane.LocalToWorld(result[index]));

        offset = copy;
        return true;
    }

    private bool TrySignedDistance(
        double side,
        out double signedDistance)
    {
        if (Math.Abs(side) <= DistanceTolerance)
        {
            signedDistance = 0.0;
            return false;
        }

        var distance = _distance ?? Math.Abs(side);
        if (!double.IsFinite(distance) ||
            distance <= DistanceTolerance)
        {
            signedDistance = 0.0;
            return false;
        }

        signedDistance = Math.CopySign(distance, side);
        return true;
    }

    private bool IsCircleOnPlane(
        OcctPoint3d center,
        OcctVector3d normal)
    {
        if (!IsOnPlane(center))
            return false;

        var dot = Math.Abs(
            CadTransformMath.Dot(
                normal.Normalized(),
                Context.WorkPlane.Normal));
        return Math.Abs(1.0 - dot) <= PlaneTolerance;
    }

    private bool IsOnPlane(
        OcctPoint3d first,
        OcctPoint3d second) =>
        IsOnPlane(first) && IsOnPlane(second);

    private bool IsOnPlane(OcctPoint3d point)
    {
        var axial = CadTransformMath.Dot(
            CadTransformMath.Between(
                Context.WorkPlane.Origin,
                point),
            Context.WorkPlane.Normal);
        return Math.Abs(axial) <= PlaneTolerance;
    }

    private static bool TrySegmentNormal(
        CadPlanePoint start,
        CadPlanePoint end,
        out double nx,
        out double ny)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= DistanceTolerance)
        {
            nx = 0.0;
            ny = 0.0;
            return false;
        }

        nx = -dy / length;
        ny = dx / length;
        return true;
    }

    private static bool TryLineIntersection(
        CadPlanePoint a,
        CadPlanePoint b,
        CadPlanePoint c,
        CadPlanePoint d,
        out CadPlanePoint point)
    {
        var rx = b.X - a.X;
        var ry = b.Y - a.Y;
        var sx = d.X - c.X;
        var sy = d.Y - c.Y;
        var denominator = rx * sy - ry * sx;
        if (Math.Abs(denominator) <= DistanceTolerance)
        {
            point = default;
            return false;
        }

        var qx = c.X - a.X;
        var qy = c.Y - a.Y;
        var t = (qx * sy - qy * sx) / denominator;
        point = new CadPlanePoint(
            a.X + rx * t,
            a.Y + ry * t);
        return double.IsFinite(point.X) &&
               double.IsFinite(point.Y);
    }

    private static double DistanceSquaredToSegment(
        CadPlanePoint point,
        CadPlanePoint start,
        CadPlanePoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= DistanceTolerance * DistanceTolerance)
        {
            var px = point.X - start.X;
            var py = point.Y - start.Y;
            return px * px + py * py;
        }

        var t =
            ((point.X - start.X) * dx +
             (point.Y - start.Y) * dy) /
            lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        var qx = start.X + dx * t;
        var qy = start.Y + dy * t;
        var ox = point.X - qx;
        var oy = point.Y - qy;
        return ox * ox + oy * oy;
    }

    private void RefreshPreviewFromLastPointer()
    {
        if (Context.Workspace.LastPointerPosition is not { } pointer)
            return;

        ShowPreview(
            Context.ResolvePoint(pointer.X, pointer.Y).Point);
    }

    private static bool TryPositive(
        string text,
        out double value)
    {
        var parsed =
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value) ||
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        return parsed &&
               double.IsFinite(value) &&
               value > DistanceTolerance;
    }

    private static bool Fail(out CadEntity entity)
    {
        entity = null!;
        return false;
    }
}
