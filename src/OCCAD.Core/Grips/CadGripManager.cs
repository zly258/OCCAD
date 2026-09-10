using OcctNet;

namespace OCCAD;

public sealed class CadGripManager
{
    private readonly CadGripMarkerPresenter _presenter = new();
    private readonly List<CadGripPoint> _grips = [];
    private readonly List<CadEntity> _entities = [];
    private OcctEngine? _engine;
    private int _hotIndex = -1;
    private double _pixelTolerance = 10.0;

    public bool HasDragTransient => _presenter.HasDragTransient;

    internal void ShowDragMarker(OcctPoint3d point) =>
        _presenter.ShowDragMarker(point);

    internal void UpdateDragMarker(OcctPoint3d point) =>
        _presenter.UpdateDragMarker(point);

    internal void ClearDragMarker() =>
        _presenter.ClearDragMarker();

    public double PixelTolerance
    {
        get => _pixelTolerance;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _pixelTolerance = value;
        }
    }

    public int MarkerSize
    {
        get => _presenter.MarkerSize;
        set
        {
            var previous = _presenter.MarkerSize;
            _presenter.MarkerSize = value;
            if (previous == value)
                return;

            if (_engine is { IsInitialized: true } &&
                _entities.Count > 0)
            {
                RebuildMarkers();
            }
        }
    }

    public int HotMarkerSize => _presenter.HotMarkerSize;
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
        if (ReferenceEquals(_engine, engine))
            return;

        _presenter.AttachEngine(engine);
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
            _presenter.ClearMarkers();
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
        _presenter.SetHot(
            previousIndex,
            _hotIndex,
            _grips);

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
        _presenter.SetHot(
            previousIndex,
            currentIndex: -1,
            _grips);
        HotChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        var hadHot = _hotIndex >= 0;
        _hotIndex = -1;
        UnsubscribeEntities();
        _entities.Clear();
        _presenter.ClearMarkers();
        _grips.Clear();

        if (hadHot)
            HotChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool SameEntities(IReadOnlyList<CadEntity> values)
    {
        if (values.Count != _entities.Count)
            return false;

        for (var index = 0; index < values.Count; index++)
        {
            if (!ReferenceEquals(values[index], _entities[index]))
                return false;
        }

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
        if (_engine is not { IsInitialized: true })
        {
            RefreshGripList();
            return;
        }

        var hadHot = _hotIndex >= 0;
        _hotIndex = -1;
        RefreshGripList();
        _presenter.Rebuild(_grips);

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

        if (_engine is not { IsInitialized: true })
        {
            _grips.Clear();
            _grips.AddRange(next);
            return;
        }

        var kindsChanged = next.Count != _grips.Count;
        if (!kindsChanged)
        {
            for (var index = 0; index < next.Count; index++)
            {
                if (next[index].Kind == _grips[index].Kind)
                    continue;

                kindsChanged = true;
                break;
            }
        }

        _grips.Clear();
        _grips.AddRange(next);

        if (!_presenter.TryUpdatePositions(
                _grips,
                kindsChanged,
                _hotIndex))
        {
            RebuildMarkers();
        }
    }

    private int FindHitIndex(int x, int y)
    {
        if (_engine is not { IsInitialized: true } engine)
            return -1;

        var tolerance = PixelTolerance;
        if (!double.IsFinite(tolerance) || tolerance <= 0.0)
        {
            throw new InvalidOperationException(
                "Grip pixel tolerance must be finite and greater than zero.");
        }

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

    private void EntityChanged(
        object? sender,
        CadEntityChangedEventArgs args)
    {
        if (args.Kind != CadEntityChangeKind.Geometry ||
            sender is not CadEntity entity ||
            !_entities.Contains(entity))
            return;

        RefreshPositions();
    }
}
