using OcctNet;

namespace OCCAD;

public readonly record struct CadSubobjectSelection(
    CadEntity Entity,
    OcctShapeType SubshapeType,
    int SubshapeIndex,
    OcctPoint3d Point,
    CadSubshapeReference? StableReference = null)
{
    public CadSubshapeReference SubshapeReference =>
        StableReference ??
        CadSubshapeReference.Create(
            Entity,
            SubshapeType,
            SubshapeIndex);

    public CadSelectionReference Reference =>
        SubshapeReference.ToSelectionReference();

    public bool IsValid =>
        Entity is not null &&
        SubshapeIndex >= 0 &&
        SubshapeType != OcctShapeType.Shape;

    public bool TryGetPathSegment(
        out CadPathSegmentInfo segment)
    {
        if (Entity is CadPathEntity path &&
            SubshapeType == OcctShapeType.Edge &&
            path.TryGetSegmentInfo(SubshapeIndex, out segment))
        {
            segment = segment with
            {
                Start = Entity.ToWorldPoint(segment.Start),
                End = Entity.ToWorldPoint(segment.End),
                Middle = segment.Middle is { } middle
                    ? Entity.ToWorldPoint(middle)
                    : null
            };
            return true;
        }

        segment = default;
        return false;
    }
}

public sealed class CadSubobjectSelectionChangedEventArgs(
    IReadOnlyList<CadSubobjectSelection> items,
    CadSubobjectSelection? primary) : EventArgs
{
    public IReadOnlyList<CadSubobjectSelection> Items { get; } =
        items ?? throw new ArgumentNullException(nameof(items));

    public CadSubobjectSelection? Primary { get; } = primary;
}

public sealed class CadSubobjectSelectionManager
{
    private readonly CadDocument _document;
    private readonly CadSelectionManager _selection;
    private readonly List<CadSubobjectSelection> _selected = [];
    private OcctEngine? _engine;

    public CadSubobjectSelectionManager(
        CadDocument document,
        CadSelectionManager selection)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _document.Changed += DocumentChanged;
        _selection.Changed += SelectionChanged;
        _selection.Cleared += SelectionCleared;
        _selection.FilterChanged += SelectionFilterChanged;
    }

    public IReadOnlyList<CadSubobjectSelection> Selected => _selected;
    public CadSubobjectSelection? Primary { get; private set; }

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException(
                "The OCCT engine is not initialized.");

        _engine = engine;
        StabilizeCurrentSelections();
    }

    public IReadOnlyList<CadSubshapeReference> CaptureReferences(
        OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return _selected
            .Select(item =>
                CadSubshapeReferenceResolver.Capture(
                    engine,
                    item))
            .ToArray();
    }

    public bool TryResolveReference(
        OcctEngine engine,
        CadSubshapeReference reference,
        out CadSubshapeReference resolved)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var entity =
            _document.FindById(reference.EntityId);
        if (entity is null ||
            !CadSubshapeReferenceResolver.TryResolve(
                engine,
                entity,
                reference,
                out var index))
        {
            resolved = default;
            return false;
        }

        resolved = new CadSubshapeReference(
            entity.Id,
            reference.ShapeType,
            index,
            reference.Fallback);
        return true;
    }

    public event EventHandler<CadSubobjectSelectionChangedEventArgs>? Changed;

    public void Select(
        CadPreselection preselection,
        CadSelectionOperation operation = CadSelectionOperation.Replace)
    {
        if (!preselection.IsSubshape)
        {
            if (operation == CadSelectionOperation.Replace) Clear();
            return;
        }

        Apply(
            Stabilize(
                new CadSubobjectSelection(
                    preselection.Entity,
                    preselection.SubshapeType,
                    preselection.SubshapeIndex,
                    preselection.Point)),
            operation);
    }

    public void Apply(
        CadSubobjectSelection item,
        CadSelectionOperation operation)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));

        item = Stabilize(item);
        var index = FindIndex(item);
        if (operation is CadSelectionOperation.Replace or CadSelectionOperation.Add or CadSelectionOperation.Toggle)
        {
            if (!_selection.CanSelect(item.Entity) ||
                item.SubshapeIndex < 0 ||
                item.SubshapeType == OcctShapeType.Shape)
                return;
        }

        var willAdd =
            operation is CadSelectionOperation.Replace or
                CadSelectionOperation.Add ||
            operation == CadSelectionOperation.Toggle &&
            index < 0;
        if (willAdd && _selection.Selected.Count > 0)
        {
            // Formal selection is one coherent mode at a time. Subobject
            // selection keeps its parent Entity context but does not coexist
            // with whole-Entity selection.
            _selection.Apply(
                Array.Empty<CadEntity>(),
                CadSelectionOperation.Replace);
        }

        switch (operation)
        {
            case CadSelectionOperation.Replace:
                _selected.Clear();
                _selected.Add(item);
                Primary = item;
                break;
            case CadSelectionOperation.Add:
                if (index < 0) _selected.Add(item);
                Primary = item;
                break;
            case CadSelectionOperation.Remove:
                if (index >= 0) _selected.RemoveAt(index);
                Primary = ResolvePrimary(Primary, item);
                break;
            case CadSelectionOperation.Toggle:
                if (index >= 0)
                {
                    _selected.RemoveAt(index);
                    Primary = ResolvePrimary(Primary, item);
                }
                else
                {
                    _selected.Add(item);
                    Primary = item;
                }
                break;
        }

        RaiseChanged();
    }

    public void Clear()
    {
        if (_selected.Count == 0 && Primary is null) return;
        _selected.Clear();
        Primary = null;
        RaiseChanged();
    }

    public void RefreshValidity()
    {
        var changed = _selected.RemoveAll(item => !_selection.CanSelect(item.Entity)) > 0;
        if (!changed) return;

        if (Primary is { } primary && FindIndex(primary) < 0)
            Primary = _selected.Count == 0 ? null : _selected[^1];
        RaiseChanged();
    }

    private int FindIndex(CadSubobjectSelection item) =>
        _selected.FindIndex(value =>
            value.SubshapeReference.SameLogicalReference(
                item.SubshapeReference));

    private CadSubobjectSelection? ResolvePrimary(
        CadSubobjectSelection? current,
        CadSubobjectSelection removed)
    {
        if (current is { } value &&
            value.SubshapeReference.SameLogicalReference(
                removed.SubshapeReference))
            return _selected.Count == 0 ? null : _selected[^1];

        return current;
    }

    private void DocumentChanged(object? sender, CadDocumentChangedEventArgs args)
    {
        if (args.Kind == CadDocumentChangeKind.Reset)
        {
            Clear();
            return;
        }

        if (args.Kind == CadDocumentChangeKind.Removed && args.Entity is { } removed)
        {
            RemoveEntitySelections(removed);
            return;
        }

        if (args.Kind == CadDocumentChangeKind.Changed &&
            args.EntityChangeKind == CadEntityChangeKind.Geometry &&
            args.Entity is { } changedEntity)
        {
            RefreshEntitySelections(changedEntity);
            return;
        }

        RefreshValidity();
    }

    private CadSubobjectSelection Stabilize(
        CadSubobjectSelection item)
    {
        if (item.StableReference is not null ||
            _engine is not { IsInitialized: true } engine ||
            !item.IsValid)
            return item;

        try
        {
            var reference =
                CadSubshapeReferenceResolver.Capture(
                    engine,
                    item);
            return item with
            {
                StableReference = reference
            };
        }
        catch (Exception exception)
            when (IsRecoverable(exception))
        {
            return item;
        }
    }

    private void StabilizeCurrentSelections()
    {
        if (_selected.Count == 0)
            return;

        var changed = false;
        for (var index = 0;
             index < _selected.Count;
             index++)
        {
            var next = Stabilize(_selected[index]);
            if (next == _selected[index])
                continue;

            _selected[index] = next;
            changed = true;
        }

        if (Primary is { } primary)
        {
            var primaryIndex = _selected.FindIndex(item =>
                item.SubshapeReference.SameLogicalReference(
                    primary.SubshapeReference));
            if (primaryIndex >= 0)
                Primary = _selected[primaryIndex];
        }

        if (changed)
            RaiseChanged();
    }

    private void RefreshEntitySelections(
        CadEntity entity)
    {
        var affected = _selected
            .Select((item, index) => (item, index))
            .Where(pair =>
                ReferenceEquals(pair.item.Entity, entity))
            .ToArray();

        if (affected.Length == 0)
            return;

        if (_engine is not { IsInitialized: true } engine)
        {
            RemoveEntitySelections(entity);
            return;
        }

        var primaryReference =
            Primary is { } primary &&
            ReferenceEquals(primary.Entity, entity)
                ? primary.SubshapeReference
                : (CadSubshapeReference?)null;

        var remove = new List<int>();
        foreach (var (item, index) in affected)
        {
            var reference = item.SubshapeReference;

            if (entity is CadPathEntity &&
                reference.ShapeType == OcctShapeType.Edge &&
                reference.Index < ((CadPathEntity)entity).SegmentCount)
            {
                var pathPoint = PathRepresentativePoint(
                    (CadPathEntity)entity,
                    reference.Index);

                _selected[index] = item with
                {
                    SubshapeIndex = reference.Index,
                    Point = pathPoint,
                    StableReference = reference
                };
                continue;
            }

            if (!CadSubshapeReferenceResolver.TryResolve(
                    engine,
                    entity,
                    reference,
                    out var resolvedIndex))
            {
                remove.Add(index);
                continue;
            }

            var resolvedReference =
                new CadSubshapeReference(
                    entity.Id,
                    reference.ShapeType,
                    resolvedIndex,
                    reference.Fallback);

            var point = item.Point;
            CadSubshapeReferenceResolver.TryGetRepresentativePoint(
                engine,
                entity,
                resolvedReference,
                out point);

            _selected[index] = item with
            {
                SubshapeIndex = resolvedIndex,
                Point = point,
                StableReference = resolvedReference
            };
        }

        for (var index = remove.Count - 1;
             index >= 0;
             index--)
        {
            _selected.RemoveAt(remove[index]);
        }

        if (primaryReference is { } expected)
        {
            var primaryIndex =
                _selected.FindIndex(item =>
                    item.SubshapeReference
                        .SameLogicalReference(expected));
            Primary = primaryIndex >= 0
                ? _selected[primaryIndex]
                : _selected.Count == 0
                    ? null
                    : _selected[^1];
        }
        else if (Primary is { } current &&
                 !_selected.Contains(current))
        {
            Primary = _selected.Count == 0
                ? null
                : _selected[^1];
        }

        RaiseChanged();
    }

    private static OcctPoint3d PathRepresentativePoint(
        CadPathEntity path,
        int segmentIndex)
    {
        if (!path.TryGetSegmentInfo(
                segmentIndex,
                out var segment))
            return path.ToWorldPoint(path.Start);

        var point = segment.Middle ??
            new OcctPoint3d(
                (segment.Start.X + segment.End.X) * 0.5,
                (segment.Start.Y + segment.End.Y) * 0.5,
                (segment.Start.Z + segment.End.Z) * 0.5);

        return path.ToWorldPoint(point);
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private void RemoveEntitySelections(CadEntity entity)
    {
        var changed = _selected.RemoveAll(item => ReferenceEquals(item.Entity, entity)) > 0;
        if (!changed) return;
        if (Primary is { } primary && ReferenceEquals(primary.Entity, entity))
            Primary = _selected.Count == 0 ? null : _selected[^1];
        RaiseChanged();
    }

    private void SelectionChanged(
        object? sender,
        CadSelectionChangedEventArgs args)
    {
        if (args.Entities.Count > 0)
            Clear();
    }

    private void SelectionCleared(object? sender, EventArgs args) => Clear();

    private void SelectionFilterChanged(object? sender, EventArgs args) =>
        RefreshValidity();

    private void RaiseChanged() =>
        Changed?.Invoke(
            this,
            new CadSubobjectSelectionChangedEventArgs(
                _selected.ToArray(),
                Primary));
}
