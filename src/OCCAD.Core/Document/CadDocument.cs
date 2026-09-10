using System.Drawing;
using OcctNet;

namespace OCCAD;

public enum CadDocumentChangeKind
{
    Added,
    Removed,
    Changed,
    LayerChanged,
    Reset
}

public sealed class CadDocumentChangedEventArgs(
    CadDocumentChangeKind kind,
    CadEntity? entity,
    CadEntityChangeKind? entityChangeKind = null,
    string? propertyName = null) : EventArgs
{
    public CadDocumentChangeKind Kind { get; } = kind;
    public CadEntity? Entity { get; } = entity;
    public CadEntityChangeKind? EntityChangeKind { get; } = entityChangeKind;
    public string? PropertyName { get; } = propertyName;
}

public sealed class CadDocumentChangeSetEventArgs : EventArgs
{
    public CadDocumentChangeSetEventArgs(
        IReadOnlyList<CadDocumentChangedEventArgs> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Changes = changes.ToArray();
        Entities = Changes
            .Select(static change => change.Entity)
            .OfType<CadEntity>()
            .Distinct()
            .ToArray();
    }

    public IReadOnlyList<CadDocumentChangedEventArgs> Changes { get; }
    public IReadOnlyList<CadEntity> Entities { get; }

    public bool Contains(CadDocumentChangeKind kind) =>
        Changes.Any(change => change.Kind == kind);
}

public readonly record struct CadResolvedAppearance(
    Color Color,
    double LineWidth,
    OcctLineStyle LineStyle,
    bool Visible,
    bool Selectable);

public sealed class CadDocument
{
    private readonly List<CadEntity> _entities = [];
    private readonly Dictionary<Guid, CadEntity> _entitiesById = [];
    private readonly Dictionary<long, CadEntity> _viewerObjects = [];
    private readonly List<CadDocumentChangedEventArgs> _pendingChanges = [];
    private readonly HashSet<Guid> _dependencyRefreshStack = [];
    private readonly CadLayerManager _layers;
    private OcctEngine? _engine;
    private int _changeSetDepth;

    public CadDocument(CadLayerManager layers)
    {
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        _layers.Changed += LayersChanged;
    }

    public IReadOnlyList<CadEntity> Entities => _entities;
    public event EventHandler<CadDocumentChangedEventArgs>? Changed;
    public event EventHandler<CadDocumentChangeSetEventArgs>? ChangeSetCommitted;

    public IDisposable BeginChangeSet()
    {
        _changeSetDepth++;
        return new ChangeSetScope(this);
    }

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException(
                "The OCCT engine is not initialized.");
        if (ReferenceEquals(_engine, engine))
            return;

        var previousEngine = _engine;
        var previousObjects = _entities
            .Select(entity => (Entity: entity, Object: entity.ViewerObject))
            .Where(static pair => pair.Object is not null)
            .Select(static pair => (pair.Entity, Object: pair.Object!))
            .ToArray();

        var replacements = new List<(CadEntity Entity, IOcctObject Object)>(
            _entities.Count);

        try
        {
            using var batch = engine.BeginDisplayBatch();
            foreach (var entity in _entities)
            {
                var replacement = entity.BuildPresentation(engine);
                replacements.Add((entity, replacement));
                ApplyPlacement(engine, entity, replacement);
                ApplyAppearance(engine, entity, replacement);
            }
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (var (_, replacement) in replacements.AsEnumerable().Reverse())
            {
                try
                {
                    if (engine.ContainsObject(replacement.Id))
                        engine.Delete(replacement);
                }
                catch (Exception cleanupFailure)
                {
                    failures.Add(cleanupFailure);
                }
            }

            if (failures.Count > 1)
                throw new AggregateException(
                    "Engine presentation rebuild and cleanup both failed.",
                    failures);
            throw;
        }

        _engine = engine;
        _viewerObjects.Clear();
        foreach (var (entity, replacement) in replacements)
        {
            entity.ViewerObject = replacement;
            _viewerObjects.Add(replacement.Id, entity);
        }

        if (previousEngine is { IsInitialized: true } oldEngine &&
            !ReferenceEquals(oldEngine, engine) &&
            previousObjects.Length > 0)
        {
            try
            {
                using var batch = oldEngine.BeginDisplayBatch();
                var existing = previousObjects
                    .Select(static pair => pair.Object)
                    .Where(value => oldEngine.ContainsObject(value.Id))
                    .ToArray();
                if (existing.Length > 0)
                    oldEngine.Delete(existing);
            }
            catch (Exception exception)
                when (IsRecoverablePresentationCleanupFailure(exception))
            {
                // The new engine state is already authoritative.
            }
        }

        PublishChange(CadDocumentChangeKind.Reset, null);
    }

    public void DetachEngine(bool deletePresentation)
    {
        if (_engine is { IsInitialized: true } engine && deletePresentation)
        {
            using (engine.BeginDisplayBatch())
            {
                foreach (var entity in _entities)
                {
                    if (entity.ViewerObject is { } shape && engine.ContainsObject(shape.Id))
                        engine.Delete(shape);
                    entity.ViewerObject = null;
                }
            }
        }
        else
        {
            foreach (var entity in _entities)
                entity.ViewerObject = null;
        }

        _viewerObjects.Clear();
        _engine = null;
    }

    public void Add(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        AddRange([entity]);
    }

    public bool Remove(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return RemoveRange([entity]) > 0;
    }

    public void Clear()
    {
        using var changes = BeginChangeSet();
        RemoveRange(_entities.ToArray());
        PublishChange(CadDocumentChangeKind.Reset, null);
    }

    public CadEntity? FindByViewerObject(IOcctObject? value) =>
        value is null ? null : _viewerObjects.GetValueOrDefault(value.Id);

    public CadEntity? FindById(Guid id) => _entitiesById.GetValueOrDefault(id);

    public IReadOnlyList<CadEntity> GetDependentEntities(Guid sourceEntityId)
    {
        if (sourceEntityId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(sourceEntityId));

        return _entities
            .Where(entity =>
                entity is ICadSourceDependentEntity dependency &&
                dependency.SourceEntityIds.Contains(sourceEntityId))
            .ToArray();
    }

    public void AddRange(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var values = entities.ToArray();
        if (values.Length == 0) return;
        if (values.Any(static entity => entity is null))
            throw new ArgumentException("Entity collection contains null.", nameof(entities));
        if (values.Distinct().Count() != values.Length ||
            values.Select(static entity => entity.Id).Distinct().Count() != values.Length)
            throw new ArgumentException("Entity collection contains duplicates.", nameof(entities));
        if (values.Any(entity => _entitiesById.ContainsKey(entity.Id)))
            throw new InvalidOperationException("One or more entities are already in this document.");

        foreach (var entity in values)
        {
            var layer = _layers.GetRequired(entity.LayerId);
            if (!string.Equals(entity.LayerId, layer.Id, StringComparison.OrdinalIgnoreCase))
                entity.LayerId = layer.Id;
        }

        if (values.Any(static entity => entity.ViewerObject is not null))
            throw new InvalidOperationException("An entity already has a viewer presentation.");

        using var batch = _engine is { IsInitialized: true } engine
            ? engine.BeginDisplayBatch()
            : null;
        try
        {
            if (_engine is { IsInitialized: true })
            {
                foreach (var entity in values)
                    BuildPresentation(entity);
            }
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (var entity in values.Reverse())
            {
                try { DeletePresentation(entity); }
                catch (Exception cleanupFailure) { failures.Add(cleanupFailure); }
            }
            if (failures.Count > 1)
                throw new AggregateException("Entity creation and cleanup failed.", failures);
            throw;
        }

        using var changes = BeginChangeSet();
        foreach (var entity in values)
        {
            _entities.Add(entity);
            _entitiesById.Add(entity.Id, entity);
            entity.Changed += EntityChanged;
        }
        foreach (var entity in values)
            PublishChange(CadDocumentChangeKind.Added, entity);

        // A persisted dependent may carry cached geometry. Once all entities in
        // a load/import batch are present, the current source entities become
        // authoritative and can safely regenerate that cache.
        foreach (var entity in values)
        {
            if (entity is not ICadSourceDependentEntity dependency ||
                dependency.SourceEntityIds.Count == 0 ||
                dependency.SourceEntityIds.Any(id => !_entitiesById.ContainsKey(id)))
                continue;

            RefreshDependentEntity(entity, dependency);
        }
    }

    public int RemoveRange(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var values = entities.Distinct().Where(_entities.Contains).ToArray();
        if (values.Length == 0) return 0;

        var selected = _engine is { IsInitialized: true } currentEngine
            ? values.Where(entity =>
                entity.ViewerObject is { } shape &&
                currentEngine.ContainsObject(shape.Id) &&
                currentEngine.IsSelected(shape)).ToHashSet()
            : [];
        try
        {
            using var batch = _engine is { IsInitialized: true } engine
                ? engine.BeginDisplayBatch()
                : null;
            foreach (var entity in values)
                DeletePresentation(entity);
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (var entity in values)
            {
                try
                {
                    if (_engine is not { IsInitialized: true } engine)
                        throw new InvalidOperationException(
                            "Cannot restore presentation without an initialized engine.");

                    if (entity.ViewerObject is { } shape && engine.ContainsObject(shape.Id))
                        _viewerObjects[shape.Id] = entity;
                    else
                    {
                        entity.ViewerObject = null;
                        BuildPresentation(entity);
                    }

                    if (selected.Contains(entity) && entity.ViewerObject is { } restored)
                        engine.SelectObject(restored, appendSelection: true);
                }
                catch (Exception restoreFailure)
                {
                    failures.Add(restoreFailure);
                }
            }

            if (failures.Count > 1)
                throw new AggregateException(
                    "Entity removal and presentation recovery failed.",
                    failures);
            throw;
        }

        using var changes = BeginChangeSet();
        foreach (var entity in values)
        {
            _entities.Remove(entity);
            _entitiesById.Remove(entity.Id);
            entity.Changed -= EntityChanged;
        }
        foreach (var entity in values)
            PublishChange(CadDocumentChangeKind.Removed, entity);
        return values.Length;
    }

    public CadResolvedAppearance ResolveAppearance(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var layer = _layers.GetRequiredById(entity.LayerId);
        var visible = entity.Visible && layer.Visible;
        return new CadResolvedAppearance(
            entity.ColorByLayer ? layer.Color : entity.Color,
            entity.LineWidthByLayer ? layer.LineWidth : entity.LineWidth,
            entity.LineStyleByLayer ? layer.LineStyle : entity.LineStyle,
            visible,
            entity.Selectable && visible && !layer.Locked);
    }

    public bool IsEntityVisible(CadEntity entity) =>
        ResolveAppearance(entity).Visible;

    public bool IsEntitySelectable(CadEntity entity) =>
        _entitiesById.TryGetValue(entity.Id, out var member) &&
        ReferenceEquals(member, entity) &&
        ResolveAppearance(entity).Selectable;

    public IReadOnlyList<CadEntity> GetEntitiesByLayer(string layerReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerReference);
        var layer = _layers.GetRequired(layerReference);
        return _entities
            .Where(entity => string.Equals(
                entity.LayerId,
                layer.Id,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void EntityChanged(object? sender, CadEntityChangedEventArgs args)
    {
        if (sender is not CadEntity entity) return;

        var layerChanged =
            args.Kind == CadEntityChangeKind.Metadata &&
            args.PropertyName == nameof(CadEntity.LayerId);
        if (layerChanged)
        {
            var layer = _layers.GetRequired(entity.LayerId);
            if (!string.Equals(entity.LayerId, layer.Id, StringComparison.OrdinalIgnoreCase))
            {
                entity.LayerId = layer.Id;
                return;
            }
        }

        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            if (args.Kind == CadEntityChangeKind.Geometry)
            {
                if (args.PropertyName == nameof(CadEntity.Placement))
                    ApplyPlacement(entity);
                else
                    RebuildPresentation(entity);
            }
            else if (args.Kind == CadEntityChangeKind.Appearance || layerChanged)
            {
                ApplyAppearance(entity);
            }
        }

        PublishChange(
            CadDocumentChangeKind.Changed,
            entity,
            args.Kind,
            args.PropertyName);

        if (args.Kind == CadEntityChangeKind.Geometry)
            RefreshDependentEntities(entity.Id);
    }

    private void RefreshDependentEntities(Guid sourceEntityId)
    {
        foreach (var dependent in GetDependentEntities(sourceEntityId))
        {
            var dependency = (ICadSourceDependentEntity)dependent;
            RefreshDependentEntity(dependent, dependency);
        }
    }

    private void RefreshDependentEntity(
        CadEntity dependent,
        ICadSourceDependentEntity dependency)
    {
        if (!_dependencyRefreshStack.Add(dependent.Id))
            return;

        CadEntity? before = null;
        try
        {
            before = dependent.Duplicate();
            dependency.RefreshFromSources(this);
        }
        catch (Exception refreshFailure)
            when (IsRecoverableDependencyRefreshFailure(refreshFailure))
        {
            if (before is null)
                return;

            try
            {
                dependent.RestoreGeometrySnapshot(before);
            }
            catch (Exception restoreFailure)
                when (IsRecoverableDependencyRefreshFailure(restoreFailure))
            {
                throw new AggregateException(
                    "Entity dependency refresh and rollback both failed.",
                    refreshFailure,
                    restoreFailure);
            }
        }
        finally
        {
            _dependencyRefreshStack.Remove(dependent.Id);
        }
    }

    private static bool IsRecoverableDependencyRefreshFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private void LayersChanged(object? sender, CadLayerManagerChangedEventArgs args)
    {
        using var changes = BeginChangeSet();

        if (args.Kind is CadLayerManagerChangeKind.Added or CadLayerManagerChangeKind.CurrentChanged)
            return;

        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            foreach (var entity in _entities)
            {
                if (args.Kind == CadLayerManagerChangeKind.Reset ||
                    args.Layer is null ||
                    string.Equals(entity.LayerId, args.Layer.Id, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyAppearance(entity);
                }
            }
        }

        PublishChange(CadDocumentChangeKind.LayerChanged, null);
    }

    private void PublishChange(
        CadDocumentChangeKind kind,
        CadEntity? entity,
        CadEntityChangeKind? entityChangeKind = null,
        string? propertyName = null)
    {
        var change = new CadDocumentChangedEventArgs(
            kind,
            entity,
            entityChangeKind,
            propertyName);

        if (_changeSetDepth > 0)
            _pendingChanges.Add(change);

        Changed?.Invoke(this, change);

        if (_changeSetDepth == 0)
        {
            ChangeSetCommitted?.Invoke(
                this,
                new CadDocumentChangeSetEventArgs([change]));
        }
    }

    private void EndChangeSet()
    {
        if (_changeSetDepth <= 0)
            throw new InvalidOperationException(
                "CAD document change-set scope is unbalanced.");

        _changeSetDepth--;
        if (_changeSetDepth != 0 || _pendingChanges.Count == 0)
            return;

        var committed = _pendingChanges.ToArray();
        _pendingChanges.Clear();
        ChangeSetCommitted?.Invoke(
            this,
            new CadDocumentChangeSetEventArgs(committed));
    }

    private sealed class ChangeSetScope : IDisposable
    {
        private CadDocument? _document;

        public ChangeSetScope(CadDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public void Dispose()
        {
            var document = System.Threading.Interlocked.Exchange(ref _document, null);
            document?.EndChangeSet();
        }
    }

    private void RebuildPresentation(CadEntity entity)
    {
        var engine = _engine ?? throw new InvalidOperationException("No engine is attached.");

        var previous = entity.ViewerObject;
        var wasSelected =
            previous is not null &&
            engine.ContainsObject(previous.Id) &&
            engine.IsSelected(previous);

        IOcctObject? replacement = null;
        try
        {
            replacement = entity.BuildPresentation(engine);
            ApplyPlacement(entity, replacement);
            ApplyAppearance(entity, replacement);
        }
        catch
        {
            if (replacement is not null && engine.ContainsObject(replacement.Id))
                engine.Delete(replacement);
            throw;
        }

        if (previous is not null && engine.ContainsObject(previous.Id))
        {
            try
            {
                engine.Delete(previous);
            }
            catch
            {
                if (engine.ContainsObject(replacement.Id))
                    engine.Delete(replacement);
                throw;
            }
        }

        if (previous is not null)
            _viewerObjects.Remove(previous.Id);

        entity.ViewerObject = replacement;
        _viewerObjects[replacement.Id] = entity;

        if (wasSelected && IsEntitySelectable(entity))
            engine.SelectObject(replacement);
    }

    private void BuildPresentation(CadEntity entity)
    {
        var engine = _engine ?? throw new InvalidOperationException("No engine is attached.");
        var shape = entity.BuildPresentation(engine);
        entity.ViewerObject = shape;
        _viewerObjects[shape.Id] = entity;
        ApplyPlacement(entity, shape);
        ApplyAppearance(entity);
    }

    private void ApplyPlacement(CadEntity entity)
    {
        if (_engine is null || entity.ViewerObject is not { } value)
            return;
        ApplyPlacement(entity, value);
    }

    private void ApplyPlacement(CadEntity entity, IOcctObject value)
    {
        var engine = _engine ?? throw new InvalidOperationException("No engine is attached.");
        ApplyPlacement(engine, entity, value);
    }

    private static void ApplyPlacement(
        OcctEngine engine,
        CadEntity entity,
        IOcctObject value)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(entity);
        engine.SetLocalTransformation(value, entity.Placement.Transform);
    }

    private void ApplyAppearance(CadEntity entity)
    {
        if (_engine is null || entity.ViewerObject is not { } shape)
            return;
        ApplyAppearance(entity, shape);
    }

    private void ApplyAppearance(CadEntity entity, IOcctObject shape)
    {
        var engine = _engine ?? throw new InvalidOperationException("No engine is attached.");
        ApplyAppearance(engine, entity, shape);
    }

    private void ApplyAppearance(
        OcctEngine engine,
        CadEntity entity,
        IOcctObject shape)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(entity);

        var appearance = ResolveAppearance(entity);
        engine.SetObjectColor(shape, appearance.Color);
        engine.SetObjectTransparency(shape, entity.Transparency);

        if (shape is OcctShape)
        {
            engine.SetObjectLineWidth(shape, appearance.LineWidth);
            engine.SetObjectLineStyle(shape, appearance.LineStyle);
            engine.SetObjectMaterial(shape, entity.Material);
            engine.SetObjectDisplayMode(shape, entity.DisplayMode);
        }

        engine.SetObjectVisible(shape, appearance.Visible);
        engine.SetObjectSelectable(shape, appearance.Selectable);
    }

    private static bool IsRecoverablePresentationCleanupFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private void DeletePresentation(CadEntity entity)
    {
        if (entity.ViewerObject is not { } shape) return;
        _viewerObjects.Remove(shape.Id);
        if (_engine is { IsInitialized: true } engine && engine.ContainsObject(shape.Id))
            engine.Delete(shape);
        entity.ViewerObject = null;
    }

    public void Regenerate()
    {
        if (_engine is not { IsInitialized: true } engine)
            return;

        using var batch = engine.BeginDisplayBatch();
        foreach (var entity in _entities.ToArray())
            RebuildPresentation(entity);
    }
}
