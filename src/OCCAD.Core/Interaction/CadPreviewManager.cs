using OcctNet;

namespace OCCAD;

public sealed class CadPreviewManager
{
    private readonly List<CadEntity> _entities = [];
    private readonly List<CadEntity> _replacementSources = [];
    private readonly CadDocument? _document;
    private static long _nextOwnerId;

    private readonly List<IOcctObject> _shapes = [];
    private readonly Dictionary<long, string> _ownedTags = [];
    private readonly string _tagPrefix =
        $"OCCAD.Preview.{System.Threading.Interlocked.Increment(ref _nextOwnerId)}";
    private long _nextTagId;
    private OcctEngine? _engine;

    public CadPreviewManager()
    {
    }

    internal CadPreviewManager(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public IReadOnlyList<CadEntity> Entities => _entities;
    public CadEntity? Entity => _entities.Count == 1 ? _entities[0] : null;
    public bool HasTransient => IsVisible || _entities.Count > 0 || _replacementSources.Count > 0;
    public bool IsVisible => _shapes.Count > 0 || _ownedTags.Count > 0;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Clear();

        // Engine recreation destroys the old viewer scene. Any ownership that
        // could not be explicitly removed from the previous engine must not be
        // carried into the new engine because object ids/tags are engine-local.
        _shapes.Clear();
        _ownedTags.Clear();
        _engine = engine;
    }

    public void Show(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Show([entity]);
    }

    public void Show(IEnumerable<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var nextEntities = entities.ToArray();
        ValidatePreviewEntities(nextEntities);
        Rebuild(nextEntities);
        _entities.Clear();
        _entities.AddRange(nextEntities);
    }

    public void Update(CadEntity entity) => Show(entity);

    public void Update(IEnumerable<CadEntity> entities) => Show(entities);

    private void ValidatePreviewEntities(IEnumerable<CadEntity> entities)
    {
        foreach (var entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (_document?.Entities.Contains(entity) == true)
                throw new ArgumentException("Preview requires independent entities, not document entities.", nameof(entities));
        }
    }

    public void ShowReplacement(IReadOnlyList<CadEntity> sources, IReadOnlyList<CadEntity> replacements)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(replacements);
        ValidatePreviewEntities(replacements);
        try
        {
            if (!_replacementSources.SequenceEqual(sources))
            {
                RestoreReplacementSources();
                if (_engine is { IsInitialized: true } engine)
                {
                    using var batch = engine.BeginDisplayBatch();
                    foreach (var entity in sources.Distinct())
                    {
                        if (entity.ViewerObject is not { } source || !engine.ContainsObject(source.Id)) continue;
                        // Register before mutation; failed native calls can have partial effects.
                        _replacementSources.Add(entity);
                        engine.SetObjectTransparency(source, 1.0);
                    }
                }
            }
            Show(replacements);
        }
        catch (Exception failure)
        {
            try
            {
                Clear();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "Replacement preview failed and cleanup also failed.",
                    failure,
                    cleanupFailure);
            }

            throw;
        }
    }

    private void RestoreReplacementSources()
    {
        if (_engine is not { IsInitialized: true } engine)
        {
            _replacementSources.Clear();
            return;
        }
        List<Exception> failures = [];
        foreach (var entity in _replacementSources.ToArray())
        {
            try
            {
                if (entity.ViewerObject is { } source && engine.ContainsObject(source.Id))
                {
                    engine.SetObjectTransparency(source, Math.Clamp(entity.Transparency, 0.0, 1.0));
                    engine.SetObjectVisible(source, _document?.ResolveAppearance(entity).Visible ?? entity.Visible);
                }
                _replacementSources.Remove(entity);
            }
            catch (Exception exception) when (IsRecoverable(exception)) { failures.Add(exception); }
        }
        if (failures.Count > 0)
            throw new AggregateException("Replacement source restoration failed; ownership retained for retry.", failures);
    }

    public void Clear()
    {
        Exception? presentationFailure = null;
        Exception? replacementFailure = null;

        try
        {
            ClearPresentation();
        }
        catch (Exception exception)
        {
            presentationFailure = exception;
        }

        // Logical preview state must become empty even when native cleanup needs
        // a later retry. Presentation ownership itself remains in _shapes/tags.
        _entities.Clear();

        try
        {
            RestoreReplacementSources();
        }
        catch (Exception exception)
        {
            replacementFailure = exception;
        }

        if (presentationFailure is not null && replacementFailure is not null)
        {
            throw new AggregateException(
                "Preview presentation cleanup and replacement-source restoration both failed.",
                presentationFailure,
                replacementFailure);
        }

        if (presentationFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(presentationFailure).Throw();
        if (replacementFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(replacementFailure).Throw();
    }

    private void ClearPresentation()
    {
        if (_engine is { IsInitialized: true } engine &&
            (_shapes.Count > 0 || _ownedTags.Count > 0))
        {
            using (engine.BeginDisplayBatch())
            {
                DeleteOwnedObjects(engine);
            }
        }
        else if (_engine is null || !_engine.IsInitialized)
        {
            // With no live viewer there is no presentation that can survive.
            _shapes.Clear();
            _ownedTags.Clear();
        }

        // Logical preview state is always cleared immediately. Presentation
        // ownership is intentionally retained when viewer deletion failed so a
        // later Clear/Rebuild can retry instead of leaking an orphan shape.
        _entities.Clear();
    }

    private void Rebuild(IReadOnlyList<CadEntity> entities)
    {
        var engine = _engine ??
            throw new InvalidOperationException("No OCCT engine is attached.");

        using var batch = engine.BeginDisplayBatch();

        DeleteOwnedObjects(engine);
        if (_shapes.Count > 0 || _ownedTags.Count > 0)
        {
            throw new InvalidOperationException(
                "The previous preview presentation could not be removed.");
        }

        _entities.Clear();

        var nextShapes = new List<IOcctObject>(entities.Count);
        try
        {
            foreach (var entity in entities)
            {
                var shape = entity.BuildPresentation(engine);
                nextShapes.Add(shape);

                var tag = $"{_tagPrefix}.{++_nextTagId}";
                engine.SetApplicationTag(shape, tag);
                _ownedTags[shape.Id] = tag;

                engine.SetLocalTransformation(
                    shape,
                    entity.Placement.Transform);
                var appearance = ResolveAppearance(entity);
                engine.SetObjectSelectable(shape, false);
                engine.SetObjectColor(shape, appearance.Color);
                engine.SetObjectTransparency(
                    shape,
                    Math.Clamp(entity.Transparency, 0.0, 1.0));
                if (shape is OcctShape)
                {
                    engine.SetObjectLineWidth(
                        shape,
                        Math.Max(0.1, appearance.LineWidth));
                    engine.SetObjectLineStyle(shape, appearance.LineStyle);
                    engine.SetObjectDisplayMode(shape, entity.DisplayMode);
                    engine.SetObjectMaterial(shape, entity.Material);
                }
            }
        }
        catch
        {
            if (!TryDeleteObjects(engine, nextShapes))
                PreserveLiveShapes(engine, nextShapes);
            PurgeMissingOwnership(engine);
            PurgeMissingShapes(engine);
            throw;
        }

        _shapes.AddRange(nextShapes);
    }

    private CadResolvedAppearance ResolveAppearance(CadEntity entity) =>
        _document is null
            ? new CadResolvedAppearance(
                entity.Color,
                entity.LineWidth,
                entity.LineStyle,
                entity.Visible,
                entity.Selectable)
            : _document.ResolveAppearance(entity);

    private void DeleteOwnedObjects(OcctEngine engine)
    {
        // Delete each tracked object independently. If deletion fails, keep the
        // handle/tag so ownership is not lost and the next cleanup can retry.
        foreach (var shape in _shapes.ToArray())
        {
            if (shape is null)
                continue;

            bool exists;
            try
            {
                exists = engine.ContainsObject(shape.Id);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                continue;
            }

            if (!exists || TryDeleteObjects(engine, [shape]))
                _shapes.Remove(shape);
        }

        foreach (var pair in _ownedTags.ToArray())
        {
            IOcctObject? value;
            try
            {
                value = engine.FindObjectByApplicationTag(pair.Value);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                continue;
            }

            if (value is null)
            {
                _ownedTags.Remove(pair.Key);
                continue;
            }

            if (TryDeleteObjects(engine, [value]))
                _ownedTags.Remove(pair.Key);
        }

        // A shape deletion can fail first and then succeed through its
        // application tag. Remove such stale handles before deciding cleanup
        // failed, otherwise Rebuild would reject one unnecessary extra cycle.
        PurgeMissingShapes(engine);
    }

    private void PreserveLiveShapes(
        OcctEngine engine,
        IEnumerable<IOcctObject> shapes)
    {
        foreach (var shape in shapes)
        {
            var keep = true;
            try
            {
                keep = engine.ContainsObject(shape.Id);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                // The viewer could not answer. Retaining the handle is safer
                // than losing ownership of a potentially live preview object.
            }

            if (keep && !_shapes.Any(existing => existing.Id == shape.Id))
                _shapes.Add(shape);
        }
    }

    private void PurgeMissingShapes(OcctEngine engine)
    {
        foreach (var shape in _shapes.ToArray())
        {
            try
            {
                if (!engine.ContainsObject(shape.Id))
                    _shapes.Remove(shape);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                // Preserve ownership and retry on the next cleanup.
            }
        }
    }

    private void PurgeMissingOwnership(OcctEngine engine)
    {
        foreach (var pair in _ownedTags.ToArray())
        {
            try
            {
                if (engine.FindObjectByApplicationTag(pair.Value) is null)
                    _ownedTags.Remove(pair.Key);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                // Preserve ownership when the viewer cannot currently answer.
                // A later Clear/Rebuild will retry the lookup/deletion.
            }
        }
    }

    private static void DeleteObjects(
        OcctEngine engine,
        IEnumerable<IOcctObject> shapes)
    {
        var values = shapes.ToArray();
        if (values.Length > 0)
            engine.Delete(values);
    }

    private static bool TryDeleteObjects(
        OcctEngine engine,
        IEnumerable<IOcctObject> shapes)
    {
        try
        {
            DeleteObjects(engine, shapes);
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return false;
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
