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

    public CadEntityFilter(
        string id,
        Func<CadEntity, bool> predicate)
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
    private readonly CadSelectionPresenter _presenter = new();
    private readonly List<CadEntity> _selected = [];
    private CadEntity? _primary;
    private int _pixelTolerance = 5;
    private CadEntityFilterKind _filterKind = CadEntityFilterKind.All;
    private CadSelectionScope _scope;
    private CadSubshapeMask _subshapeMask = CadSubshapeMask.All;

    public CadSelectionManager(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.Changed += DocumentChanged;
    }

    public int PixelTolerance
    {
        get => _pixelTolerance;
        set
        {
            if (value is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_pixelTolerance == value)
                return;

            _pixelTolerance = value;
            _presenter.SetPixelTolerance(value);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
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

    public CadEntityFilterKind FilterKind
    {
        get => _filterKind;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_filterKind == value)
                return;

            _filterKind = value;
            RefreshValidity();
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

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

    public event EventHandler<CadSelectionChangedEventArgs>? Changed;
    public event EventHandler? Cleared;
    public event EventHandler? FilterChanged;
    public event EventHandler? SettingsChanged;

    public void SetScope(
        CadSelectionScope scope,
        CadSubshapeMask mask)
    {
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        if ((mask & ~CadSubshapeMask.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(mask));
        if (_scope == scope && _subshapeMask == mask)
            return;

        _scope = scope;
        _subshapeMask = mask;
        RefreshValidity();
        _presenter.SynchronizeModes(
            _document.Entities,
            _scope,
            _subshapeMask);
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanSelectSubshape(
        CadEntity entity,
        OcctShapeType type) =>
        Scope == CadSelectionScope.Subobject &&
        CanSelectOwner(entity) &&
        SubshapeMask.Allows(type);

    internal bool CanSelectOwner(CadEntity entity) =>
        _document.Entities.Contains(entity) &&
        _document.IsEntitySelectable(entity) &&
        (Filter is null || Filter.Allows(entity));

    public bool CanSelect(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _document.Entities.Contains(entity) &&
               IsSelectable(entity);
    }

    public void SetFilter(CadEntityFilter? filter)
    {
        if (ReferenceEquals(Filter, filter))
            return;

        Filter = filter;
        RefreshValidity();
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearFilter() => SetFilter(null);

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _presenter.AttachEngine(
            engine,
            PixelTolerance,
            _scope,
            _subshapeMask,
            _document.Entities,
            _selected,
            _primary);
    }

    public void UpdateFromViewer(
        IEnumerable<IOcctObject> selectedObjects,
        IOcctObject? primaryObject = null)
    {
        ArgumentNullException.ThrowIfNull(selectedObjects);
        if (Scope != CadSelectionScope.Entity)
            return;

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

        SetSelection(
            entities,
            primary,
            syncEngine: false);
    }

    public void Select(
        CadEntity entity,
        CadSelectionOperation operation = CadSelectionOperation.Replace)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Apply([entity], operation, entity);
    }

    public void Select(
        CadEntity entity,
        bool append) =>
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
        if (Scope != CadSelectionScope.Entity)
            return;

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

        SetSelection(
            target,
            nextPrimary,
            syncEngine: true);
    }

    public void Clear()
    {
        var hadSelection =
            _selected.Count > 0 ||
            _primary is not null;

        if (hadSelection)
            SetSelection([], null, syncEngine: true);
        else
            _presenter.ClearSelection();

        // Explicit Clear means clear all formal selection state, including any
        // coupled subobject selection managed by the workspace.
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshValidity()
    {
        var valid = _selected
            .Where(IsSelectable)
            .ToArray();

        if (valid.Length == _selected.Count)
            return;

        var primary =
            _primary is not null && valid.Contains(_primary)
                ? _primary
                : valid.FirstOrDefault();

        SetSelection(
            valid,
            primary,
            syncEngine: true);
    }

    private List<CadEntity> Add(IReadOnlyList<CadEntity> values)
    {
        var result = new List<CadEntity>(_selected);
        foreach (var entity in values)
        {
            if (!result.Contains(entity))
                result.Add(entity);
        }
        return result;
    }

    private bool IsSelectable(CadEntity entity)
    {
        if (Scope != CadSelectionScope.Entity ||
            !CanSelectOwner(entity))
            return false;

        return _filterKind switch
        {
            CadEntityFilterKind.Point =>
                entity is CadPointEntity,
            CadEntityFilterKind.Curve =>
                entity is CadLineEntity or
                    CadCenterLineEntity or
                    CadCenterMarkEntity or
                    CadPolylineEntity or
                    CadArcEntity or
                    CadCircleEntity or
                    CadEllipseEntity or
                    CadSplineEntity,
            CadEntityFilterKind.Region =>
                entity is CadPolygonEntity or
                    CadRegularPolygonEntity or
                    CadRectangleEntity,
            CadEntityFilterKind.Solid =>
                entity is CadBoxEntity or
                    CadCylinderEntity or
                    CadConeEntity or
                    CadSphereEntity,
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
        if (requestedPrimary is not null &&
            target.Contains(requestedPrimary))
            return requestedPrimary;

        if (target.Count == 0)
            return null;

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
            {
                if (target.Contains(values[index]))
                    return values[index];
            }
        }

        if (_primary is not null &&
            target.Contains(_primary))
            return _primary;

        return target[^1];
    }

    private void DocumentChanged(
        object? sender,
        CadDocumentChangedEventArgs args)
    {
        if (args.Entity is { } entity &&
            (args.Kind == CadDocumentChangeKind.Added ||
             args.Kind == CadDocumentChangeKind.Changed &&
             args.EntityChangeKind == CadEntityChangeKind.Geometry))
        {
            _presenter.SynchronizeSubshapeModes(
                entity,
                _scope,
                _subshapeMask);
        }

        if (args.Kind == CadDocumentChangeKind.Reset)
        {
            SetSelection([], null, syncEngine: false);
            Cleared?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (args.Kind == CadDocumentChangeKind.Removed &&
            args.Entity is { } removed &&
            _selected.Contains(removed))
        {
            var remaining = _selected
                .Where(entity =>
                    !ReferenceEquals(entity, removed))
                .ToArray();
            var primary = ReferenceEquals(_primary, removed)
                ? remaining.FirstOrDefault()
                : _primary;

            SetSelection(
                remaining,
                primary,
                syncEngine: false);
            return;
        }

        if (args.Kind == CadDocumentChangeKind.LayerChanged ||
            args.Kind == CadDocumentChangeKind.Changed &&
            args.EntityChangeKind is
                CadEntityChangeKind.Appearance or
                CadEntityChangeKind.Metadata)
        {
            // Visibility, lock state, selectable state, and layer reassignment
            // all change selection eligibility. Selection must follow the same
            // canonical document appearance rules as hit testing/preselection.
            RefreshValidity();
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
            {
                _presenter.SynchronizeSelection(
                    _selected,
                    _primary);
            }
            return;
        }

        _selected.Clear();
        _selected.AddRange(entities);
        _primary = normalizedPrimary;

        if (syncEngine)
        {
            _presenter.SynchronizeSelection(
                _selected,
                _primary);
        }

        Changed?.Invoke(
            this,
            new CadSelectionChangedEventArgs(
                _selected.ToArray(),
                _primary));
    }
}
