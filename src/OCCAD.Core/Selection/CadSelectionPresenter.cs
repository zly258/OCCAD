using OcctNet;

namespace OCCAD;

/// <summary>
/// Bridges semantic OCCAD selection state to OCCT selection modes and viewer
/// selection. It does not decide what is selectable and owns no domain state.
/// </summary>
internal sealed class CadSelectionPresenter
{
    private OcctEngine? _engine;

    public void AttachEngine(
        OcctEngine engine,
        int pixelTolerance,
        CadSelectionScope scope,
        CadSubshapeMask subshapeMask,
        IReadOnlyList<CadEntity> documentEntities,
        IReadOnlyList<CadEntity> selected,
        CadEntity? primary)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(documentEntities);
        ArgumentNullException.ThrowIfNull(selected);
        if (!engine.IsInitialized)
        {
            throw new InvalidOperationException(
                "The OCCT engine is not initialized.");
        }

        _engine = engine;
        SetPixelTolerance(pixelTolerance);
        SynchronizeModes(
            documentEntities,
            scope,
            subshapeMask);
        SynchronizeSelection(selected, primary);
    }

    public void SetPixelTolerance(int value)
    {
        if (_engine is { IsInitialized: true } engine)
            engine.SetSelectionTolerance(value);
    }

    public void SynchronizeModes(
        IEnumerable<CadEntity> entities,
        CadSelectionScope scope,
        CadSubshapeMask subshapeMask)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (_engine is not { IsInitialized: true } engine)
            return;

        engine.SetSelectionMode(OcctSelectionMode.Object);
        if (scope == CadSelectionScope.Entity)
            return;

        foreach (var entity in entities)
        {
            SynchronizeSubshapeModes(
                entity,
                scope,
                subshapeMask);
        }
    }

    public void SynchronizeSubshapeModes(
        CadEntity entity,
        CadSelectionScope scope,
        CadSubshapeMask subshapeMask)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (_engine is not { IsInitialized: true } engine ||
            scope != CadSelectionScope.Subobject ||
            entity.ViewerObject is not { } shape)
            return;

        engine.SetSelectionModeActive(
            shape,
            OcctSelectionMode.Object,
            false,
            OcctSelectionModeConcurrency.Multiple,
            false);

        foreach (var mode in Enum.GetValues<OcctSelectionMode>())
        {
            if (mode == OcctSelectionMode.Object)
                continue;

            var type = Enum.Parse<OcctShapeType>(mode.ToString());
            engine.SetSelectionModeActive(
                shape,
                mode,
                subshapeMask.Allows(type),
                OcctSelectionModeConcurrency.Multiple,
                false);
        }
    }

    public void SynchronizeSelection(
        IReadOnlyList<CadEntity> selected,
        CadEntity? primary)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (_engine is not { IsInitialized: true } engine)
            return;

        IReadOnlyList<CadEntity> ordered = primary is null
            ? selected
            : [
                primary,
                .. selected.Where(entity =>
                    !ReferenceEquals(entity, primary))
            ];

        var objects = ordered
            .Select(entity => entity.ViewerObject)
            .OfType<IOcctObject>()
            .ToArray();

        if (objects.Length == 0)
            engine.ClearSelection();
        else
            engine.SetSelection(objects);
    }

    public void ClearSelection()
    {
        if (_engine is { IsInitialized: true } engine)
            engine.ClearSelection();
    }
}
