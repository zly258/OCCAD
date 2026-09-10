using System.Drawing;
using OcctNet;

namespace OCCAD;

public readonly record struct CadResolvedPoint(
    OcctPoint3d Point,
    CadSnapPoint? Snap,
    CadTrackingResult? Tracking = null);

public readonly record struct CadPointerPosition(int X, int Y);

public sealed class CadWorkspace : IDisposable
{
    private readonly CadPointResolver _pointResolver;
    private bool _isModified;
    private bool _suppressModifiedTracking;
    private long _savedHistoryStateId;

    public CadWorkspace()
    {
        _pointResolver = new CadPointResolver(this);
        Entities = CadCoreRegistration.CreateEntityRegistry();
        Layers = new CadLayerManager();
        Document = new CadDocument(Layers);
        Selection = new CadSelectionManager(Document);
        Subobjects = new CadSubobjectSelectionManager(Document, Selection);
        Preselection = new CadPreselectionManager(Document, Selection);
        WorkPlane = new CadWorkPlane();
        Drafting = new CadDraftingSettings();
        Snap = new CadSnapManager(Document);
        Tracking = new CadTrackingManager();
        Precision = new CadPrecisionInputManager(Drafting, Tracking);
        Preview = new CadPreviewManager(Document);
        Grips = new CadGripManager();
        Transients = new CadTransientScene();
        Transients.Register(CadTransientChannel.ToolPreview, Preview.Clear, () => Preview.HasTransient);
        Transients.Register(CadTransientChannel.Snap, Snap.Clear, () => Snap.HasTransient);
        Transients.Register(CadTransientChannel.Tracking, Tracking.Clear, () => Tracking.HasTransient);
        Transients.Register(CadTransientChannel.GripDrag, Grips.ClearDragMarker, () => Grips.HasDragTransient);
        Transients.Register(CadTransientChannel.Preselection, Preselection.Clear, () => Preselection.Current is not null);
        History = new CadHistory();

        var toolRegistry = CadCoreRegistration.CreateToolRegistry();
        Tools = new CadToolManager(this, toolRegistry);
        Selection.Changed += FormalSelectionChanged;
        Subobjects.Changed += SubobjectSelectionChanged;
        Actions = new CadActionManager();
        CadCoreRegistration.RegisterActions(Actions, this);

        Document.ChangeSetCommitted += DocumentChangedForModification;
        Layers.Changed += LayersChangedForModification;
        History.Changed += HistoryChangedForModification;
        _savedHistoryStateId = History.CurrentStateId;
        Events = new CadWorkspaceEvents(this);
    }

    public CadEntityRegistry Entities { get; }
    public CadLayerManager Layers { get; }
    public CadDocument Document { get; }
    public CadSelectionManager Selection { get; }
    public CadSubobjectSelectionManager Subobjects { get; }
    public CadPreselectionManager Preselection { get; }
    public CadWorkPlane WorkPlane { get; }
    public CadDraftingSettings Drafting { get; }
    public CadSnapManager Snap { get; }
    public CadTrackingManager Tracking { get; }
    public CadPrecisionInputManager Precision { get; }
    public CadPreviewManager Preview { get; }
    public CadGripManager Grips { get; }
    public CadTransientScene Transients { get; }
    public CadWorkspaceEvents Events { get; }
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

    public void ClearPointerObservation()
    {
        LastResolvedPoint = null;
        LastPointerPosition = null;
        Snap.Clear();
        Tracking.Clear();
    }

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!engine.IsInitialized)
            throw new InvalidOperationException("The OCCT engine is not initialized.");
        if (ReferenceEquals(Engine, engine)) return;

        Tools.CancelCurrent();
        ClearPointerObservation();
        var previousEngine = Engine;
        var previousSuppress = _suppressModifiedTracking;
        Engine = engine;
        _suppressModifiedTracking = true;
        try
        {
            Document.AttachEngine(engine);
        }
        catch
        {
            Engine = previousEngine;
            throw;
        }
        finally
        {
            _suppressModifiedTracking = previousSuppress;
        }

        Selection.AttachEngine(engine);
        Subobjects.AttachEngine(engine);
        Snap.AttachEngine(engine);
        Tracking.AttachEngine(engine);
        Preview.AttachEngine(engine);
        Grips.AttachEngine(engine);
    }

    public void AddEntity(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.Layer = Layers.Current.Name;
        CadTransaction.Execute(this,
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

        CadTransaction.Execute(this,
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
        CadTransaction.Execute(this,
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
        CadTransaction.Execute(this, new CadRemoveEntitiesHistoryEntry(Document, values));
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
        CadTransaction.Execute(this, new CadRemoveEntitiesHistoryEntry(Document, entities));
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

        CadTransaction.Execute(this,
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

        CadTransaction.Execute(this, new CadRemoveLayerHistoryEntry(Layers, layer));
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

    public void SetLayerColor(CadLayer layer, Color color) =>
        ApplyLayerChange(
            layer,
            "Layer Color",
            () => layer.Color = color);

    public void SetLayerLineStyle(
        CadLayer layer,
        OcctLineStyle lineStyle) =>
        ApplyLayerChange(
            layer,
            "Layer Line Style",
            () => layer.LineStyle = lineStyle);

    public void SetLayerLineWidth(
        CadLayer layer,
        double lineWidth) =>
        ApplyLayerChange(
            layer,
            "Layer Line Width",
            () => layer.LineWidth = lineWidth);

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

    public void SetTransparency(double transparency)
    {
        var targets = (Selection.Selected.Count > 0
                ? Selection.Selected
                : Document.Entities)
            .ToArray();

        ApplyStateChange(
            targets,
            $"Display Transparency {transparency:P0}",
            entity =>
            {
                entity.DisplayMode = OcctDisplayMode.Shaded;
                entity.Transparency = Math.Clamp(transparency, 0.0, 1.0);
            },
            requireSelectable: false);
    }

    public void SetHiddenLineMode()
    {
        var targets = (Selection.Selected.Count > 0
                ? Selection.Selected
                : Document.Entities)
            .ToArray();

        ApplyStateChange(
            targets,
            "Display Hidden Line",
            entity =>
            {
                entity.DisplayMode = OcctDisplayMode.Wireframe;
            },
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
            entity => entity.TranslatePlacement(displacement));
    }

    public void RotateEntities(IEnumerable<CadEntity> entities, OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        if (!axis.TryNormalize(out var normal)) throw new ArgumentOutOfRangeException(nameof(axis));
        if (!double.IsFinite(angleDegrees)) throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        if (Math.Abs(angleDegrees % 360.0) <= 1e-12) return;
        ApplyGeometryChange(
            EditableEntities(entities),
            "Rotate",
            entity => entity.RotatePlacement(
                center,
                normal,
                angleDegrees));
    }

    public void ScaleEntities(IEnumerable<CadEntity> entities, OcctPoint3d center, double factor)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!center.IsFinite) throw new ArgumentOutOfRangeException(nameof(center));
        if (!double.IsFinite(factor) || factor <= 0.0) throw new ArgumentOutOfRangeException(nameof(factor));
        if (Math.Abs(factor - 1.0) <= 1e-12) return;
        ApplyGeometryChange(
            EditableEntities(entities),
            "Scale",
            entity => entity.ScaleFromWorld(
                center,
                factor));
    }
    public IReadOnlyList<CadEntity> CopyEntities(
        IEnumerable<CadEntity> entities,
        OcctVector3d displacement,
        int count = 1)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement));
        if (displacement.LengthSquared <= 1e-18)
            return Array.Empty<CadEntity>();
        if (count is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(count));

        var sources = EditableEntities(entities).ToArray();
        if (sources.Length == 0)
            return Array.Empty<CadEntity>();

        var copies = new List<CadEntity>(
            sources.Length * count);
        for (var index = 1; index <= count; index++)
        {
            var step = displacement * index;
            foreach (var source in sources)
            {
                var copy = source.Duplicate();
                copy.TranslatePlacement(step);
                copies.Add(copy);
            }
        }

        CadTransaction.Execute(this,
            new CadAddEntitiesHistoryEntry(
                Document,
                copies,
                copies.Count == 1
                    ? "Copy"
                    : $"Copy {copies.Count}"));
        return copies;
    }

    public IReadOnlyList<CadEntity> CreateRectangularArray(IEnumerable<CadEntity> entities,
        int columns, int rows, OcctVector3d columnStep, OcctVector3d rowStep)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var sources = EditableEntities(entities).ToArray();
        var copies = CadRectangularArray.CreateCopies(sources, columns, rows, columnStep, rowStep);
        CadTransaction.Execute(this, new CadAddEntitiesHistoryEntry(Document, copies, "Rectangular Array"));
        return copies;
    }
    public IReadOnlyList<CadEntity> CreateCircularArray(IEnumerable<CadEntity> entities, OcctPoint3d center,
        OcctVector3d axis, int count, double sweep = 360, bool rotateItems = true, OcctPoint3d? reference = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var copies = CadCircularArray.CreateCopies(EditableEntities(entities).ToArray(), center, axis, count, sweep, rotateItems, reference);
        CadTransaction.Execute(this, new CadAddEntitiesHistoryEntry(Document, copies, "Circular Array"));
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
        CadTransaction.Execute(this, new CadAddEntitiesHistoryEntry(Document, copies, "Path Array"));
        return copies;
    }
    public IReadOnlyList<CadEntity> MirrorEntities(IEnumerable<CadEntity> entities, OcctPoint3d origin,
        OcctVector3d normal, bool keepSource = true)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var sources = EditableEntities(entities).ToArray();
        var copies = sources.Select(entity => entity.MirroredCopy(origin, normal)).ToArray();
        if (copies.Length == 0) return copies;
        if (keepSource) CadTransaction.Execute(this, new CadAddEntitiesHistoryEntry(Document, copies, "Mirror"));
        else
        {
            var geometry = sources.Zip(copies).ToDictionary(pair => pair.First, pair => pair.Second);
            ApplyGeometryChange(sources, "Mirror", entity => entity.RestoreGeometrySnapshot(geometry[entity]));
        }
        return keepSource ? copies : sources;
    }

    public CadTransaction BeginTransaction(string name = "Modify") => new(this, name);

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
        Subobjects.RefreshValidity();
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
        Subobjects.RefreshValidity();
    }

    public bool Undo()
    {
        Tools.CancelCurrent();
        Selection.Clear();
        Subobjects.Clear();
        Preselection.Clear();
        Grips.Clear();
        bool changed;
        using (Document.BeginChangeSet()) changed = History.Undo();
        if (changed) SetModified(History.CurrentStateId != _savedHistoryStateId);
        return changed;
    }

    public bool Redo()
    {
        Tools.CancelCurrent();
        Selection.Clear();
        Subobjects.Clear();
        Preselection.Clear();
        Grips.Clear();
        bool changed;
        using (Document.BeginChangeSet()) changed = History.Redo();
        if (changed) SetModified(History.CurrentStateId != _savedHistoryStateId);
        return changed;
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

    public CadResolvedPoint ResolvePoint(int x, int y, OcctPoint3d? constraintOrigin = null,
        CadSnapResolvePolicy snapPolicy = default)
    {
        ObservePointer(x, y);
        var resolved = _pointResolver.Resolve(x, y, constraintOrigin, snapPolicy);
        LastResolvedPoint = resolved;
        return resolved;
    }

    public void Dispose()
    {
        List<Exception> failures = [];

        // Disconnect internal observers first so cleanup cannot recursively
        // rebuild grips, modified state, or workspace event projections while
        // the object graph is being torn down.
        Cleanup(Events.Dispose);
        Selection.Changed -= FormalSelectionChanged;
        Subobjects.Changed -= SubobjectSelectionChanged;
        Document.ChangeSetCommitted -= DocumentChangedForModification;
        Layers.Changed -= LayersChangedForModification;
        History.Changed -= HistoryChangedForModification;

        Cleanup(() => Tools.CancelCurrent());
        Cleanup(Transients.ClearAll);
        Cleanup(Snap.Clear);
        Cleanup(Tracking.Clear);
        Cleanup(Preview.Clear);
        Cleanup(Grips.Clear);
        Cleanup(Selection.Clear);
        Cleanup(Subobjects.Clear);
        Cleanup(() => Document.DetachEngine(deletePresentation: true));

        Engine = null;
        LastResolvedPoint = null;
        LastPointerPosition = null;

        if (failures.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"CadWorkspace disposed with {failures.Count} recoverable cleanup failure(s): " +
                string.Join(Environment.NewLine, failures));
        }

        void Cleanup(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception) when (IsRecoverableDisposeFailure(exception))
            {
                failures.Add(exception);
            }
        }
    }

    internal void RefreshSelectionGrips()
    {
        if (Tools.OwnsInteraction ||
            Subobjects.Selected.Count > 0)
        {
            Grips.Clear();
            return;
        }

        Grips.Show(Selection.Selected);
    }

    private void FormalSelectionChanged(
        object? sender,
        CadSelectionChangedEventArgs args)
    {
        if (!Tools.OwnsInteraction)
            RefreshSelectionGrips();
    }

    private void SubobjectSelectionChanged(
        object? sender,
        CadSubobjectSelectionChangedEventArgs args)
    {
        if (!Tools.OwnsInteraction)
            RefreshSelectionGrips();
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

    internal void RestoreModifiedState(bool value) => SetModified(value);

    private void SetModified(bool value)
    {
        if (_isModified == value) return;
        _isModified = value;
        PublishModifiedChanged();
    }

    private void PublishModifiedChanged()
    {
        var handlers = ModifiedChanged;
        if (handlers is null)
            return;

        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception exception) when (IsRecoverableObserverFailure(exception))
            {
                // Modified state is already authoritative. UI title/status-bar
                // observers cannot invalidate a save/undo/document transition.
                System.Diagnostics.Debug.WriteLine(
                    $"ModifiedChanged observer failed after state changed: {exception}");
            }
        }
    }

    private static bool IsRecoverableObserverFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static bool IsRecoverableDisposeFailure(Exception exception)
    {
        if (exception is AggregateException aggregate)
            return aggregate.Flatten().InnerExceptions.All(IsRecoverableDisposeFailure);

        if (exception is OutOfMemoryException or StackOverflowException or AccessViolationException)
            return false;

        return exception.InnerException is null ||
               IsRecoverableDisposeFailure(exception.InnerException);
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

    private void ApplyGeometryChange(IEnumerable<CadEntity> entities, string name, Action<CadEntity> action) =>
        CadTransaction.ApplyEntities(this, entities.Distinct().ToArray(), name, action, geometryOnly: true);

    private void ApplyLayerChange(
        CadLayer layer,
        string name,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(action);
        Layers.GetRequired(layer.Name);

        var before = layer.CaptureState();
        try
        {
            action();
            var after = layer.CaptureState();
            if (before == after)
                return;

            History.RecordApplied(
                new CadLayerStateHistoryEntry(
                    layer,
                    before,
                    after,
                    name));
            Selection.RefreshValidity();
            Subobjects.RefreshValidity();
        }
        catch (Exception failure)
        {
            try
            {
                layer.RestoreState(before);
            }
            catch (Exception restoreFailure)
            {
                throw new AggregateException(
                    "Layer change and rollback both failed.",
                    failure,
                    restoreFailure);
            }

            throw;
        }
    }

    private void ApplyStateChange(IEnumerable<CadEntity> entities, string name,
        Action<CadEntity> action, bool requireSelectable = true)
    {
        var targets = entities.Distinct().Where(Document.Entities.Contains)
            .Where(entity => !requireSelectable || Document.IsEntitySelectable(entity)).ToArray();
        CadTransaction.ApplyEntities(this, targets, name, action);
    }
}
