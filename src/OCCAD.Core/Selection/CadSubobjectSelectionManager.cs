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

    public bool IsSubshape =>
        SubshapeIndex >= 0 &&
        SubshapeType != OcctShapeType.Shape;

    public bool IsValid =>
        Entity is not null &&
        IsSubshape;
}

public sealed class CadSubobjectSelectionChangedEventArgs(
    IReadOnlyList<CadSubobjectSelection> items,
    CadSubobjectSelection? primary) : EventArgs
{
    public IReadOnlyList<CadSubobjectSelection> Items { get; } =
        items ?? throw new ArgumentNullException(nameof(items));

    public CadSubobjectSelection? Primary { get; } = primary;
}

/// <summary>
/// Owns formal subobject-selection semantics: selected items, primary item,
/// selection operations and validity against the current selection policy.
/// Native/stable topology reference work is delegated to
/// <see cref="CadSubobjectReferenceService"/>.
/// </summary>
public sealed class CadSubobjectSelectionManager
{
    private readonly CadDocument _document;
    private readonly CadSelectionManager _selection;
    private readonly CadSubobjectReferenceService _references;
    private readonly List<CadSubobjectSelection> _selected = [];

    public CadSubobjectSelectionManager(
        CadDocument document,
        CadSelectionManager selection)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _references = new CadSubobjectReferenceService(_document);

        _document.Changed += DocumentChanged;
        _selection.Changed += SelectionChanged;
        _selection.Cleared += SelectionCleared;
        _selection.FilterChanged += SelectionFilterChanged;
    }

    public IReadOnlyList<CadSubobjectSelection> Selected => _selected;
    public CadSubobjectSelection? Primary { get; private set; }
    public IReadOnlyList<CadSelectionReference> References =>
        _selected
            .Select(static item => item.Reference)
            .ToArray();
    public CadSelectionReference? PrimaryReference =>
        Primary?.Reference;

    public event EventHandler<CadSubobjectSelectionChangedEventArgs>? Changed;

    public void AttachEngine(OcctEngine engine)
    {
        _references.AttachEngine(engine);
        StabilizeCurrentSelections();
    }

    public IReadOnlyList<CadSubshapeReference> CaptureReferences(
        OcctEngine engine) =>
        _references.CaptureReferences(
            engine,
            _selected);

    public bool TryResolveReference(
        OcctEngine engine,
        CadSubshapeReference reference,
        out CadSubshapeReference resolved) =>
        _references.TryResolveReference(
            engine,
            reference,
            out resolved);

    public void Select(
        CadPreselection preselection,
        CadSelectionOperation operation = CadSelectionOperation.Replace)
    {
        if (!preselection.IsSubshape)
        {
            if (operation == CadSelectionOperation.Replace)
                Clear();
            return;
        }

        Apply(
            _references.Stabilize(
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

        if (!item.IsValid)
            return;

        item = _references.Stabilize(item);
        var index = FindIndex(item);
        var addsItem =
            operation is CadSelectionOperation.Replace or
                CadSelectionOperation.Add ||
            operation == CadSelectionOperation.Toggle &&
            index < 0;

        if (addsItem &&
            !_selection.CanSelectSubshape(
                item.Entity,
                item.SubshapeType))
            return;

        if (addsItem &&
            _selection.Selected.Count > 0)
        {
            // Formal selection is one coherent scope at a time. A subobject
            // keeps its owner entity as context but does not coexist with
            // whole-entity formal selection.
            _selection.Apply(
                Array.Empty<CadEntity>(),
                CadSelectionOperation.Replace);
        }

        var changed = operation switch
        {
            CadSelectionOperation.Replace => Replace(item),
            CadSelectionOperation.Add => Add(item, index),
            CadSelectionOperation.Remove => Remove(item, index),
            CadSelectionOperation.Toggle => Toggle(item, index),
            _ => false
        };

        if (changed)
            RaiseChanged();
    }

    public void Clear()
    {
        if (_selected.Count == 0 && Primary is null)
            return;

        _selected.Clear();
        Primary = null;
        RaiseChanged();
    }

    public void RefreshValidity()
    {
        var changed = _selected.RemoveAll(item =>
            !_selection.CanSelectSubshape(
                item.Entity,
                item.SubshapeType)) > 0;
        if (!changed)
            return;

        if (Primary is { } primary &&
            FindIndex(primary) < 0)
        {
            Primary = _selected.Count == 0
                ? null
                : _selected[^1];
        }

        RaiseChanged();
    }

    private bool Replace(CadSubobjectSelection item)
    {
        if (_selected.Count == 1 &&
            SameLogicalSelection(_selected[0], item) &&
            Primary is { } primary &&
            SameLogicalSelection(primary, item))
            return false;

        _selected.Clear();
        _selected.Add(item);
        Primary = item;
        return true;
    }

    private bool Add(
        CadSubobjectSelection item,
        int existingIndex)
    {
        if (existingIndex >= 0)
        {
            var primaryChanged =
                Primary is not { } primary ||
                !SameLogicalSelection(primary, item);
            Primary = _selected[existingIndex];
            return primaryChanged;
        }

        _selected.Add(item);
        Primary = item;
        return true;
    }

    private bool Remove(
        CadSubobjectSelection item,
        int existingIndex)
    {
        if (existingIndex < 0)
            return false;

        var removed = _selected[existingIndex];
        _selected.RemoveAt(existingIndex);
        Primary = ResolvePrimary(Primary, removed);
        return true;
    }

    private bool Toggle(
        CadSubobjectSelection item,
        int existingIndex)
    {
        if (existingIndex >= 0)
            return Remove(item, existingIndex);

        _selected.Add(item);
        Primary = item;
        return true;
    }

    private int FindIndex(CadSubobjectSelection item) =>
        _selected.FindIndex(value =>
            SameLogicalSelection(value, item));

    private static bool SameLogicalSelection(
        CadSubobjectSelection left,
        CadSubobjectSelection right) =>
        left.SubshapeReference.SameLogicalReference(
            right.SubshapeReference);

    private CadSubobjectSelection? ResolvePrimary(
        CadSubobjectSelection? current,
        CadSubobjectSelection removed)
    {
        if (current is { } value &&
            SameLogicalSelection(value, removed))
        {
            return _selected.Count == 0
                ? null
                : _selected[^1];
        }

        return current;
    }

    private void DocumentChanged(
        object? sender,
        CadDocumentChangedEventArgs args)
    {
        if (args.Kind == CadDocumentChangeKind.Reset)
        {
            Clear();
            return;
        }

        if (args.Kind == CadDocumentChangeKind.Removed &&
            args.Entity is { } removed)
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

    private void StabilizeCurrentSelections()
    {
        if (_selected.Count == 0)
            return;

        var primaryReference =
            Primary?.SubshapeReference;
        var changed = false;

        for (var index = 0;
             index < _selected.Count;
             index++)
        {
            var next = _references.Stabilize(_selected[index]);
            if (next == _selected[index])
                continue;

            _selected[index] = next;
            changed = true;
        }

        if (primaryReference is { } expected)
            Primary = FindByReference(expected) ?? Primary;

        if (changed)
            RaiseChanged();
    }

    private void RefreshEntitySelections(CadEntity entity)
    {
        var affected = _selected
            .Select((item, index) => (item, index))
            .Where(pair =>
                ReferenceEquals(pair.item.Entity, entity))
            .ToArray();
        if (affected.Length == 0)
            return;

        var primaryReference =
            Primary is { } primary &&
            ReferenceEquals(primary.Entity, entity)
                ? primary.SubshapeReference
                : (CadSubshapeReference?)null;

        var remove = new List<int>();
        var changed = false;
        foreach (var (item, index) in affected)
        {
            if (!_references.TryRefresh(
                    item,
                    out var refreshed))
            {
                remove.Add(index);
                continue;
            }

            if (refreshed != item)
            {
                _selected[index] = refreshed;
                changed = true;
            }
        }

        for (var index = remove.Count - 1;
             index >= 0;
             index--)
        {
            _selected.RemoveAt(remove[index]);
            changed = true;
        }

        if (primaryReference is { } expected)
        {
            Primary = FindByReference(expected) ??
                (_selected.Count == 0
                    ? null
                    : _selected[^1]);
        }
        else if (Primary is { } current &&
                 !_selected.Contains(current))
        {
            Primary = _selected.Count == 0
                ? null
                : _selected[^1];
        }

        if (changed)
            RaiseChanged();
    }

    private CadSubobjectSelection? FindByReference(
        CadSubshapeReference reference)
    {
        var index = _selected.FindIndex(item =>
            item.SubshapeReference.SameLogicalReference(reference));
        return index >= 0
            ? _selected[index]
            : null;
    }

    private void RemoveEntitySelections(CadEntity entity)
    {
        var changed = _selected.RemoveAll(item =>
            ReferenceEquals(item.Entity, entity)) > 0;
        if (!changed)
            return;

        if (Primary is { } primary &&
            ReferenceEquals(primary.Entity, entity))
        {
            Primary = _selected.Count == 0
                ? null
                : _selected[^1];
        }

        RaiseChanged();
    }

    private void SelectionChanged(
        object? sender,
        CadSelectionChangedEventArgs args)
    {
        if (args.Entities.Count > 0)
            Clear();
    }

    private void SelectionCleared(
        object? sender,
        EventArgs args) =>
        Clear();

    private void SelectionFilterChanged(
        object? sender,
        EventArgs args) =>
        RefreshValidity();

    private void RaiseChanged() =>
        Changed?.Invoke(
            this,
            new CadSubobjectSelectionChangedEventArgs(
                _selected.ToArray(),
                Primary));
}
