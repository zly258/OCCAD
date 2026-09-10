using OcctNet;

namespace OCCAD;

public enum CadSelectionOperation
{
    Replace,
    Add,
    Remove,
    Toggle
}

public enum CadEntityFilterKind
{
    All = 0,
    Point = 1,
    Curve = 2,
    Region = 3,
    Solid = 4
}

public sealed class CadEntityFilter
{
    private readonly Func<CadEntity, bool> _predicate;

    public CadEntityFilter(string id, Func<CadEntity, bool> predicate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(predicate);
        Id = id.Trim();
        _predicate = predicate;
    }

    public string Id { get; }

    public bool Allows(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _predicate(entity);
    }
}

public sealed class CadSelectionChangedEventArgs(
    IReadOnlyList<CadEntity> entities,
    CadEntity? primary) : EventArgs
{
    public IReadOnlyList<CadEntity> Entities { get; } =
        entities ?? throw new ArgumentNullException(nameof(entities));

    public CadEntity? Primary { get; } = primary;
}

public sealed class CadSelectionManager
{
    private readonly CadDocument _document;
    private readonly List<CadEntity> _selected = [];
    private OcctEngine? _engine;
    private CadEntity? _primary;

    public CadSelectionManager(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.Changed += DocumentChanged;
    }

    private int _pixelTolerance = 5;
    public int PixelTolerance
    {
        get => _pixelTolerance;
        set
        {
            if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value));
            _pixelTolerance = value;
            if (_engine is { IsInitialized: true } engine)
                TryViewerSideEffect(() => engine.SetSelectionTolerance(value), "selection tolerance");
        }
    }

    public IReadOnlyList<CadEntity> Selected => _selected;
    public IReadOnlyList<CadSelectionReference> References =>
        _selected
            .Select(CadSelectionReference.Entity)
            .ToArray();
    public CadEntity? Primary => _primary;
    public CadSelectionReference? PrimaryReference =>
        _primary is null
            ? null
            : CadSelectionReference.Entity(_primary);
    public CadEntityFilter? Filter { get; private set; }
    private CadEntityFilterKind _filterKind = CadEntityFilterKind.All;

    public CadEntityFilterKind FilterKind
    {
        get => _filterKind;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_filterKind == value) return;
            _filterKind = value;
            RefreshValidity();
            PublishSimple(FilterChanged, "FilterChanged");
        }
    }

    private CadSelectionScope _scope;
    private CadSubshapeMask _subshapeMask = CadSubshapeMask.All;

    public CadSelectionScope Scope
    {
        get => _scope;
        set => SetScope(value, _subshapeMask);
    }

    public CadSubshapeMask SubshapeMask
    {
        get => _subshapeMask;
        set => SetScope(_scope, value);
    }

    public void SetScope(CadSelectionScope scope, CadSubshapeMask mask)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if ((mask & ~CadSubshapeMask.All) != 0) throw new ArgumentOutOfRangeException(nameof(mask));
        if (_scope == scope && _subshapeMask == mask) return;
        _scope = scope;
        _subshapeMask = mask;
        RefreshValidity();
        TryViewerSideEffect(SynchronizeSelectionModes, "selection modes");
        PublishSimple(FilterChanged, "FilterChanged");
    }

    public bool CanSelectSubshape(CadEntity entity, OcctShapeType type) =>
        Scope == CadSelectionScope.Subobject &&
        CanSelectOwner(entity) && SubshapeMask.Allows(type);

    internal bool CanSelectOwner(CadEntity entity) =>
        _document.Entities.Contains(entity) &&
        _document.IsEntitySelectable(entity) &&
        (Filter is null || Filter.Allows(entity));

    private void SynchronizeSelectionModes()
    {
        if (_engine is not { IsInitialized: true } engine) return;
        engine.SetSelectionMode(OcctSelectionMode.Object);
        if (Scope == CadSelectionScope.Entity) return;
        foreach (var entity in _document.Entities)
            SynchronizeSubshapeModes(entity);
    }

    private void SynchronizeSubshapeModes(CadEntity entity)
    {
        if (_engine is not { IsInitialized: true } engine ||
            Scope != CadSelectionScope.Subobject || entity.ViewerObject is not { } shape) return;
        engine.SetSelectionModeActive(shape, OcctSelectionMode.Object, false,
            OcctSelectionModeConcurrency.Multiple, false);
        foreach (var mode in Enum.GetValues<OcctSelectionMode>())
        {
            if (mode == OcctSelectionMode.Object) continue;
            var type = Enum.Parse<OcctShapeType>(mode.ToString());
            engine.SetSelectionModeActive(shape, mode, SubshapeMask.Allows(type),
                OcctSelectionModeConcurrency.Multiple, false);
        }
    }

    public event EventHandler<CadSelectionChangedEventArgs>? Changed;
    public event EventHandler? Cleared;
    public event EventHandler? FilterChanged;

    public bool CanSelect(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _document.Entities.Contains(entity) && IsSelectable(entity);
    }

    public void SetFilter(CadEntityFilter? filter)
    {
        if (ReferenceEquals(Filter, filter)) return;
        Filter = filter;
        RefreshValidity();
        PublishSimple(FilterChanged, "FilterChanged");
    }

    public void ClearFilter() => SetFilter(null);

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException("The OCCT engine is not initialized.");

        _engine = engine;
        TryViewerSideEffect(() => engine.SetSelectionTolerance(PixelTolerance), "selection tolerance");
        TryViewerSideEffect(SynchronizeSelectionModes, "selection modes");
        TryViewerSideEffect(SyncEngineSelection, "selection synchronization");
    }

    public void UpdateFromViewer(
        IEnumerable<IOcctObject> selectedObjects,
        IOcctObject? primaryObject = null)
    {
        ArgumentNullException.ThrowIfNull(selectedObjects);
        if (Scope != CadSelectionScope.Entity) return;

        var entities = selectedObjects
            .Select(_document.FindByViewerObject)
            .OfType<CadEntity>()
            .Where(IsSelectable)
            .Distinct()
            .ToArray();

        var primary = _document.FindByViewerObject(primaryObject);
        if (primary is null || !entities.Contains(primary))
        {
            primary = _primary is not null && entities.Contains(_primary)
                ? _primary
                : entities.FirstOrDefault();
        }

        SetSelection(entities, primary, syncEngine: false);
    }

    public void Select(
        CadEntity entity,
        CadSelectionOperation operation = CadSelectionOperation.Replace)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Apply([entity], operation, entity);
    }

    public void Select(CadEntity entity, bool append) =>
        Select(
            entity,
            append
                ? CadSelectionOperation.Add
                : CadSelectionOperation.Replace);

    public void Apply(
        IEnumerable<CadEntity> entities,
        CadSelectionOperation operation,
        CadEntity? primary = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));

        if (Scope != CadSelectionScope.Entity) return;

        var requested = entities
            .Distinct()
            .Where(entity => _document.Entities.Contains(entity))
            .ToArray();

        var values = operation switch
        {
            CadSelectionOperation.Replace or CadSelectionOperation.Add =>
                requested.Where(IsSelectable).ToArray(),
            CadSelectionOperation.Remove => requested,
            CadSelectionOperation.Toggle =>
                requested
                    .Where(entity =>
                        _selected.Contains(entity) ||
                        IsSelectable(entity))
                    .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        var target = operation switch
        {
            CadSelectionOperation.Replace => values.ToList(),
            CadSelectionOperation.Add => Add(values),
            CadSelectionOperation.Remove => Remove(values),
            CadSelectionOperation.Toggle => Toggle(values),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        if (operation == CadSelectionOperation.Replace &&
            target.Count == 0)
        {
            Clear();
            return;
        }

        var nextPrimary = ResolvePrimary(
            operation,
            values,
            target,
            primary);

        SetSelection(target, nextPrimary, syncEngine: true);
    }

    public void Clear()
    {
        var hadSelection = _selected.Count > 0 || _primary is not null;
        if (hadSelection)
        {
            SetSelection([], null, syncEngine: true);
        }
        else if (_engine is { IsInitialized: true } engine)
        {
            TryViewerSideEffect(engine.ClearSelection, "selection clear");
        }

        // This event represents the user's/API caller's explicit clear intent,
        // not merely a transition to an empty entity set. Subobject selection is
        // allowed to exist without entity selection, but an explicit Clear means
        // clear the complete formal selection state.
        PublishSimple(Cleared, "Cleared");
    }

    public void RefreshValidity()
    {
        var valid = _selected
            .Where(IsSelectable)
            .ToArray();

        if (valid.Length == _selected.Count) return;

        var primary = _primary is not null && valid.Contains(_primary)
            ? _primary
            : valid.FirstOrDefault();

        SetSelection(valid, primary, syncEngine: true);
    }

    private List<CadEntity> Add(IReadOnlyList<CadEntity> values)
    {
        var result = new List<CadEntity>(_selected);
        foreach (var entity in values)
            if (!result.Contains(entity))
                result.Add(entity);
        return result;
    }

    private bool IsSelectable(CadEntity entity)
    {
        if (Scope != CadSelectionScope.Entity || !CanSelectOwner(entity))
            return false;

        return _filterKind switch
        {
            CadEntityFilterKind.Point => entity is CadPointEntity,
            CadEntityFilterKind.Curve => entity is CadLineEntity or CadPolylineEntity or CadArcEntity or CadCircleEntity or CadEllipseEntity or CadSplineEntity or CadPathEntity,
            CadEntityFilterKind.Region => entity is CadRegionEntity or CadPolygonEntity or CadRectangleEntity,
            CadEntityFilterKind.Solid => entity is CadBoxEntity or CadCylinderEntity or CadConeEntity or CadSphereEntity or CadTorusEntity or CadFeatureEntity or CadBooleanEntity or CadImportedShapeEntity,
            _ => true
        };
    }

    private List<CadEntity> Remove(IReadOnlyList<CadEntity> values)
    {
        var remove = values.ToHashSet();
        return _selected
            .Where(entity => !remove.Contains(entity))
            .ToList();
    }

    private List<CadEntity> Toggle(IReadOnlyList<CadEntity> values)
    {
        var result = new List<CadEntity>(_selected);
        foreach (var entity in values)
        {
            if (!result.Remove(entity))
                result.Add(entity);
        }

        return result;
    }

    private CadEntity? ResolvePrimary(
        CadSelectionOperation operation,
        IReadOnlyList<CadEntity> values,
        IReadOnlyList<CadEntity> target,
        CadEntity? requestedPrimary)
    {
        if (requestedPrimary is not null && target.Contains(requestedPrimary))
            return requestedPrimary;

        if (target.Count == 0) return null;

        if (operation == CadSelectionOperation.Remove &&
            _primary is not null &&
            target.Contains(_primary))
            return _primary;

        if (operation == CadSelectionOperation.Add &&
            values.Count > 0)
            return values[^1];

        if (operation == CadSelectionOperation.Toggle)
        {
            for (var index = values.Count - 1; index >= 0; index--)
                if (target.Contains(values[index]))
                    return values[index];
        }

        if (_primary is not null && target.Contains(_primary))
            return _primary;

        return target[^1];
    }

    private void DocumentChanged(object? sender, CadDocumentChangedEventArgs args)
    {
        if (args.Entity is { } entity &&
            (args.Kind == CadDocumentChangeKind.Added ||
             args.Kind == CadDocumentChangeKind.Changed && args.EntityChangeKind == CadEntityChangeKind.Geometry))
            TryViewerSideEffect(() => SynchronizeSubshapeModes(entity), "subshape selection modes");
        if (args.Kind == CadDocumentChangeKind.Reset)
        {
            SetSelection([], null, syncEngine: false);
            PublishSimple(Cleared, "Cleared");
            return;
        }

        if (args.Kind == CadDocumentChangeKind.Removed &&
            args.Entity is { } removed &&
            _selected.Contains(removed))
        {
            var remaining = _selected
                .Where(entity => !ReferenceEquals(entity, removed))
                .ToArray();
            var primary =
                ReferenceEquals(_primary, removed)
                    ? remaining.FirstOrDefault()
                    : _primary;
            SetSelection(remaining, primary, syncEngine: false);
        }
    }

    private void SetSelection(
        IReadOnlyList<CadEntity> entities,
        CadEntity? primary,
        bool syncEngine)
    {
        var normalizedPrimary =
            primary is not null && entities.Contains(primary)
                ? primary
                : entities.FirstOrDefault();

        if (_selected.Count == entities.Count &&
            _selected.SequenceEqual(entities) &&
            ReferenceEquals(_primary, normalizedPrimary))
        {
            if (syncEngine)
                TryViewerSideEffect(SyncEngineSelection, "selection synchronization");
            return;
        }

        _selected.Clear();
        _selected.AddRange(entities);
        _primary = normalizedPrimary;

        if (syncEngine)
            TryViewerSideEffect(SyncEngineSelection, "selection synchronization");

        PublishChanged();
    }

    private void SyncEngineSelection()
    {
        if (_engine is not { IsInitialized: true } engine)
            return;

        IReadOnlyList<CadEntity> ordered = _primary is null
            ? _selected
            : [_primary, .. _selected.Where(entity => !ReferenceEquals(entity, _primary))];

        var objects = ordered
            .Select(entity => entity.ViewerObject)
            .OfType<IOcctObject>()
            .ToArray();

        if (objects.Length == 0)
            engine.ClearSelection();
        else
            engine.SetSelection(objects);
    }

    private void PublishChanged()
    {
        var handlers = Changed;
        if (handlers is null)
            return;

        var args = new CadSelectionChangedEventArgs(
            _selected.ToArray(),
            _primary);
        foreach (EventHandler<CadSelectionChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception) when (IsRecoverableSideEffectFailure(exception))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Selection Changed observer failed after formal state changed: {exception}");
            }
        }
    }

    private void PublishSimple(EventHandler? handlers, string eventName)
    {
        if (handlers is null)
            return;

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception exception) when (IsRecoverableSideEffectFailure(exception))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Selection {eventName} observer failed after formal state changed: {exception}");
            }
        }
    }

    private static void TryViewerSideEffect(Action action, string operation)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (IsRecoverableSideEffectFailure(exception))
        {
            System.Diagnostics.Debug.WriteLine(
                $"Selection viewer {operation} failed; formal selection remains authoritative: {exception}");
        }
    }

    private static bool IsRecoverableSideEffectFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
