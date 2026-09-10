using OcctNet;

namespace OCCAD;

public enum CadSelectionOperation
{
    Replace,
    Add,
    Remove,
    Toggle
}

public sealed class CadSelectionFilter
{
    private readonly Func<CadEntity, bool> _predicate;

    public CadSelectionFilter(string id, Func<CadEntity, bool> predicate)
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

    public IReadOnlyList<CadEntity> Selected => _selected;
    public CadEntity? Primary => _primary;
    public CadSelectionFilter? Filter { get; private set; }

    public event EventHandler<CadSelectionChangedEventArgs>? Changed;
    public event EventHandler? FilterChanged;

    public bool CanSelect(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _document.Entities.Contains(entity) && IsSelectable(entity);
    }

    public void SetFilter(CadSelectionFilter? filter)
    {
        if (ReferenceEquals(Filter, filter)) return;
        Filter = filter;
        RefreshValidity();
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearFilter() => SetFilter(null);

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException("The OCCT engine is not initialized.");

        _engine = engine;
        SyncEngineSelection();
    }

    public void UpdateFromViewer(
        IEnumerable<IOcctObject> selectedObjects,
        IOcctObject? primaryObject = null)
    {
        ArgumentNullException.ThrowIfNull(selectedObjects);

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

        var requested = entities
            .Distinct()
            .Where(entity => _document.Entities.Contains(entity))
            .ToArray();

        var values = operation switch
        {
            CadSelectionOperation.Replace or CadSelectionOperation.Add =>
                requested.Where(IsSelectable).ToArray(),
            CadSelectionOperation.Remove =>
                requested,
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

        var nextPrimary = ResolvePrimary(
            operation,
            values,
            target,
            primary);

        SetSelection(target, nextPrimary, syncEngine: true);
    }

    public void Clear()
    {
        if (_selected.Count == 0 && _primary is null)
        {
            if (_engine is { IsInitialized: true } engine)
                engine.ClearSelection();
            return;
        }

        SetSelection([], null, syncEngine: true);
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

    private bool IsSelectable(CadEntity entity) =>
        _document.IsEntitySelectable(entity) &&
        (Filter?.Allows(entity) ?? true);

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
        if (args.Kind == CadDocumentChangeKind.Reset)
        {
            SetSelection([], null, syncEngine: false);
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
            if (syncEngine) SyncEngineSelection();
            return;
        }

        _selected.Clear();
        _selected.AddRange(entities);
        _primary = normalizedPrimary;

        if (syncEngine)
            SyncEngineSelection();

        Changed?.Invoke(
            this,
            new CadSelectionChangedEventArgs(
                _selected.ToArray(),
                _primary));
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
}
