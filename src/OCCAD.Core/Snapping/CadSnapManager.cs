using System.Drawing;
using OcctNet;

namespace OCCAD;

public sealed class CadSnapManager
{
    private const int MarkerSize = 13;
    private const int MarkerDisplayPriority = 10;
    private const double PriorityTieDistancePixels = 2.0;
    private const double HysteresisPixels = 2.5;
    private const CadSnapType AllModes =
        CadSnapType.Endpoint |
        CadSnapType.Midpoint |
        CadSnapType.Center |
        CadSnapType.Vertex |
        CadSnapType.Quadrant |
        CadSnapType.Nearest |
        CadSnapType.Intersection |
        CadSnapType.Perpendicular |
        CadSnapType.Tangent;

    private static readonly IReadOnlyDictionary<CadSnapType, byte[]> MarkerPixels =
        Enum.GetValues<CadSnapType>()
            .Where(static type => type != CadSnapType.None && type != CadSnapType.Default)
            .ToDictionary(static type => type, CreateMarkerPixels);

    private readonly CadDocument _document;
    private readonly Dictionary<CadEntity, IReadOnlyList<CadSnapPoint>> _snapPointCache = [];
    private readonly List<(int X, int Y, CadSnapPoint Point)> _centerPoints = [];
    private CenterProjection? _centerProjection;
    private OcctEngine? _engine;
    private OcctPoint? _marker;
    private CadSnapType? _markerType;
    private bool _active;
    private bool _enabled = true;
    private CadSnapType _modes = CadSnapType.Default;
    private CadSnapType? _temporaryModes;
    private IReadOnlyList<CadSnapPoint> _candidates = Array.Empty<CadSnapPoint>();
    private int _currentCandidateIndex = -1;
    private int? _lastResolveX;
    private int? _lastResolveY;

    public CadSnapManager(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.Changed += DocumentChanged;
    }

    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            if (!value)
            {
                _temporaryModes = null;
                Clear();
            }
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (!value) ResetCandidateState();
        }
    }

    public CadSnapType Modes
    {
        get => _modes;
        set
        {
            ValidateModes(value, nameof(value));
            if (_modes == value) return;
            _modes = value;
            ResetCandidateState();
        }
    }

    public CadSnapType? TemporaryModes
    {
        get => _temporaryModes;
        set
        {
            if (value is { } modes)
                ValidateModes(modes, nameof(value));
            if (_temporaryModes == value) return;
            _temporaryModes = value;
            ResetCandidateState();
        }
    }

    public CadSnapType EffectiveModes => TemporaryModes ?? Modes;

    public double PixelTolerance { get; set; } = 10.0;
    public CadSnapPoint? Current { get; private set; }
    public IReadOnlyList<CadSnapPoint> Candidates => _candidates;
    public int CurrentCandidateIndex => _currentCandidateIndex;

    public event EventHandler? CurrentChanged;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (ReferenceEquals(_engine, engine))
            return;

        Clear();
        _centerProjection = null;
        _centerPoints.Clear();
        _engine = engine;
    }

    public CadSnapPoint? Resolve(
        int x,
        int y,
        OcctPoint3d queryPoint,
        CadWorkPlane workPlane,
        OcctPoint3d? reference = null,
        CadSnapResolvePolicy policy = default)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!queryPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(queryPoint));
        if (policy == default)
            policy = CadSnapResolvePolicy.KeepExactPoint;
        policy.Validate();

        var effectiveModes = EffectiveModes;
        if (!Active ||
            !Enabled ||
            effectiveModes == CadSnapType.None ||
            _engine is not { IsInitialized: true } engine)
        {
            ResetCandidateState();
            return null;
        }

        var tolerance = PixelTolerance;
        if (!double.IsFinite(tolerance) || tolerance <= 0.0)
            throw new InvalidOperationException(
                "Snap pixel tolerance must be positive.");

        try
        {
            var ranked = new List<CadSnapCandidate>();
            var order = 0;
            var limitSquared = tolerance * tolerance;
            var nearbyEntities = NearbyEntities(
                engine,
                x,
                y,
                tolerance);

            // Entity-declared snap semantics are authoritative. Native topology
            // candidates below are exact fallbacks for BRep/freeform edges.
            foreach (var entity in nearbyEntities)
            {
                foreach (var candidate in GetCachedSnapPoints(entity))
                {
                    if (candidate.Type != CadSnapType.Center)
                        Consider(candidate);
                }
            }

            if ((effectiveModes & CadSnapType.Center) != 0)
            {
                foreach (var candidate in NearbyCenters(
                             engine,
                             x,
                             y,
                             tolerance,
                             workPlane,
                             policy))
                    Consider(candidate);
            }

            foreach (var candidate in GetPrecisionCandidates(
                         queryPoint,
                         workPlane,
                         reference,
                         nearbyEntities))
                Consider(candidate);

            foreach (var candidate in GetNativeSubshapeCandidates(
                         engine,
                         x,
                         y,
                         queryPoint,
                         workPlane,
                         reference,
                         effectiveModes))
                Consider(candidate);

            ranked.Sort(static (left, right) =>
            {
                var leftDistance =
                    Math.Sqrt(left.DistanceSquared);
                var rightDistance =
                    Math.Sqrt(right.DistanceSquared);

                if (Math.Abs(leftDistance - rightDistance) >
                    PriorityTieDistancePixels)
                    return leftDistance.CompareTo(rightDistance);

                var result =
                    left.Priority.CompareTo(right.Priority);
                if (result != 0)
                    return result;

                result =
                    left.DepthDistanceSquared.CompareTo(
                        right.DepthDistanceSquared);
                if (result != 0)
                    return result;

                result =
                    left.DistanceSquared.CompareTo(
                        right.DistanceSquared);
                return result != 0
                    ? result
                    : left.Order.CompareTo(right.Order);
            });

            var nextCandidates = ranked
                .Select(static value => value.Point)
                .ToArray();
            var preserveCycle =
                _lastResolveX == x &&
                _lastResolveY == y &&
                _candidates.SequenceEqual(nextCandidates);

            var hysteresisIndex = -1;
            if (!preserveCycle &&
                Current is { } previous &&
                ranked.Count > 0)
            {
                for (var index = 0;
                     index < ranked.Count;
                     index++)
                {
                    if (!SameCandidate(
                            ranked[index].Point,
                            previous))
                        continue;

                    var bestDistance =
                        Math.Sqrt(
                            ranked[0].DistanceSquared);
                    var currentDistance =
                        Math.Sqrt(
                            ranked[index].DistanceSquared);

                    if (currentDistance <=
                        bestDistance +
                        HysteresisPixels)
                    {
                        hysteresisIndex = index;
                    }

                    break;
                }
            }

            _candidates = nextCandidates;
            _lastResolveX = x;
            _lastResolveY = y;

            if (_candidates.Count == 0)
            {
                _currentCandidateIndex = -1;
                SetCurrent(null);
                return null;
            }

            if (!preserveCycle ||
                _currentCandidateIndex < 0 ||
                _currentCandidateIndex >= _candidates.Count)
            {
                _currentCandidateIndex =
                    hysteresisIndex >= 0
                        ? hysteresisIndex
                        : 0;
            }

            var current = _candidates[_currentCandidateIndex];
            SetCurrent(current);
            return current;

            void Consider(CadSnapPoint source)
            {
                if ((source.Type & effectiveModes) == 0 ||
                    !source.Position.IsFinite)
                    return;

                var candidate = ApplyPlanePolicy(source, workPlane, policy);
                if (candidate is null) return;

                if (ranked.Any(existing =>
                        existing.Point.Type == candidate.Value.Type &&
                        existing.Point.Position.DistanceTo(
                            candidate.Value.Position) <= 1e-8))
                    return;

                var screen = engine.WorldToScreen(candidate.Value.Position);
                var dx = screen.X - x;
                var dy = screen.Y - y;
                var distanceSquared =
                    (double)dx * dx +
                    (double)dy * dy;
                if (distanceSquared > limitSquared) return;

                ranked.Add(
                    new CadSnapCandidate(
                        candidate.Value,
                        PriorityGroup(candidate.Value.Type),
                        distanceSquared,
                        candidate.Value.Position
                            .DistanceTo(queryPoint) *
                        candidate.Value.Position
                            .DistanceTo(queryPoint),
                        order++));
            }
        }
        catch (Exception exception) when (IsRecoverableSnapFailure(exception))
        {
            ResetCandidateState();
            return null;
        }
    }

    public IReadOnlyList<CadSnapPoint> GetPrecisionCandidates(
        OcctPoint3d queryPoint,
        CadWorkPlane workPlane,
        OcctPoint3d? reference = null,
        IReadOnlyCollection<CadEntity>? candidates = null)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!queryPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(queryPoint));

        return CadPrecisionSnapGeometry.GetCandidates(
            _document,
            workPlane,
            queryPoint,
            reference,
            EffectiveModes,
            candidates);
    }

    public bool CycleNext() => CycleCandidate(1);
    public bool CyclePrevious() => CycleCandidate(-1);

    public void Clear()
    {
        _candidates = Array.Empty<CadSnapPoint>();
        _currentCandidateIndex = -1;
        _lastResolveX = null;
        _lastResolveY = null;
        var changed = Current is not null;
        Current = null;
        DeleteMarker();
        if (changed)
            CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CadSnapPoint? ApplyPlanePolicy(
        CadSnapPoint candidate,
        CadWorkPlane workPlane,
        CadSnapResolvePolicy policy)
    {
        if (!workPlane.IsActive ||
            policy.PlaneMode == CadSnapPlaneMode.KeepEntityPoint)
            return candidate;

        var local = workPlane.WorldToLocal(candidate.Position);
        var projected = workPlane.LocalToWorld(local);
        var distance = candidate.Position.DistanceTo(projected);

        return distance <= policy.PlaneTolerance
            ? candidate with { WorkPlane = null }
            : null;
    }

    private bool CycleCandidate(int direction)
    {
        if (_candidates.Count <= 1) return false;
        _currentCandidateIndex =
            (_currentCandidateIndex + direction) %
            _candidates.Count;
        if (_currentCandidateIndex < 0)
            _currentCandidateIndex += _candidates.Count;
        SetCurrent(_candidates[_currentCandidateIndex]);
        return true;
    }

    private void ResetCandidateState()
    {
        _candidates = Array.Empty<CadSnapPoint>();
        _currentCandidateIndex = -1;
        _lastResolveX = null;
        _lastResolveY = null;
        SetCurrent(null);
    }

    private IReadOnlyList<CadSnapPoint> GetNativeSubshapeCandidates(
        OcctEngine engine,
        int x,
        int y,
        OcctPoint3d queryPoint,
        CadWorkPlane workPlane,
        OcctPoint3d? reference,
        CadSnapType effectiveModes)
    {
        var wantsVertex =
            (effectiveModes &
             (CadSnapType.Endpoint |
              CadSnapType.Vertex)) != 0;
        var wantsEdge =
            (effectiveModes &
             (CadSnapType.Endpoint |
              CadSnapType.Midpoint |
              CadSnapType.Center |
              CadSnapType.Quadrant |
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
              CadSnapType.Quadrant |
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

                // A closest endpoint is not a perpendicular foot unless the
                // endpoint tangent is also perpendicular to the reference ray.
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

            var wantsCircle =
                (modes &
                 (CadSnapType.Center |
                  CadSnapType.Quadrant)) != 0;
            if (wantsCircle &&
                engine.GetEdgeCurveType(edge) == OcctCurveType.Circle &&
                TryGetCircularEdgeFrame(
                    engine,
                    owner,
                    edgeIndex,
                    out var circle))
            {
                if ((modes & CadSnapType.Center) != 0)
                {
                    AddUnique(
                        result,
                        new CadSnapPoint(
                            entity,
                            circle.Center,
                            CadSnapType.Center,
                            edgeIndex));
                }

                if ((modes & CadSnapType.Quadrant) != 0)
                {
                    foreach (var quadrant in CircularQuadrants(circle))
                    {
                        var projection =
                            engine.ProjectPointToEdge(edge, quadrant);
                        if (projection.Distance > 1e-7)
                            continue;

                        AddUnique(
                            result,
                            new CadSnapPoint(
                                entity,
                                quadrant,
                                CadSnapType.Quadrant,
                                edgeIndex));
                    }
                }
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

        // These are exact OCCT curve evaluations, not tessellation samples. Three
        // parameters are sufficient to reconstruct the analytic circle frame.
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

    private static IEnumerable<OcctPoint3d> CircularQuadrants(
        CircularEdgeFrame circle)
    {
        yield return circle.Center + circle.XAxis * circle.Radius;
        yield return circle.Center + circle.YAxis * circle.Radius;
        yield return circle.Center - circle.XAxis * circle.Radius;
        yield return circle.Center - circle.YAxis * circle.Radius;
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
        catch (Exception exception) when (IsRecoverableSnapFailure(exception))
        {
        }
    }

    private IReadOnlyList<CadEntity> NearbyEntities(
        OcctEngine engine,
        int x,
        int y,
        double tolerance)
    {
        var radius = Math.Max(
            2,
            (int)Math.Ceiling(tolerance) + 2);
        var objects = engine.QueryRectangle(
            x - radius,
            y - radius,
            x + radius,
            y + radius,
            allowOverlap: true);
        if (objects.Count == 0)
            return Array.Empty<CadEntity>();

        return objects
            .Select(_document.FindByViewerObject)
            .OfType<CadEntity>()
            .Where(_document.IsEntitySelectable)
            .Distinct()
            .ToArray();
    }

    // Centers may lie far from any selectable edge (circle, arc, rectangle, etc.).
    // Keep their projected positions independently of native edge picking.
    private IEnumerable<CadSnapPoint> NearbyCenters(
        OcctEngine engine,
        int x,
        int y,
        double tolerance,
        CadWorkPlane plane,
        CadSnapResolvePolicy policy)
    {
        var camera = engine.GetCamera();
        var viewport = engine.GetViewportState();
        var projection = new CenterProjection(
            camera.Eye,
            camera.Center,
            camera.Up,
            camera.Direction,
            camera.Scale,
            viewport.Width,
            viewport.Height,
            viewport.ProjectionType,
            viewport.PerspectiveFieldOfView,
            plane.IsActive,
            plane.Origin,
            plane.XAxis,
            plane.YAxis,
            policy.PlaneMode,
            policy.PlaneTolerance);
        if (_centerProjection != projection)
        {
            _centerProjection = null;
            _centerPoints.Clear();
            foreach (var entity in _document.Entities)
            {
                if (!_document.IsEntitySelectable(entity))
                    continue;

                foreach (var point in GetCachedSnapPoints(entity))
                {
                    if (point.Type != CadSnapType.Center ||
                        !point.Position.IsFinite)
                        continue;

                    var candidate = ApplyPlanePolicy(point, plane, policy);
                    if (candidate is null) continue;
                    if (viewport.ProjectionType == OcctProjectionType.Perspective &&
                        (candidate.Value.Position - camera.Eye)
                            .Dot(camera.Center - camera.Eye) <= 0)
                        continue;

                    var screen = engine.WorldToScreen(candidate.Value.Position);
                    _centerPoints.Add((screen.X, screen.Y, candidate.Value));
                }
            }
            _centerPoints.Sort(
                static (left, right) => left.X.CompareTo(right.X));
            _centerProjection = projection;
        }

        var low = 0;
        var high = _centerPoints.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_centerPoints[middle].X < x - tolerance)
                low = middle + 1;
            else
                high = middle;
        }

        for (var index = low; index < _centerPoints.Count; index++)
        {
            var center = _centerPoints[index];
            if (center.X > x + tolerance) break;
            if (Math.Abs((double)center.Y - y) <= tolerance)
                yield return center.Point;
        }
    }

    private readonly record struct NativeEdgeHit(
        OcctShape Owner,
        CadEntity Entity,
        int EdgeIndex);

    private sealed record CenterProjection(
        OcctPoint3d Eye,
        OcctPoint3d Center,
        OcctVector3d Up,
        OcctVector3d Direction,
        double Scale,
        int Width,
        int Height,
        OcctProjectionType Projection,
        double FieldOfView,
        bool PlaneActive,
        OcctPoint3d PlaneOrigin,
        OcctVector3d PlaneX,
        OcctVector3d PlaneY,
        CadSnapPlaneMode PlaneMode,
        double PlaneTolerance);

    private readonly record struct CircularEdgeFrame(
        OcctPoint3d Center,
        OcctVector3d Normal,
        OcctVector3d XAxis,
        OcctVector3d YAxis,
        double Radius);

    private IReadOnlyList<CadSnapPoint> GetCachedSnapPoints(CadEntity entity)
    {
        if (_snapPointCache.TryGetValue(entity, out var points))
            return points;

        points = entity.GetWorldSnapPoints();
        _snapPointCache[entity] = points;
        return points;
    }

    private void DocumentChanged(
        object? sender,
        CadDocumentChangedEventArgs args)
    {
        _centerProjection = null;
        _centerPoints.Clear();
        if (args.Entity is { } entity)
            _snapPointCache.Remove(entity);
        else
            _snapPointCache.Clear();

        if (args.Entity is null ||
            _candidates.Any(point =>
                ReferenceEquals(point.Entity, args.Entity)))
            ResetCandidateState();
    }

    private static bool SameCandidate(
        CadSnapPoint left,
        CadSnapPoint right) =>
        ReferenceEquals(left.Entity, right.Entity) &&
        left.Type == right.Type &&
        left.Index == right.Index &&
        left.Position.DistanceTo(
            right.Position) <= 1e-8;

    internal static int PriorityGroup(CadSnapType type) => type switch
    {
        CadSnapType.Endpoint or CadSnapType.Vertex => 0,
        CadSnapType.Intersection => 1,
        CadSnapType.Midpoint => 2,
        CadSnapType.Center => 3,
        CadSnapType.Quadrant => 4,
        CadSnapType.Perpendicular => 5,
        CadSnapType.Tangent => 6,
        CadSnapType.Nearest => 7,
        _ => 8
    };

    private static void ValidateModes(
        CadSnapType value,
        string parameterName)
    {
        var raw = (int)value;
        var allowed = (int)AllModes;
        if ((raw & ~allowed) != 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private readonly record struct CadSnapCandidate(
        CadSnapPoint Point,
        int Priority,
        double DistanceSquared,
        double DepthDistanceSquared,
        int Order);

    private void SetCurrent(CadSnapPoint? value)
    {
        if (Nullable.Equals(Current, value)) return;
        Current = value;
        try
        {
            if (value is { } snap)
                ShowMarker(snap.Position, snap.Type);
            else
                HideMarker();
        }
        catch (Exception exception) when (IsRecoverableSnapFailure(exception))
        {
            DeleteMarker();
        }
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowMarker(
        OcctPoint3d position,
        CadSnapType type)
    {
        if (_engine is not { IsInitialized: true } engine)
            return;

        var pixels = MarkerPixels.TryGetValue(type, out var value)
            ? value
            : CreateMarkerPixels(CadSnapType.Endpoint);

        using var batch = engine.BeginDisplayBatch();
        if (_marker is { } marker &&
            engine.ContainsObject(marker.Id))
        {
            engine.UpdatePoints(
                [new OcctPointStateUpdate(marker, position, true)]);
            if (_markerType != type)
            {
                engine.SetPointPixmapStyle(
                    marker,
                    MarkerSize,
                    MarkerSize,
                    pixels);
                _markerType = type;
            }
            engine.SetObjectVisible(marker, true);
            return;
        }

        _marker = engine.AddPointPixmap(
            position,
            MarkerSize,
            MarkerSize,
            pixels);
        _markerType = type;
        engine.SetObjectSelectable(_marker.Value, false);
        engine.SetDisplayPriority(
            _marker.Value,
            MarkerDisplayPriority);
    }

    private void HideMarker()
    {
        if (_marker is not { } marker) return;
        if (_engine is { IsInitialized: true } engine &&
            engine.ContainsObject(marker.Id))
            engine.SetObjectVisible(marker, false);
    }

    private void DeleteMarker()
    {
        if (_marker is not { } marker) return;
        try
        {
            if (_engine is { IsInitialized: true } engine &&
                engine.ContainsObject(marker.Id))
            {
                using var batch = engine.BeginDisplayBatch();
                engine.Delete(marker);
            }
        }
        catch (Exception exception) when (IsRecoverableSnapFailure(exception))
        {
        }
        finally
        {
            _marker = null;
            _markerType = null;
        }
    }

    private static bool IsRecoverableSnapFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static byte[] CreateMarkerPixels(CadSnapType type)
    {
        var pixels = new byte[MarkerSize * MarkerSize * 4];
        var color = Color.FromArgb(238, 220, 45, 45);
        var center = MarkerSize / 2;

        for (var y = 0; y < MarkerSize; y++)
        {
            for (var x = 0; x < MarkerSize; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var fill = type switch
                {
                    CadSnapType.Endpoint =>
                        Math.Abs(dx) <= 3 &&
                        Math.Abs(dy) <= 3,
                    CadSnapType.Midpoint =>
                        dy >= -4 &&
                        dy <= 4 &&
                        Math.Abs(dx) <= dy + 4,
                    CadSnapType.Center =>
                        dx * dx + dy * dy <= 13,
                    CadSnapType.Vertex =>
                        Math.Abs(dx) + Math.Abs(dy) <= 5,
                    CadSnapType.Quadrant =>
                        Math.Abs(dx) <= 2 ||
                        Math.Abs(dy) <= 2,
                    CadSnapType.Intersection =>
                        Math.Abs(dx - dy) <= 1 ||
                        Math.Abs(dx + dy) <= 1,
                    CadSnapType.Perpendicular =>
                        (Math.Abs(dx + 3) <= 1 &&
                         dy >= -4 && dy <= 4) ||
                        (dx >= -3 && dx <= 4 &&
                         Math.Abs(dy - 3) <= 1),
                    CadSnapType.Tangent =>
                        (Math.Abs(dy + 3) <= 1 &&
                         dx >= -4 && dx <= 4) ||
                        (Math.Abs(dx) <= 1 &&
                         dy >= -3 && dy <= 4),
                    CadSnapType.Nearest =>
                        dx * dx + dy * dy <= 5,
                    _ =>
                        Math.Abs(dx) <= 2 &&
                        Math.Abs(dy) <= 2
                };
                if (!fill) continue;

                var offset =
                    (y * MarkerSize + x) * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        return pixels;
    }
}
