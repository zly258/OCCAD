using OcctNet;

namespace OCCAD;

public readonly record struct CadSubobjectSelection(
    CadEntity Entity,
    OcctShapeType SubshapeType,
    int SubshapeIndex,
    OcctPoint3d Point)
{
    public CadSelectionReference Reference =>
        CadSelectionReference.Subobject(
            Entity,
            SubshapeType,
            SubshapeIndex);

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
            return true;

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
            new CadSubobjectSelection(
                preselection.Entity,
                preselection.SubshapeType,
                preselection.SubshapeIndex,
                preselection.Point),
            operation);
    }

    public void Apply(
        CadSubobjectSelection item,
        CadSelectionOperation operation)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));

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
            value.Reference == item.Reference);

    private CadSubobjectSelection? ResolvePrimary(
        CadSubobjectSelection? current,
        CadSubobjectSelection removed)
    {
        if (current is { } value &&
            value.Reference == removed.Reference)
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
            RemoveEntitySelections(changedEntity);
            return;
        }

        RefreshValidity();
    }

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
