using OcctNet;

namespace OCCAD;

public readonly record struct CadPreselection(
    CadEntity Entity,
    OcctPoint3d Point,
    OcctShapeType SubshapeType,
    int SubshapeIndex)
{
    public bool IsSubshape => SubshapeIndex >= 0;

    public CadSelectionReference Reference =>
        IsSubshape
            ? CadSelectionReference.Subobject(
                Entity,
                SubshapeType,
                SubshapeIndex)
            : CadSelectionReference.Entity(Entity);
}

public sealed class CadPreselectionChangedEventArgs(CadPreselection? value) : EventArgs
{
    public CadPreselection? Value { get; } = value;
}

public sealed class CadPreselectionManager
{
    private readonly CadDocument _document;
    private readonly CadSelectionManager _selection;

    public CadPreselectionManager(
        CadDocument document,
        CadSelectionManager selection)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _document.Changed += DocumentChanged;
        _selection.FilterChanged += SelectionFilterChanged;
    }

    // OCCT detection and click selection use the same physical-pixel aperture.
    public int PixelTolerance => _selection.PixelTolerance;

    public CadPreselection? Current { get; private set; }

    public event EventHandler<CadPreselectionChangedEventArgs>? Changed;

    public void Update(CadEntity? entity, OcctSelectionHitDetail? hit)
    {
        if (entity is null || (_selection.Scope == CadSelectionScope.Entity
                ? !_selection.CanSelect(entity)
                : hit is not { SubshapeIndex: >= 0 } detail ||
                  !_selection.CanSelectSubshape(entity, detail.SubshapeType)))
        {
            Clear();
            return;
        }

        var next = hit is { } value
            ? new CadPreselection(
                entity,
                value.Point,
                _selection.Scope == CadSelectionScope.Subobject ? value.SubshapeType : OcctShapeType.Shape,
                _selection.Scope == CadSelectionScope.Subobject ? value.SubshapeIndex : -1)
            : new CadPreselection(
                entity,
                OcctPoint3d.Origin,
                OcctShapeType.Shape,
                -1);

        if (Current == next) return;
        Current = next;
        Changed?.Invoke(this, new CadPreselectionChangedEventArgs(next));
    }

    public void Clear()
    {
        if (Current is null) return;
        Current = null;
        Changed?.Invoke(this, new CadPreselectionChangedEventArgs(null));
    }

    private void DocumentChanged(object? sender, CadDocumentChangedEventArgs args)
    {
        if (args.Kind == CadDocumentChangeKind.Reset ||
            (args.Kind == CadDocumentChangeKind.Removed &&
             Current is { } current &&
             ReferenceEquals(args.Entity, current.Entity)))
        {
            Clear();
            return;
        }

        if (Current is { } value && !CanKeep(value))
            Clear();
    }

    private bool CanKeep(CadPreselection value) =>
        value.IsSubshape
            ? _selection.CanSelectSubshape(value.Entity, value.SubshapeType)
            : _selection.CanSelect(value.Entity);

    private void SelectionFilterChanged(object? sender, EventArgs args)
    {
        if (Current is { } current && !CanKeep(current))
            Clear();
    }
}
