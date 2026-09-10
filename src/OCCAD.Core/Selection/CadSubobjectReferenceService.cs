using OcctNet;

namespace OCCAD;

/// <summary>
/// Owns the native/topological part of subobject reference management. The
/// selection manager owns only the selected set and primary-item semantics.
/// </summary>
internal sealed class CadSubobjectReferenceService
{
    private readonly CadDocument _document;
    private OcctEngine? _engine;

    public CadSubobjectReferenceService(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException(
                "The OCCT engine is not initialized.");

        _engine = engine;
    }

    public IReadOnlyList<CadSubshapeReference> CaptureReferences(
        OcctEngine engine,
        IEnumerable<CadSubobjectSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(selections);

        return selections
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

        var entity = _document.FindById(reference.EntityId);
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

    public CadSubobjectSelection Stabilize(
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

    public bool TryRefresh(
        CadSubobjectSelection item,
        out CadSubobjectSelection refreshed)
    {
        if (!item.IsValid)
        {
            refreshed = default;
            return false;
        }

        if (_engine is not { IsInitialized: true } engine ||
            !CadSubshapeReferenceResolver.TryResolve(
                engine,
                item.Entity,
                item.SubshapeReference,
                out var resolvedIndex))
        {
            refreshed = default;
            return false;
        }

        var reference = item.SubshapeReference;
        var resolvedReference =
            new CadSubshapeReference(
                item.Entity.Id,
                reference.ShapeType,
                resolvedIndex,
                reference.Fallback);

        var point = item.Point;
        CadSubshapeReferenceResolver.TryGetRepresentativePoint(
            engine,
            item.Entity,
            resolvedReference,
            out point);

        refreshed = item with
        {
            SubshapeIndex = resolvedIndex,
            Point = point,
            StableReference = resolvedReference
        };
        return true;
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
