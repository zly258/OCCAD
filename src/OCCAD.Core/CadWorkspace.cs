using OcctNet;

namespace OCCAD;

public readonly record struct CadResolvedPoint(
    OcctPoint3d Point,
    CadSnapPoint? Snap,
    CadTrackingResult? Tracking = null);

public readonly record struct CadPointerPosition(int X, int Y);

public sealed class CadWorkspace : IDisposable
{
    private bool _isModified;
    private bool _suppressModifiedTracking;
    private long _savedHistoryStateId;

    public CadWorkspace()
    {
        Entities = CadCoreRegistration.CreateEntityRegistry();
        Layers = new CadLayerManager();
        Document = new CadDocument(Layers);
        Selection = new CadSelectionManager(Document);
        Subobjects = new CadSubobjectSelectionManager(Document, Selection);
        Preselection = new CadPreselectionManager(Document, Selection);
        WorkPlane = new CadWorkPlane();
        Snap = new CadSnapManager(Document);
        Tracking = new CadTrackingManager();
        Precision = new CadPrecisionInputManager(WorkPlane, Tracking);
        Preview = new CadPreviewManager();
        Grips = new CadGripManager();
        History = new CadHistory();

        var toolRegistry = CadCoreRegistration.CreateToolRegistry();
        Tools = new CadToolManager(this, toolRegistry);
        Actions = new CadActionManager();
        CadCoreRegistration.RegisterActions(Actions, this);

        Document.ChangeSetCommitted += DocumentChangedForModification;
        Layers.Changed += LayersChangedForModification;
        History.Changed += HistoryChangedForModification;
        _savedHistoryStateId = History.CurrentStateId;
    }

    public CadEntityRegistry Entities { get; }
    public CadLayerManager Layers { get; }
    public CadDocument Document { get; }
    public CadSelectionManager Selection { get; }
    public CadSubobjectSelectionManager Subobjects { get; }
    public CadPreselectionManager Preselection { get; }
    public CadWorkPlane WorkPlane { get; }
    public CadSnapManager Snap { get; }
    public CadTrackingManager Tracking { get; }
    public CadPrecisionInputManager Precision { get; }
    public CadPreviewManager Preview { get; }
    public CadGripManager Grips { get; }
    public CadHistory History { get; }
    public CadToolManager Tools { get; }
    public CadActionManager Actions { get; }
    public CadResolvedPoint? LastResolvedPoint { get; private set; }
    public CadPointerPosition? LastPointerPosition { get; private set; }
    public OcctEngine? Engine { get; private set; }
    public bool IsModified => _isModified;

    public event EventHandler? ModifiedChanged;

    public void MarkSaved()
    {
        _savedHistoryStateId = History.CurrentStateId;
        SetModified(false);
    }

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException("The OCCT engine is not initialized.");
        if (ReferenceEquals(Engine, engine)) return;

        Tools.CancelCurrent();
        Engine = engine;
        var previousSuppress = _suppressModifiedTracking;
        _suppressModifiedTracking = true;
        try
        {
            Document.AttachEngine(engine);
        }
        finally
        {
            _suppressModifiedTracking = previousSuppress;
        }

        Selection.AttachEngine(engine);
        Snap.AttachEngine(engine);
        Tracking.AttachEngine(engine);
        Preview.AttachEngine(engine);
        Grips.AttachEngine(engine);
    }

    public void AddEntity(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.Layer = Layers.Current.Name;
        History.Execute(
            new CadAddEntitiesHistoryEntry(
                Document,
                [entity],
                $"Create {entity.Name}"));
    }

    internal IReadOnlyList<CadEntity> AddGeneratedEntities(
        IEnumerable<CadEntity> entities,
        string historyName)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentException.ThrowIfNullOrWhiteSpace(historyName);

        var values = entities.ToArray();
        if (values.Length == 0)
            return values;

        History.Execute(
            new CadAddEntitiesHistoryEntry(
                Document,
                values,
                historyName.Trim()));
        return values;
    }

    internal IReadOnlyList<CadEntity> ReplaceEntities(
        IEnumerable<CadEntity> before,
        IEnumerable<CadEntity> after,
        string historyName)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentException.ThrowIfNullOrWhiteSpace(historyName);

        var sources = before
            .Distinct()
            .Where(Document.Entities.Contains)
            .ToArray();
        if (sources.Length == 0)
            return Array.Empty<CadEntity>();

        var replacements = after.ToArray();
        Selection.Clear();
        Subobjects.Clear();
        Preselection.Clear();
        Grips.Clear();
        History.Execute(
            new CadReplaceEntitiesHistoryEntry(
                Document,
                sources,
                replacements,
                historyName.Trim()));
        return replacements;
    }

    internal void ApplyGeneratedGeometryChange(
        IEnumerable<CadEntity> entities,
        string historyName,
        Action<CadEntity> action) =>
        ApplyGeometryChange(
            EditableEntities(entities),
            historyName,
            action);

    public void DeleteEntities(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var values = entities
            .Distinct()
            .Where(Document.IsEntitySelectable)
            .ToArray();
        if (values.Length == 0) return;

        Selection.Clear();
        Subobjects.Clear();
        Preselection.Clear();
        Grips.Clear();
        History.Execute(new CadRemoveEntitiesHistoryEntry(Document, values));
    }

    public void ClearModel()
    {
        Tools.CancelCurrent();
        Selection.Clear();
        Subobjects.Clear();
        Preselection.Clear();
        Tracking.Clear();
        Preview.Clear();
        Grips.Clear();

        var entities = Document.Entities.ToArray();
        if (entities.Length == 0) return;
        History.Execute(new CadRemoveEntitiesHistoryEntry(Document, entities));
    }

    public CadLayer AddLayer(string name, bool makeCurrent = true)
    {
        var previousCurrent = Layers.Current;
        var layer = Layers.Add(name);
        if (makeCurrent)
            Layers.SetCurrent(layer);

        History.RecordApplied(
            new CadAddLayerHistoryEntry(
                Layers,
                layer,
                previousCurrent,
                makeCurrent));
        return layer;
    }

    public CadLayer AddNextLayer()
    {
        for (var index = 1; ; index++)
        {
            var name = $"Layer{index}";
            if (Layers.TryGet(name) is not null) continue;
            return AddLayer(name);
        }
    }

    public void SetCurrentLayer(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _ = Layers.IndexOf(layer);
        var before = Layers.Current;
        if (ReferenceEquals(before, layer)) return;

        History.Execute(
            new CadSetCurrentLayerHistoryEntry(
                Layers,
                before,
                layer));
    }

    public void RenameLayer(CadLayer layer, string name)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var before = CaptureLayerState(layer);
        Layers.Rename(layer, name);
        RecordLayerStateChange(layer, before, "Rename Layer");
    }

    public void RemoveLayer(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _ = Layers.IndexOf(layer);

        var entities = Document.GetEntitiesByLayer(layer.Name);
        if (entities.Count > 0)
            throw new InvalidOperationException(
                $"Layer '{layer.Name}' contains {entities.Count} " +
                "entities and cannot be removed.");

        History.Execute(new CadRemoveLayerHistoryEntry(Layers, layer));
    }

    public void AssignEntitiesToLayer(
        IEnumerable<CadEntity> entities,
        string layerName)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var layer = Layers.GetRequired(layerName);
        ApplyStateChange(
            entities,
            $"Assign Layer {layer.Name}",
            entity => entity.Layer = layer.Name);
    }

    public void SetLayerVisible(CadLayer layer, bool visible) =>
        ApplyLayerChange(
            layer,
            "Layer Visibility",
            () => layer.Visible = visible);

    public void SetLayerLocked(CadLayer layer, bool locked) =>
        ApplyLayerChange(
            layer,
            "Layer Lock",
            () => layer.Locked = locked);

    public void HideEntities(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var targets = entities
            .Distinct()
            .Where(Document.Entities.Contains)
            .Where(static entity => entity.Visible)
            .ToArray();
        ApplyStateChange(
            targets,
            targets.Length == 1 ? "Hide" : $"Hide {targets.Length}",
            static entity => entity.Visible = false,
            requireSelectable: false);
    }

    public void IsolateEntities(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var isolated = entities
            .Distinct()
            .Where(Document.Entities.Contains)
            .ToHashSet();
        if (isolated.Count == 0) return;

        var targets = Document.Entities
            .Where(entity => entity.Visible != isolated.Contains(entity))
            .ToArray();
        ApplyStateChange(
            targets,
            "Isolate",
            entity => entity.Visible = isolated.Contains(entity),
            requireSelectable: false);
    }

    public void ShowAllEntities()
    {
        var targets = Document.Entities
            .Where(static entity => !entity.Visible)
            .ToArray();
        ApplyStateChange(
            targets,
            "Show All",
            static entity => entity.Visible = true,
            requireSelectable: false);
    }

    public void SetDisplayMode(OcctDisplayMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        var targets = (Selection.Selected.Count > 0
                ? Selection.Selected
                : Document.Entities)
            .Where(entity => entity.DisplayMode != mode)
            .ToArray();

        ApplyStateChange(
            targets,
            $"Display {mode}",
            entity => entity.DisplayMode = mode,
            requireSelectable: false);
    }

    public void TranslateEntities(
        IEnumerable<CadEntity> entities,
        OcctVector3d displacement)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement));
        if (displacement == OcctVector3d.Zero) return;

        ApplyGeometryChange(
            EditableEntities(entities),
            "Move",
            entity => entity.Translate(displacement));
    }

    public void RotateEntities(IEnumerable<CadEntity> entities, OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        if (!axis.TryNormalize(out var normal)) throw new ArgumentOutOfRangeException(nameof(axis));
        if (!double.IsFinite(angleDegrees)) throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        if (Math.Abs(angleDegrees % 360.0) <= 1e-12) return;
        ApplyGeometryChange(EditableEntities(entities), "Rotate", entity => entity.Rotate(center, normal, angleDegrees));
    }

    public void ScaleEntities(IEnumerable<CadEntity> entities, OcctPoint3d center, double factor)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        if (!double.IsFinite(factor) || factor <= 0.0) throw new ArgumentOutOfRangeException(nameof(factor));
        if (Math.Abs(factor - 1.0) <= 1e-12) return;
        ApplyGeometryChange(EditableEntities(entities), "Scale", entity => entity.Scale(center, factor));
    }
    public IReadOnlyList<CadEntity> CopyEntities(
        IEnumerable<CadEntity> entities,
        OcctVector3d displacement)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement));

        var copies = EditableEntities(entities)
            .Select(static entity => entity.Duplicate())
            .ToArray();
        if (copies.Length == 0) return copies;

        foreach (var copy in copies)
            copy.Translate(displacement);

        History.Execute(
            new CadAddEntitiesHistoryEntry(
                Document,
                copies,
                copies.Length == 1 ? "Copy" : $"Copy {copies.Length}"));
        return copies;
    }

    public IReadOnlyList<CadEntity> CreateRectangularArray(IEnumerable<CadEntity> entities,
        int columns, int rows, OcctVector3d columnStep, OcctVector3d rowStep)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var sources = EditableEntities(entities).ToArray();
        var copies = CadRectangularArray.CreateCopies(sources, columns, rows, columnStep, rowStep);
        History.Execute(new CadAddEntitiesHistoryEntry(Document, copies, "Rectangular Array"));
        return copies;
    }
    public IReadOnlyList<CadEntity> CreateCircularArray(IEnumerable<CadEntity> entities, OcctPoint3d center,
        OcctVector3d axis, int count, double sweep = 360, bool rotateItems = true, OcctPoint3d? reference = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var copies = CadCircularArray.CreateCopies(EditableEntities(entities).ToArray(), center, axis, count, sweep, rotateItems, reference);
        History.Execute(new CadAddEntitiesHistoryEntry(Document, copies, "Circular Array"));
        return copies;
    }
    public IReadOnlyList<CadEntity> CreatePathArray(IEnumerable<CadEntity> entities, CadEntity pathEntity,
        double spacing, OcctPoint3d reference, OcctVector3d forward, OcctVector3d up, bool align = true)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(pathEntity);
        if (!Document.IsEntitySelectable(pathEntity)) throw new ArgumentException("Path is not editable in this document.", nameof(pathEntity));
        var sources = EditableEntities(entities).ToArray();
        if (sources.Contains(pathEntity)) throw new ArgumentException("The path cannot also be a source object.", nameof(pathEntity));
        using var path = new CadArrayPath(pathEntity);
        var copies = CadPathArray.CreateCopies(sources, path, spacing, reference, forward, up, align);
        History.Execute(new CadAddEntitiesHistoryEntry(Document, copies, "Path Array"));
        return copies;
    }
    public IReadOnlyList<CadEntity> MirrorEntities(IEnumerable<CadEntity> entities, OcctPoint3d origin,
        OcctVector3d normal, bool keepSource = true)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var sources = EditableEntities(entities).ToArray();
        var copies = sources.Select(entity => entity.MirroredCopy(origin, normal)).ToArray();
        if (copies.Length == 0) return copies;
        if (keepSource) History.Execute(new CadAddEntitiesHistoryEntry(Document, copies, "Mirror"));
        else
        {
            var geometry = sources.Zip(copies).ToDictionary(pair => pair.First, pair => pair.Second);
            ApplyGeometryChange(sources, "Mirror", entity => entity.RestoreGeometry(geometry[entity]));
        }
        return keepSource ? copies : sources;
    }
    public IReadOnlyList<CadEntity> CaptureEntityStates(
        IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return entities
            .Distinct()
            .Select(static entity => entity.Duplicate())
            .ToArray();
    }

    public CadLayerState CaptureLayerState(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        Layers.GetRequired(layer.Name);
        return layer.CaptureState();
    }

    public void RecordEntityStateChange(
        IEnumerable<CadEntity> entities,
        IReadOnlyList<CadEntity> before,
        string name)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(before);
        var targets = entities.Distinct().ToArray();
        if (targets.Length == 0) return;
        if (targets.Length != before.Count)
            throw new ArgumentException(
                "Entity state snapshot count does not match.",
                nameof(before));

        var after = targets
            .Select(static entity => entity.Duplicate())
            .ToArray();
        History.RecordApplied(
            new CadEntityStateHistoryEntry(
                targets,
                before,
                after,
                name));
        Selection.RefreshValidity();
    }

    public void RecordLayerStateChange(
        CadLayer layer,
        CadLayerState before,
        string name)
    {
        ArgumentNullException.ThrowIfNull(layer);
        Layers.GetRequired(layer.Name);
        var after = layer.CaptureState();
        if (before == after) return;

        History.RecordApplied(
            new CadLayerStateHistoryEntry(
                layer,
                before,
                after,
                name));
        Selection.RefreshValidity();
    }

    public bool Undo()
    {
        Tools.CancelCurrent();
        Selection.Clear();
        Subobjects.Clear();
        Grips.Clear();
        using var changes = Document.BeginChangeSet();
        return History.Undo();
    }

    public bool Redo()
    {
        Tools.CancelCurrent();
        Selection.Clear();
        Subobjects.Clear();
        Grips.Clear();
        using var changes = Document.BeginChangeSet();
        return History.Redo();
    }

    public void ResetDocument()
    {
        var previousSuppress = _suppressModifiedTracking;
        _suppressModifiedTracking = true;
        try
        {
            Tools.CancelCurrent();
            Selection.Clear();
            Subobjects.Clear();
            Preselection.Clear();
            Tracking.Clear();
            Preview.Clear();
            Grips.Clear();
            Document.Clear();
            Layers.Reset();
            History.Clear();
            LastResolvedPoint = null;
            LastPointerPosition = null;
        }
        finally
        {
            _suppressModifiedTracking = previousSuppress;
        }

        _savedHistoryStateId = History.CurrentStateId;
        SetModified(false);
    }

    internal void ObservePointer(int x, int y) =>
        LastPointerPosition = new CadPointerPosition(x, y);

    public CadResolvedPoint ResolvePoint(
        int x,
        int y,
        OcctPoint3d? constraintOrigin = null)
    {
        ObservePointer(x, y);
        var engine = Engine ??
            throw new InvalidOperationException("No OCCT engine is attached.");

        OcctPoint3d point;
        if (WorkPlane.IsActive)
        {
            var ray = engine.GetViewRay(x, y);
            if (!WorkPlane.TryIntersect(ray, out point))
                throw new InvalidOperationException(
                    "The view ray is parallel to the active work plane.");
        }
        else
        {
            point = engine.ScreenToWorld(x, y);
        }

        var snap = Snap.Resolve(
            x,
            y,
            point,
            WorkPlane,
            constraintOrigin);
        if (snap is { } value)
        {
            point = value.Position;
            // Hovering a snap target must not replace the drawing frame or unlock it.
            // Tools capture their frame when activated; only an explicit plane change
            // may reinitialize that frame before the first point.
        }

        CadTrackingResult? tracking = null;
        if (constraintOrigin is { } trackingOrigin)
        {
            if (snap is null) tracking = WorkPlane.Track(trackingOrigin, point);
            if (tracking is { } tracked)
                point = tracked.Point;

            var constrained = WorkPlane.Constrain(trackingOrigin, point);
            if (snap is not null && constrained.DistanceTo(point) > 1e-9)
            {
                // An explicit dimension wins over an incompatible object snap.
                snap = null;
                Snap.Clear();
            }
            point = constrained;
            if (tracking is { } activeTracking)
                tracking = activeTracking with { Point = point };
        }

        if (constraintOrigin is not null)
            Tracking.Update(WorkPlane, point, tracking);
        else
            Tracking.Clear();

        var resolved = new CadResolvedPoint(point, snap, tracking);
        LastResolvedPoint = resolved;
        return resolved;
    }

    public void Dispose()
    {
        Document.ChangeSetCommitted -= DocumentChangedForModification;
        Layers.Changed -= LayersChangedForModification;
        History.Changed -= HistoryChangedForModification;
        Tools.CancelCurrent();
        Snap.Clear();
        Tracking.Clear();
        Preview.Clear();
        Grips.Clear();
        Selection.Clear();
        Subobjects.Clear();
        Document.DetachEngine(deletePresentation: true);
        Engine = null;
        LastResolvedPoint = null;
        LastPointerPosition = null;
    }

    internal CadEntity CaptureGeometry(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.Duplicate();
    }

    internal void RecordGeometryChange(
        CadEntity entity,
        CadEntity before,
        string name)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(before);

        var after = entity.Duplicate();
        if (Entities.GeometryEquals(before, after))
            return;

        History.RecordApplied(
            new CadGeometryHistoryEntry(
                [entity],
                [before],
                [after],
                name));
    }

    private void DocumentChangedForModification(
        object? sender,
        CadDocumentChangeSetEventArgs args)
    {
        if (!_suppressModifiedTracking)
            SetModified(true);
    }

    private void LayersChangedForModification(
        object? sender,
        CadLayerManagerChangedEventArgs args)
    {
        if (!_suppressModifiedTracking)
            SetModified(true);
    }

    private void HistoryChangedForModification(
        object? sender,
        CadHistoryChangedEventArgs args)
    {
        if (_suppressModifiedTracking)
            return;

        SetModified(args.StateId != _savedHistoryStateId);
    }

    private void SetModified(bool value)
    {
        if (_isModified == value) return;
        _isModified = value;
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<CadEntity> EditableEntities(
        IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return entities
            .Distinct()
            .Where(Document.IsEntitySelectable)
            .ToArray();
    }

    private void ApplyGeometryChange(
        IEnumerable<CadEntity> entities,
        string name,
        Action<CadEntity> action)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(action);
        var targets = entities.Distinct().ToArray();
        if (targets.Length == 0) return;

        var before = targets
            .Select(static entity => entity.Duplicate())
            .ToArray();
        using var changes = Document.BeginChangeSet();
        try
        {
            foreach (var entity in targets)
                action(entity);

            var after = targets
                .Select(static entity => entity.Duplicate())
                .ToArray();
            History.RecordApplied(
                new CadGeometryHistoryEntry(
                    targets,
                    before,
                    after,
                    name));
        }
        catch
        {
            for (var index = 0; index < targets.Length; index++)
                targets[index].RestoreGeometry(before[index]);
            throw;
        }
    }

    private void ApplyLayerChange(
        CadLayer layer,
        string name,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(action);
        Layers.GetRequired(layer.Name);
        var before = layer.CaptureState();
        action();
        var after = layer.CaptureState();
        if (before == after) return;

        History.RecordApplied(
            new CadLayerStateHistoryEntry(
                layer,
                before,
                after,
                name));
        Selection.RefreshValidity();
    }

    private void ApplyStateChange(
        IEnumerable<CadEntity> entities,
        string name,
        Action<CadEntity> action,
        bool requireSelectable = true)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(action);

        var candidates = entities
            .Distinct()
            .Where(Document.Entities.Contains);
        var targets = (requireSelectable
                ? candidates.Where(Document.IsEntitySelectable)
                : candidates)
            .ToArray();
        if (targets.Length == 0) return;

        var before = targets
            .Select(static entity => entity.Duplicate())
            .ToArray();
        using var changes = Document.BeginChangeSet();
        try
        {
            foreach (var entity in targets)
                action(entity);

            var after = targets
                .Select(static entity => entity.Duplicate())
                .ToArray();
            History.RecordApplied(
                new CadEntityStateHistoryEntry(
                    targets,
                    before,
                    after,
                    name));
            Selection.RefreshValidity();
        }
        catch
        {
            for (var index = 0; index < targets.Length; index++)
                targets[index].RestoreState(before[index]);
            throw;
        }
    }
}
