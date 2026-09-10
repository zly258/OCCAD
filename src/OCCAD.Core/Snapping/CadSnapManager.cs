using System.Drawing;
using OcctNet;

namespace OCCAD;

public sealed class CadSnapManager
{
    private const int MarkerSize = 13;
    private const int MarkerDisplayPriority = 10;
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
    private CadSnapPlaneMode _planeMode = CadSnapPlaneMode.KeepEntityPoint;
    private double _planeTolerance = 1e-6;

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

    public CadSnapPlaneMode PlaneMode
    {
        get => _planeMode;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_planeMode == value) return;
            _planeMode = value;
            ResetCandidateState();
        }
    }

    public double PlaneTolerance
    {
        get => _planeTolerance;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_planeTolerance.Equals(value)) return;
            _planeTolerance = value;
            ResetCandidateState();
        }
    }

    public double PixelTolerance { get; set; } = 10.0;
    public CadSnapPoint? Current { get; private set; }
    public IReadOnlyList<CadSnapPoint> Candidates => _candidates;
    public int CurrentCandidateIndex => _currentCandidateIndex;

    public event EventHandler? CurrentChanged;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        DeleteMarker();
        _centerProjection = null;
        _engine = engine;
        _marker = null;
        _markerType = null;
        Current = null;
    }

    public CadSnapPoint? Resolve(
        int x,
        int y,
        OcctPoint3d queryPoint,
        CadWorkPlane workPlane,
        OcctPoint3d? reference = null)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!queryPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(queryPoint));

        var effectiveModes = EffectiveModes;
        if (!Active || !Enabled || effectiveModes == CadSnapType.None || _engine is not { IsInitialized: true } engine)
        {
            ResetCandidateState();
            return null;
        }

        var tolerance = PixelTolerance;
        if (!double.IsFinite(tolerance) || tolerance <= 0.0)
            throw new InvalidOperationException("Snap pixel tolerance must be positive.");

        try
        {
            var ranked = new List<CadSnapCandidate>();
            var order = 0;
            var limitSquared = tolerance * tolerance;
            var nearbyEntities = NearbyEntities(engine, x, y, tolerance);

            foreach (var entity in nearbyEntities)
            {
                foreach (var candidate in GetCachedSnapPoints(entity))
                    if (candidate.Type != CadSnapType.Center) Consider(candidate);
            }

            if ((effectiveModes & CadSnapType.Center) != 0)
                foreach (var candidate in NearbyCenters(engine, x, y, tolerance, workPlane))
                    Consider(candidate);

            foreach (var candidate in GetPrecisionCandidates(queryPoint, workPlane, reference, nearbyEntities))
                Consider(candidate);

            ranked.Sort(static (left, right) =>
            {
                var result = left.Priority.CompareTo(right.Priority);
                if (result != 0) return result;
                result = left.DistanceSquared.CompareTo(right.DistanceSquared);
                return result != 0 ? result : left.Order.CompareTo(right.Order);
            });

            var nextCandidates = ranked.Select(static value => value.Point).ToArray();
            var preserveCycle = _lastResolveX == x && _lastResolveY == y && _candidates.SequenceEqual(nextCandidates);
            _candidates = nextCandidates;
            _lastResolveX = x;
            _lastResolveY = y;

            if (_candidates.Count == 0)
            {
                _currentCandidateIndex = -1;
                SetCurrent(null);
                return null;
            }

            if (!preserveCycle || _currentCandidateIndex < 0 || _currentCandidateIndex >= _candidates.Count)
                _currentCandidateIndex = 0;

            var current = _candidates[_currentCandidateIndex];
            SetCurrent(current);
            return current;

            void Consider(CadSnapPoint source)
            {
                if ((source.Type & effectiveModes) == 0 || !source.Position.IsFinite) return;
                var candidate = ApplyPlanePolicy(source, workPlane);
                if (candidate is null) return;

                var screen = engine.WorldToScreen(candidate.Value.Position);
                var dx = screen.X - x;
                var dy = screen.Y - y;
                var distanceSquared = (double)dx * dx + (double)dy * dy;
                if (distanceSquared > limitSquared) return;
                ranked.Add(new CadSnapCandidate(candidate.Value, PriorityGroup(candidate.Value.Type), distanceSquared, order++));
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
        if (!queryPoint.IsFinite) throw new ArgumentOutOfRangeException(nameof(queryPoint));

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
        if (changed) CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private CadSnapPoint? ApplyPlanePolicy(CadSnapPoint candidate, CadWorkPlane workPlane)
    {
        if (!workPlane.IsActive || PlaneMode == CadSnapPlaneMode.KeepEntityPoint)
            return candidate;

        var local = workPlane.WorldToLocal(candidate.Position);
        var projected = workPlane.LocalToWorld(local);
        var distance = candidate.Position.DistanceTo(projected);

        if (PlaneMode == CadSnapPlaneMode.RequireOnWorkPlane)
            return distance <= PlaneTolerance
                ? candidate with { WorkPlane = null }
                : null;

        return candidate with
        {
            Position = projected,
            WorkPlane = null
        };
    }

    private bool CycleCandidate(int direction)
    {
        if (_candidates.Count <= 1) return false;
        _currentCandidateIndex = (_currentCandidateIndex + direction) % _candidates.Count;
        if (_currentCandidateIndex < 0) _currentCandidateIndex += _candidates.Count;
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

    private IReadOnlyList<CadEntity> NearbyEntities(OcctEngine engine, int x, int y, double tolerance)
    {
        var radius = Math.Max(2, (int)Math.Ceiling(tolerance) + 2);
        var objects = engine.QueryRectangle(x - radius, y - radius, x + radius, y + radius, allowOverlap: true);
        if (objects.Count == 0) return Array.Empty<CadEntity>();

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
        OcctEngine engine, int x, int y, double tolerance, CadWorkPlane plane)
    {
        var camera = engine.GetCamera();
        var viewport = engine.GetViewportState();
        var projection = new CenterProjection(camera.Eye, camera.Center, camera.Up, camera.Direction,
            camera.Scale, viewport.Width, viewport.Height, viewport.ProjectionType,
            viewport.PerspectiveFieldOfView, plane.IsActive, plane.Origin, plane.XAxis, plane.YAxis,
            PlaneMode, PlaneTolerance);
        if (_centerProjection != projection)
        {
            _centerProjection = null;
            _centerPoints.Clear();
            foreach (var entity in _document.Entities)
            {
                if (!_document.IsEntitySelectable(entity)) continue;
                foreach (var point in GetCachedSnapPoints(entity))
                {
                    if (point.Type != CadSnapType.Center || !point.Position.IsFinite) continue;
                    var candidate = ApplyPlanePolicy(point, plane);
                    if (candidate is null) continue;
                    if (viewport.ProjectionType == OcctProjectionType.Perspective &&
                        (candidate.Value.Position - camera.Eye).Dot(camera.Center - camera.Eye) <= 0) continue;
                    var screen = engine.WorldToScreen(candidate.Value.Position);
                    _centerPoints.Add((screen.X, screen.Y, candidate.Value));
                }
            }
            _centerPoints.Sort(static (left, right) => left.X.CompareTo(right.X));
            _centerProjection = projection;
        }

        var low = 0;
        var high = _centerPoints.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_centerPoints[middle].X < x - tolerance) low = middle + 1;
            else high = middle;
        }
        for (var index = low; index < _centerPoints.Count; index++)
        {
            var center = _centerPoints[index];
            if (center.X > x + tolerance) break;
            if (Math.Abs((double)center.Y - y) <= tolerance) yield return center.Point;
        }
    }

    private sealed record CenterProjection(
        OcctPoint3d Eye, OcctPoint3d Center, OcctVector3d Up, OcctVector3d Direction,
        double Scale, int Width, int Height, OcctProjectionType Projection, double FieldOfView,
        bool PlaneActive, OcctPoint3d PlaneOrigin, OcctVector3d PlaneX, OcctVector3d PlaneY,
        CadSnapPlaneMode PlaneMode, double PlaneTolerance);

    private IReadOnlyList<CadSnapPoint> GetCachedSnapPoints(CadEntity entity)
    {
        if (_snapPointCache.TryGetValue(entity, out var points)) return points;
        points = entity.GetSnapPoints();
        _snapPointCache[entity] = points;
        return points;
    }

    private void DocumentChanged(object? sender, CadDocumentChangedEventArgs args)
    {
        _centerProjection = null;
        _centerPoints.Clear();
        if (args.Entity is { } entity) _snapPointCache.Remove(entity);
        else _snapPointCache.Clear();

        // A candidate must never outlive the geometry or visibility it came from.
        if (args.Entity is null || _candidates.Any(point => ReferenceEquals(point.Entity, args.Entity)))
            ResetCandidateState();
    }

    internal static int PriorityGroup(CadSnapType type) => type switch
    {
        CadSnapType.Intersection => 0,
        CadSnapType.Endpoint or CadSnapType.Vertex or CadSnapType.Midpoint or CadSnapType.Center or CadSnapType.Quadrant => 1,
        CadSnapType.Perpendicular or CadSnapType.Tangent => 2,
        CadSnapType.Nearest => 3,
        _ => 4
    };

    private static void ValidateModes(CadSnapType value, string parameterName)
    {
        var raw = (int)value;
        var allowed = (int)AllModes;
        if ((raw & ~allowed) != 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private readonly record struct CadSnapCandidate(CadSnapPoint Point, int Priority, double DistanceSquared, int Order);

    private void SetCurrent(CadSnapPoint? value)
    {
        if (Nullable.Equals(Current, value)) return;
        Current = value;
        try
        {
            if (value is { } snap) ShowMarker(snap.Position, snap.Type);
            else HideMarker();
        }
        catch (Exception exception) when (IsRecoverableSnapFailure(exception))
        {
            DeleteMarker();
        }
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowMarker(OcctPoint3d position, CadSnapType type)
    {
        if (_engine is not { IsInitialized: true } engine) return;
        var pixels = MarkerPixels.TryGetValue(type, out var value)
            ? value
            : CreateMarkerPixels(CadSnapType.Endpoint);

        using var batch = engine.BeginDisplayBatch();
        if (_marker is { } marker && engine.ContainsObject(marker.Id))
        {
            engine.UpdatePoints([new OcctPointStateUpdate(marker, position, true)]);
            if (_markerType != type)
            {
                engine.SetPointPixmapStyle(marker, MarkerSize, MarkerSize, pixels);
                _markerType = type;
            }
            engine.SetObjectVisible(marker, true);
            return;
        }

        _marker = engine.AddPointPixmap(position, MarkerSize, MarkerSize, pixels);
        _markerType = type;
        engine.SetObjectSelectable(_marker.Value, false);
        engine.SetDisplayPriority(_marker.Value, MarkerDisplayPriority);
    }

    private void HideMarker()
    {
        if (_marker is not { } marker) return;
        if (_engine is { IsInitialized: true } engine && engine.ContainsObject(marker.Id))
            engine.SetObjectVisible(marker, false);
    }

    private void DeleteMarker()
    {
        if (_marker is not { } marker) return;
        try
        {
            if (_engine is { IsInitialized: true } engine && engine.ContainsObject(marker.Id))
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
                    CadSnapType.Endpoint => Math.Abs(dx) <= 3 && Math.Abs(dy) <= 3,
                    CadSnapType.Midpoint => dy >= -4 && dy <= 4 && Math.Abs(dx) <= dy + 4,
                    CadSnapType.Center => dx * dx + dy * dy <= 13,
                    CadSnapType.Vertex => Math.Abs(dx) + Math.Abs(dy) <= 5,
                    CadSnapType.Quadrant => Math.Abs(dx) <= 2 || Math.Abs(dy) <= 2,
                    CadSnapType.Intersection => Math.Abs(dx - dy) <= 1 || Math.Abs(dx + dy) <= 1,
                    CadSnapType.Perpendicular => (Math.Abs(dx + 3) <= 1 && dy >= -4 && dy <= 4) || (dx >= -3 && dx <= 4 && Math.Abs(dy - 3) <= 1),
                    CadSnapType.Tangent => (Math.Abs(dy + 3) <= 1 && dx >= -4 && dx <= 4) || (Math.Abs(dx) <= 1 && dy >= -3 && dy <= 4),
                    CadSnapType.Nearest => dx * dx + dy * dy <= 5,
                    _ => Math.Abs(dx) <= 2 && Math.Abs(dy) <= 2
                };
                if (!fill) continue;
                var offset = (y * MarkerSize + x) * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        return pixels;
    }
}
