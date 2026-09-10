using System.Drawing;
using OcctNet;

namespace OCCAD;

public sealed class CadGripManager
{
    private const int MinimumMarkerSize = 7;
    private const int MaximumMarkerSize = 31;

    private int _markerSize = 15;
    private IReadOnlyDictionary<CadGripKind, byte[]> _normalMarkerPixels =
        CreateMarkerSet(
            15,
            Color.FromArgb(235, 45, 105, 190),
            circular: false);
    private IReadOnlyDictionary<CadGripKind, byte[]> _hotMarkerPixels =
        CreateMarkerSet(
            19,
            Color.FromArgb(255, 235, 175, 35),
            circular: true);

    private readonly List<CadGripPoint> _grips = [];
    private readonly List<OcctPoint> _markers = [];
    private readonly List<CadEntity> _entities = [];
    private OcctEngine? _engine;
    private int _hotIndex = -1;

    private OcctPoint? _dragMarker;
    private byte[]? _dragMarkerPixels;
    public bool HasDragTransient => _dragMarker is not null;

    internal void ShowDragMarker(OcctPoint3d point)
    {
        if (!point.IsFinite ||
            _engine is not { IsInitialized: true } engine)
            return;

        ClearDragMarker();
        if (_dragMarker is not null)
            throw new InvalidOperationException("Previous grip drag marker could not be removed.");

        var size = Math.Min(35, HotMarkerSize + 2);
        _dragMarkerPixels ??= CreateCircularMarkerPixels(
            size,
            Color.FromArgb(255, 245, 178, 35));

        var createdMarker = engine.AddPointPixmap(
            point,
            size,
            size,
            _dragMarkerPixels);

        _dragMarker = createdMarker;
        engine.SetObjectSelectable(createdMarker, false);
        engine.SetDisplayPriority(createdMarker, 10);
    }

    internal void UpdateDragMarker(OcctPoint3d point)
    {
        if (!point.IsFinite)
            return;

        var engine = _engine;
        if (engine is not { IsInitialized: true })
            return;

        if (_dragMarker is not { } marker ||
            !engine.ContainsObject(marker.Id))
        {
            ShowDragMarker(point);
            return;
        }

        engine.UpdatePoints([
            new OcctPointStateUpdate(
                marker,
                point,
                true)
        ]);
    }

    internal void ClearDragMarker()
    {
        var marker = _dragMarker;
        if (marker is not { } existingMarker ||
            _engine is not { IsInitialized: true } engine ||
            !engine.ContainsObject(existingMarker.Id))
        {
            _dragMarker = null;
            return;
        }

        try
        {
            engine.Delete(existingMarker);
            _dragMarker = null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
        }
    }

    private static byte[] CreateCircularMarkerPixels(
        int size,
        Color color)
    {
        var pixels = new byte[size * size * 4];
        var center = size / 2;
        var radius = Math.Max(2, center - 1);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                if (dx * dx + dy * dy > radius * radius)
                    continue;

                var offset = (y * size + x) * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        return pixels;
    }

    private double _pixelTolerance = 10.0;
    public double PixelTolerance
    {
        get => _pixelTolerance;
        set
        {
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _pixelTolerance = value;
        }
    }

    public int MarkerSize
    {
        get => _markerSize;
        set
        {
            if (value < MinimumMarkerSize ||
                value > MaximumMarkerSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Grip marker size must be between {MinimumMarkerSize} and {MaximumMarkerSize} pixels.");
            }

            if (_markerSize == value)
                return;

            _markerSize = value;
            _dragMarkerPixels = null;
            _normalMarkerPixels =
                CreateMarkerSet(
                    _markerSize,
                    Color.FromArgb(235, 45, 105, 190),
                    circular: false);
            _hotMarkerPixels =
                CreateMarkerSet(
                    HotMarkerSize,
                    Color.FromArgb(255, 235, 175, 35),
                    circular: true);

            if (_engine is { IsInitialized: true } &&
                _entities.Count > 0)
            {
                RebuildMarkers();
            }
        }
    }

    public int HotMarkerSize =>
        Math.Min(
            MaximumMarkerSize + 4,
            _markerSize + 4);
    public IReadOnlyList<CadGripPoint> Grips => _grips;
    public IReadOnlyList<CadEntity> Entities => _entities;
    public CadEntity? Entity => _entities.Count == 1 ? _entities[0] : null;
    public CadGripPoint? HotGrip =>
        _hotIndex >= 0 && _hotIndex < _grips.Count
            ? _grips[_hotIndex]
            : null;

    public event EventHandler? HotChanged;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (ReferenceEquals(_engine, engine)) return;

        ClearMarkers();
        ClearDragMarker();
        _dragMarker = null;
        _engine = engine;
        if (_entities.Count > 0)
            RebuildMarkers();
    }

    public void Show(CadEntity? entity)
    {
        if (entity is null)
        {
            Clear();
            return;
        }

        Show([entity]);
    }

    public void Show(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var values = entities
            .Where(static entity => entity is not null)
            .Distinct()
            .ToArray();

        if (values.Length == 0)
        {
            Clear();
            return;
        }

        if (SameEntities(values))
        {
            RefreshPositions();
            return;
        }

        var hadHot = _hotIndex >= 0;
        _hotIndex = -1;
        UnsubscribeEntities();
        _entities.Clear();
        _entities.AddRange(values);
        SubscribeEntities();

        if (_engine is { IsInitialized: true })
            RebuildMarkers();
        else
        {
            ClearMarkers();
            RefreshGripList();
        }

        if (hadHot)
            HotChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool UpdateHot(int x, int y)
    {
        var nextIndex = FindHitIndex(x, y);
        if (nextIndex == _hotIndex)
            return nextIndex >= 0;

        var previousIndex = _hotIndex;
        _hotIndex = nextIndex;

        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();

            if (previousIndex >= 0 &&
                previousIndex < _markers.Count &&
                engine.ContainsObject(_markers[previousIndex].Id))
            {
                SetMarkerStyle(
                    engine,
                    previousIndex,
                    MarkerSize,
                    _normalMarkerPixels);
            }

            if (_hotIndex >= 0 &&
                _hotIndex < _markers.Count &&
                engine.ContainsObject(_markers[_hotIndex].Id))
            {
                SetMarkerStyle(
                    engine,
                    _hotIndex,
                    HotMarkerSize,
                    _hotMarkerPixels);
            }
        }

        HotChanged?.Invoke(this, EventArgs.Empty);
        return _hotIndex >= 0;
    }

    public bool TryHit(int x, int y, out CadGripPoint grip)
    {
        var index = FindHitIndex(x, y);
        if (index < 0)
        {
            grip = default;
            return false;
        }

        if (index != _hotIndex)
            UpdateHot(x, y);

        grip = _grips[index];
        return true;
    }

    public void ClearHot()
    {
        if (_hotIndex < 0)
            return;

        var previousIndex = _hotIndex;
        _hotIndex = -1;

        if (_engine is { IsInitialized: true } engine &&
            previousIndex < _markers.Count &&
            engine.ContainsObject(_markers[previousIndex].Id))
        {
            using var batch = engine.BeginDisplayBatch();
            SetMarkerStyle(
                engine,
                previousIndex,
                MarkerSize,
                _normalMarkerPixels);
        }

        HotChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        var hadHot = _hotIndex >= 0;
        _hotIndex = -1;
        UnsubscribeEntities();
        _entities.Clear();
        ClearMarkers();
        _grips.Clear();

        if (hadHot)
            HotChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool SameEntities(IReadOnlyList<CadEntity> values)
    {
        if (values.Count != _entities.Count)
            return false;

        for (var index = 0; index < values.Count; index++)
            if (!ReferenceEquals(values[index], _entities[index]))
                return false;

        return true;
    }

    private void SubscribeEntities()
    {
        foreach (var entity in _entities)
            entity.Changed += EntityChanged;
    }

    private void UnsubscribeEntities()
    {
        foreach (var entity in _entities)
            entity.Changed -= EntityChanged;
    }

    private void RebuildMarkers()
    {
        if (_engine is not { IsInitialized: true } engine)
        {
            RefreshGripList();
            return;
        }

        var hadHot = _hotIndex >= 0;
        _hotIndex = -1;
        ClearMarkers();
        RefreshGripList();

        using (engine.BeginDisplayBatch())
        {
            foreach (var gripPoint in _grips)
            {
                var marker = engine.AddPointPixmap(
                    gripPoint.Position,
                    MarkerSize,
                    MarkerSize,
                    _normalMarkerPixels[gripPoint.Kind]);
                engine.SetObjectSelectable(marker, false);
                engine.SetDisplayPriority(marker, 10);
                _markers.Add(marker);
            }
        }

        if (hadHot)
            HotChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshGripList()
    {
        _grips.Clear();
        foreach (var entity in _entities)
            _grips.AddRange(entity.GetWorldGripPoints());
    }

    private void RefreshPositions()
    {
        var next = new List<CadGripPoint>();
        foreach (var entity in _entities)
            next.AddRange(entity.GetWorldGripPoints());

        if (_engine is not { IsInitialized: true } engine)
        {
            _grips.Clear();
            _grips.AddRange(next);
            return;
        }

        if (next.Count != _markers.Count ||
            _markers.Any(marker =>
                !engine.ContainsObject(marker.Id)))
        {
            RebuildMarkers();
            return;
        }

        var kindsChanged = false;
        for (var index = 0; index < next.Count; index++)
            kindsChanged |= next[index].Kind != _grips[index].Kind;

        _grips.Clear();
        _grips.AddRange(next);

        var updates = new OcctPointStateUpdate[_markers.Count];
        for (var index = 0; index < _markers.Count; index++)
        {
            updates[index] = new OcctPointStateUpdate(
                _markers[index],
                _grips[index].Position,
                true);
        }

        using var batch = engine.BeginDisplayBatch();
        engine.UpdatePoints(updates);
        if (kindsChanged)
        {
            for (var index = 0; index < _markers.Count; index++)
                SetMarkerStyle(
                    engine,
                    index,
                    MarkerSize,
                    _normalMarkerPixels);

            if (_hotIndex >= 0 &&
                _hotIndex < _markers.Count)
            {
                SetMarkerStyle(
                    engine,
                    _hotIndex,
                    HotMarkerSize,
                    _hotMarkerPixels);
            }
        }
    }

    private void ClearMarkers()
    {
        if (_engine is { IsInitialized: true } engine && _markers.Count > 0)
        {
            using var batch = engine.BeginDisplayBatch();
            var existing = _markers
                .Where(marker => engine.ContainsObject(marker.Id))
                .Cast<IOcctObject>()
                .ToArray();
            if (existing.Length > 0)
                engine.Delete(existing);
        }

        _markers.Clear();
    }

    private int FindHitIndex(int x, int y)
    {
        if (_engine is not { IsInitialized: true } engine)
            return -1;

        var tolerance = PixelTolerance;
        if (!double.IsFinite(tolerance) || tolerance <= 0.0)
            throw new InvalidOperationException(
                "Grip pixel tolerance must be finite and greater than zero.");

        var limit = tolerance * tolerance;
        var best = double.PositiveInfinity;
        var bestIndex = -1;

        for (var index = 0; index < _grips.Count; index++)
        {
            var screen = engine.WorldToScreen(_grips[index].Position);
            var dx = screen.X - x;
            var dy = screen.Y - y;
            var distance = (double)dx * dx + (double)dy * dy;
            if (distance > limit || distance >= best)
                continue;

            best = distance;
            bestIndex = index;
        }

        return bestIndex;
    }

    private void EntityChanged(object? sender, CadEntityChangedEventArgs args)
    {
        if (args.Kind != CadEntityChangeKind.Geometry ||
            sender is not CadEntity entity ||
            !_entities.Contains(entity))
            return;

        RefreshPositions();
    }

    private void SetMarkerStyle(
        OcctEngine engine,
        int index,
        int size,
        IReadOnlyDictionary<CadGripKind, byte[]> styles)
    {
        engine.SetPointPixmapStyle(
            _markers[index],
            size,
            size,
            styles[_grips[index].Kind]);
    }

    private static IReadOnlyDictionary<CadGripKind, byte[]> CreateMarkerSet(
        int size,
        Color color,
        bool circular)
    {
        return Enum.GetValues<CadGripKind>()
            .ToDictionary(
                static kind => kind,
                kind => CreateMarkerPixels(
                    kind,
                    size,
                    color,
                    circular));
    }

    private static byte[] CreateMarkerPixels(
        CadGripKind kind,
        int size,
        Color color,
        bool circular)
    {
        var pixels = new byte[size * size * 4];
        var center = size / 2;
        var radius = Math.Max(2, center - 1);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var draw = circular
                    ? dx * dx + dy * dy <= radius * radius
                    : Math.Abs(dx) <= radius && Math.Abs(dy) <= radius;
                if (!draw)
                    continue;

                var offset = (y * size + x) * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        return pixels;
    }
}
