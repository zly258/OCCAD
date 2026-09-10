using System.Drawing;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Coordinates object-snap semantics: enabled modes, candidate collection,
/// ranking, hysteresis, cycling and the current snap. Native topology queries
/// and marker presentation are delegated to dedicated collaborators.
/// </summary>
public sealed class CadSnapManager
{
    private const double HysteresisPixels = 2.5;

    private const CadSnapType RuntimeModes =
        CadSnapType.Endpoint |
        CadSnapType.Midpoint |
        CadSnapType.Center |
        CadSnapType.Vertex |
        CadSnapType.Quadrant |
        CadSnapType.Nearest |
        CadSnapType.Intersection |
        CadSnapType.Perpendicular |
        CadSnapType.Tangent |
        CadSnapType.ApparentIntersection |
        CadSnapType.Extension |
        CadSnapType.Insertion |
        CadSnapType.Node;

    private const CadSnapType CompatibleModes = RuntimeModes;

    private readonly CadDocument _document;
    private readonly CadNativeSnapProvider _nativeProvider;
    private readonly CadSnapMarkerPresenter _presenter = new();
    private readonly Dictionary<CadEntity, IReadOnlyList<CadSnapPoint>> _snapPointCache = [];
    private readonly List<(int X, int Y, CadSnapPoint Point)> _centerPoints = [];

    private CenterProjection? _centerProjection;
    private OcctEngine? _engine;
    private bool _active;
    private bool _enabled = true;
    private CadSnapType _modes = CadSnapType.Default;
    private CadSnapType? _temporaryModes;
    private IReadOnlyList<CadSnapPoint> _candidates = Array.Empty<CadSnapPoint>();
    private int _currentCandidateIndex = -1;
    private int? _lastResolveX;
    private int? _lastResolveY;
    private double _pixelTolerance = 10.0;

    public CadSnapManager(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _nativeProvider = new CadNativeSnapProvider(_document);
        _document.Changed += DocumentChanged;
    }

    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value)
                return;

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
            if (_enabled == value)
                return;

            _enabled = value;
            if (!value)
                ResetCandidateState();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public CadSnapType Modes
    {
        get => _modes;
        set
        {
            ValidateModes(value, nameof(value));
            var normalized = NormalizeModes(value);
            if (_modes == normalized)
                return;

            _modes = normalized;
            ResetCandidateState();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public CadSnapType? TemporaryModes
    {
        get => _temporaryModes;
        set
        {
            CadSnapType? normalized = null;
            if (value is { } modes)
            {
                ValidateModes(modes, nameof(value));
                normalized = NormalizeModes(modes);
            }

            if (_temporaryModes == normalized)
                return;

            _temporaryModes = normalized;
            ResetCandidateState();
        }
    }

    public CadSnapType EffectiveModes =>
        NormalizeModes(TemporaryModes ?? Modes);

    public double PixelTolerance
    {
        get => _pixelTolerance;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_pixelTolerance.Equals(value))
                return;

            _pixelTolerance = value;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int MarkerSize
    {
        get => _presenter.MarkerSize;
        set
        {
            if (_presenter.MarkerSize == value)
                return;

            _presenter.MarkerSize = value;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Color MarkerColor
    {
        get => _presenter.MarkerColor;
        set => _presenter.MarkerColor = value;
    }

    public bool HasTransient =>
        Current is not null ||
        _presenter.HasMarker ||
        _candidates.Count > 0;

    public CadSnapPoint? Current { get; private set; }
    public IReadOnlyList<CadSnapPoint> Candidates => _candidates;
    public int CurrentCandidateIndex => _currentCandidateIndex;

    public event EventHandler? CurrentChanged;
    public event EventHandler? SettingsChanged;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (ReferenceEquals(_engine, engine))
            return;

        Clear();
        _centerProjection = null;
        _centerPoints.Clear();
        _engine = engine;
        _presenter.AttachEngine(engine);
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
            // is an exact fallback for BRep/freeform edges.
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

            foreach (var candidate in _nativeProvider.GetCandidates(
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
                var result = left.Priority.CompareTo(right.Priority);
                if (result != 0)
                    return result;

                result = left.DistanceSquared.CompareTo(right.DistanceSquared);
                if (result != 0)
                    return result;

                result = left.DepthDistanceSquared.CompareTo(right.DepthDistanceSquared);
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
                        Math.Sqrt(ranked[0].DistanceSquared);
                    var currentDistance =
                        Math.Sqrt(ranked[index].DistanceSquared);

                    if (ranked[index].Priority == ranked[0].Priority &&
                        currentDistance <= bestDistance + HysteresisPixels)
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

                var candidate = ApplyPlanePolicy(
                    source,
                    workPlane,
                    policy);
                if (candidate is null)
                    return;

                if (ranked.Any(existing =>
                        existing.Point.Type == candidate.Value.Type &&
                        existing.Point.Position.DistanceTo(
                            candidate.Value.Position) <= 1e-8))
                    return;

                var screen =
                    engine.WorldToScreen(candidate.Value.Position);
                var dx = screen.X - x;
                var dy = screen.Y - y;
                var distanceSquared =
                    (double)dx * dx +
                    (double)dy * dy;
                if (distanceSquared > limitSquared)
                    return;

                var depthDistance =
                    candidate.Value.Position.DistanceTo(queryPoint);
                ranked.Add(
                    new CadSnapCandidate(
                        candidate.Value,
                        PriorityGroup(candidate.Value.Type),
                        distanceSquared,
                        depthDistance * depthDistance,
                        order++));
            }
        }
        catch (Exception exception) when (IsRecoverableSnapFailure(exception))
        {
            try
            {
                ResetCandidateState();
            }
            catch (Exception cleanupFailure)
                when (IsRecoverableSnapFailure(cleanupFailure))
            {
                throw new AggregateException(
                    "Snap resolution and transient cleanup both failed.",
                    exception,
                    cleanupFailure);
            }

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
        _presenter.Clear();

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
        if (_candidates.Count <= 1)
            return false;

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

    // Centers may lie far from any selectable edge. Their projected positions
    // are indexed independently of native edge detection.
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

                    var candidate = ApplyPlanePolicy(
                        point,
                        plane,
                        policy);
                    if (candidate is null)
                        continue;

                    if (viewport.ProjectionType ==
                            OcctProjectionType.Perspective &&
                        (candidate.Value.Position - camera.Eye)
                            .Dot(camera.Center - camera.Eye) <= 0)
                        continue;

                    var screen =
                        engine.WorldToScreen(candidate.Value.Position);
                    _centerPoints.Add(
                        (screen.X, screen.Y, candidate.Value));
                }
            }

            _centerPoints.Sort(
                static (left, right) =>
                    left.X.CompareTo(right.X));
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

        for (var index = low;
             index < _centerPoints.Count;
             index++)
        {
            var center = _centerPoints[index];
            if (center.X > x + tolerance)
                break;
            if (Math.Abs((double)center.Y - y) <= tolerance)
                yield return center.Point;
        }
    }

    private IReadOnlyList<CadSnapPoint> GetCachedSnapPoints(
        CadEntity entity)
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
        left.Position.DistanceTo(right.Position) <= 1e-8;

    internal static int PriorityGroup(CadSnapType type) =>
        type switch
        {
            CadSnapType.Endpoint or
                CadSnapType.Vertex or
                CadSnapType.Intersection => 0,
            CadSnapType.Midpoint or
                CadSnapType.Center => 1,
            CadSnapType.Perpendicular or
                CadSnapType.Tangent => 2,
            CadSnapType.Quadrant or
                CadSnapType.Extension or
                CadSnapType.Insertion or
                CadSnapType.Node or
                CadSnapType.ApparentIntersection => 3,
            CadSnapType.Nearest => 4,
            _ => 12
        };

    private static CadSnapType NormalizeModes(CadSnapType value) =>
        value & RuntimeModes;

    private static void ValidateModes(
        CadSnapType value,
        string parameterName)
    {
        var raw = (int)value;
        var allowed = (int)CompatibleModes;
        if ((raw & ~allowed) != 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private void SetCurrent(CadSnapPoint? value)
    {
        if (Nullable.Equals(Current, value))
        {
            if (value is { } current)
            {
                _presenter.Show(
                    current.Position,
                    current.Type);
            }

            return;
        }

        Current = value;
        if (value is { } snap)
        {
            _presenter.Show(
                snap.Position,
                snap.Type);
        }
        else
        {
            _presenter.Hide();
        }

        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsRecoverableSnapFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private readonly record struct CadSnapCandidate(
        CadSnapPoint Point,
        int Priority,
        double DistanceSquared,
        double DepthDistanceSquared,
        int Order);

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
}
