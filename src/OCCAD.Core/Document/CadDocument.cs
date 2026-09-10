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
        if (!engine.IsInitialized) throw new InvalidOperationException("The OCCT engine is not initialized.");
        if (ReferenceEquals(_engine, engine)) return;
        _engine = engine;
        _viewerObjects.Clear();
        using (engine.BeginDisplayBatch())
        {
            foreach (var entity in _entities)
            {
                entity.ViewerObject = null;
                BuildPresentation(entity);
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
                    if (entity.ViewerObject is { } shape && engine.ContainsObject(shape.Id)) engine.Delete(shape);
                    entity.ViewerObject = null;
                }
            }
        }
        else
        {
            foreach (var entity in _entities) entity.ViewerObject = null;
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
        foreach (var entity in values) _layers.GetRequired(entity.Layer);

        // Build all native presentations before registering entities or publishing
        // document changes. A later failure must not leave a partial array/import.
        if (values.Any(static entity => entity.ViewerObject is not null))
            throw new InvalidOperationException("An entity already has a viewer presentation.");
        using var batch = _engine is { IsInitialized: true } engine ? engine.BeginDisplayBatch() : null;
        try
        {
            if (_engine is { IsInitialized: true })
                foreach (var entity in values) BuildPresentation(entity);
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (var entity in values.Reverse())
            {
                try { DeletePresentation(entity); }
                catch (Exception cleanupFailure) { failures.Add(cleanupFailure); }
            }
            if (failures.Count > 1) throw new AggregateException("Entity creation and cleanup failed.", failures);
            throw;
        }

        using var changes = BeginChangeSet();
        foreach (var entity in values)
        {
            _entities.Add(entity);
            _entitiesById.Add(entity.Id, entity);
            entity.Changed += EntityChanged;
        }
        foreach (var entity in values) PublishChange(CadDocumentChangeKind.Added, entity);
    }
    public int RemoveRange(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var values = entities.Distinct().Where(_entities.Contains).ToArray();
        if (values.Length == 0) return 0;

        var selected = _engine is { IsInitialized: true } currentEngine
            ? values.Where(entity => entity.ViewerObject is { } shape && currentEngine.ContainsObject(shape.Id) && currentEngine.IsSelected(shape)).ToHashSet()
            : [];
        try
        {
            using var batch = _engine is { IsInitialized: true } engine ? engine.BeginDisplayBatch() : null;
            foreach (var entity in values) DeletePresentation(entity);
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (var entity in values)
            {
                try
                {
                    if (_engine is not { IsInitialized: true } engine)
                        throw new InvalidOperationException("Cannot restore presentation without an initialized engine.");
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
                catch (Exception restoreFailure) { failures.Add(restoreFailure); }
            }
            if (failures.Count > 1) throw new AggregateException("Entity removal and presentation recovery failed.", failures);
            throw;
        }

        using var changes = BeginChangeSet();
        foreach (var entity in values)
        {
            _entities.Remove(entity);
            _entitiesById.Remove(entity.Id);
            entity.Changed -= EntityChanged;
        }
        foreach (var entity in values) PublishChange(CadDocumentChangeKind.Removed, entity);
        return values.Length;
    }
    public CadResolvedAppearance ResolveAppearance(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var layer = _layers.GetRequired(entity.Layer);
        var visible = entity.Visible && layer.Visible;
        return new CadResolvedAppearance(
            entity.ColorByLayer ? layer.Color : entity.Color,
            entity.LineWidthByLayer ? layer.LineWidth : entity.LineWidth,
            entity.LineStyleByLayer ? layer.LineStyle : entity.LineStyle,
            visible,
            entity.Selectable && visible && !layer.Locked);
    }

    public bool IsEntityVisible(CadEntity entity) => ResolveAppearance(entity).Visible;

    public bool IsEntitySelectable(CadEntity entity) =>
        _entitiesById.TryGetValue(entity.Id, out var member) && ReferenceEquals(member, entity) &&
        ResolveAppearance(entity).Selectable;

    public IReadOnlyList<CadEntity> GetEntitiesByLayer(string layerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerName);
        var normalized = layerName.Trim();
        return _entities
            .Where(entity => string.Equals(
                entity.Layer,
                normalized,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void EntityChanged(object? sender, CadEntityChangedEventArgs args)
    {
        if (sender is not CadEntity entity) return;
        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            if (args.Kind == CadEntityChangeKind.Geometry)
                RebuildPresentation(entity);
            else if (args.Kind == CadEntityChangeKind.Appearance ||
                     (args.Kind == CadEntityChangeKind.Metadata && args.PropertyName == nameof(CadEntity.Layer)))
                ApplyAppearance(entity);
        }
        PublishChange(
            CadDocumentChangeKind.Changed,
            entity,
            args.Kind,
            args.PropertyName);
    }

    private void LayersChanged(object? sender, CadLayerManagerChangedEventArgs args)
    {
        using var changes = BeginChangeSet();

        if (args.Kind == CadLayerManagerChangeKind.LayerChanged &&
            args.LayerChangeKind == CadLayerChangeKind.Metadata &&
            args.Layer is { } renamedLayer &&
            !string.IsNullOrWhiteSpace(args.PreviousName))
        {
            var affected = GetEntitiesByLayer(args.PreviousName);
            foreach (var entity in affected)
                entity.Layer = renamedLayer.Name;
        }

        if (args.Kind is CadLayerManagerChangeKind.Added or CadLayerManagerChangeKind.CurrentChanged)
            return;

        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            foreach (var entity in _entities)
            {
                if (args.Kind == CadLayerManagerChangeKind.Reset ||
                    args.Layer is null ||
                    string.Equals(entity.Layer, args.Layer.Name, StringComparison.OrdinalIgnoreCase))
                    ApplyAppearance(entity);
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
        {
            throw new InvalidOperationException(
                "CAD document change-set scope is unbalanced.");
        }

        _changeSetDepth--;
        if (_changeSetDepth != 0 ||
            _pendingChanges.Count == 0)
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
            _document = document ??
                throw new ArgumentNullException(nameof(document));
        }

        public void Dispose()
        {
            var document =
                System.Threading.Interlocked.Exchange(
                    ref _document,
                    null);
            document?.EndChangeSet();
        }
    }

    private void RebuildPresentation(CadEntity entity)
    {
        var engine =
            _engine ??
            throw new InvalidOperationException(
                "No engine is attached.");

        var previous = entity.ViewerObject;
        var wasSelected =
            previous is not null &&
            engine.ContainsObject(previous.Id) &&
            engine.IsSelected(previous);

        IOcctObject? replacement = null;
        try
        {
            replacement = entity.BuildPresentation(engine);
            ApplyAppearance(entity, replacement);
        }
        catch
        {
            if (replacement is not null &&
                engine.ContainsObject(replacement.Id))
                engine.Delete(replacement);
            throw;
        }

        if (previous is not null &&
            engine.ContainsObject(previous.Id))
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

        if (wasSelected &&
            IsEntitySelectable(entity))
            engine.SelectObject(replacement);
    }

    private void BuildPresentation(CadEntity entity)
    {
        var engine = _engine ?? throw new InvalidOperationException("No engine is attached.");
        var shape = entity.BuildPresentation(engine);
        entity.ViewerObject = shape;
        _viewerObjects[shape.Id] = entity;
        ApplyAppearance(entity);
    }

    private void ApplyAppearance(CadEntity entity)
    {
        if (_engine is null ||
            entity.ViewerObject is not { } shape)
            return;

        ApplyAppearance(entity, shape);
    }

    private void ApplyAppearance(
        CadEntity entity,
        IOcctObject shape)
    {
        var engine =
            _engine ??
            throw new InvalidOperationException(
                "No engine is attached.");

        var appearance = ResolveAppearance(entity);
        engine.SetObjectColor(
            shape,
            appearance.Color);
        engine.SetObjectTransparency(
            shape,
            entity.Transparency);

        if (shape is OcctShape)
        {
            engine.SetObjectLineWidth(
                shape,
                appearance.LineWidth);
            engine.SetObjectLineStyle(
                shape,
                appearance.LineStyle);
            engine.SetObjectMaterial(
                shape,
                entity.Material);
            engine.SetObjectDisplayMode(
                shape,
                entity.DisplayMode);
        }

        engine.SetObjectVisible(
            shape,
            appearance.Visible);
        engine.SetObjectSelectable(
            shape,
            appearance.Selectable);
    }

    private void DeletePresentation(CadEntity entity)
    {
        if (entity.ViewerObject is not { } shape) return;
        _viewerObjects.Remove(shape.Id);
        if (_engine is { IsInitialized: true } engine && engine.ContainsObject(shape.Id)) engine.Delete(shape);
        entity.ViewerObject = null;
    }
}

