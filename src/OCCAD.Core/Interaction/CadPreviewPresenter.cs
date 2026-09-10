using OcctNet;

namespace OCCAD;

/// <summary>
/// Owns every native object and application tag created for tool preview.
/// Semantic preview state remains in <see cref="CadPreviewManager"/>; this
/// presenter is the single authority for viewer ownership and cleanup.
/// </summary>
internal sealed class CadPreviewPresenter
{
    private static long _nextOwnerId;

    private readonly CadDocument? _document;
    private readonly List<IOcctObject> _shapes = [];
    private readonly Dictionary<long, string> _ownedTags = [];
    private readonly List<CadEntity> _replacementSources = [];
    private readonly string _tagPrefix =
        $"OCCAD.Preview.{System.Threading.Interlocked.Increment(ref _nextOwnerId)}";

    private long _nextTagId;
    private OcctEngine? _engine;

    public CadPreviewPresenter(CadDocument? document)
    {
        _document = document;
    }

    public bool IsVisible =>
        _shapes.Count > 0 ||
        _ownedTags.Count > 0;

    public bool HasReplacementSources =>
        _replacementSources.Count > 0;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        // The caller must clean the previous engine before switching. Engine
        // recreation destroys its viewer scene, so object ids and tags are
        // engine-local and must never be carried into the replacement engine.
        _shapes.Clear();
        _ownedTags.Clear();
        _replacementSources.Clear();
        _engine = engine;
    }

    public void SetReplacementSources(IReadOnlyList<CadEntity> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (_replacementSources.SequenceEqual(sources))
            return;

        RestoreReplacementSources();
        if (_engine is not { IsInitialized: true } engine)
            return;

        using var batch = engine.BeginDisplayBatch();
        foreach (var entity in sources.Distinct())
        {
            if (entity.ViewerObject is not { } source ||
                !engine.ContainsObject(source.Id))
                continue;

            // Register ownership before mutation because a native call may have
            // partially applied before throwing.
            _replacementSources.Add(entity);
            engine.SetObjectTransparency(source, 1.0);
        }
    }

    public void RestoreReplacementSources()
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
                if (entity.ViewerObject is { } source &&
                    engine.ContainsObject(source.Id))
                {
                    engine.SetObjectTransparency(
                        source,
                        Math.Clamp(entity.Transparency, 0.0, 1.0));
                    engine.SetObjectVisible(
                        source,
                        ResolveAppearance(entity).Visible);
                }

                _replacementSources.Remove(entity);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Replacement source restoration failed; ownership retained for retry.",
                failures);
        }
    }

    public void Rebuild(IReadOnlyList<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var engine = _engine ??
            throw new InvalidOperationException("No OCCT engine is attached.");

        using var batch = engine.BeginDisplayBatch();
        DeleteOwnedObjects(engine);
        if (_shapes.Count > 0 || _ownedTags.Count > 0)
        {
            throw new InvalidOperationException(
                "The previous preview presentation could not be removed.");
        }

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
                    engine.SetObjectLineStyle(
                        shape,
                        appearance.LineStyle);
                    engine.SetObjectDisplayMode(
                        shape,
                        entity.DisplayMode);
                    engine.SetObjectMaterial(
                        shape,
                        entity.Material);
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

    public void ClearPresentation()
    {
        if (_engine is { IsInitialized: true } engine &&
            (_shapes.Count > 0 || _ownedTags.Count > 0))
        {
            using (engine.BeginDisplayBatch())
                DeleteOwnedObjects(engine);
        }
        else if (_engine is null || !_engine.IsInitialized)
        {
            // With no live viewer there is no presentation that can survive.
            _shapes.Clear();
            _ownedTags.Clear();
        }
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
        // Delete each tracked object independently. If deletion fails, retain
        // its handle/tag so the next cleanup can retry instead of orphaning a
        // preview presentation in the viewer.
        foreach (var shape in _shapes.ToArray())
        {
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

        // Tag-based cleanup can succeed after handle deletion failed. Remove
        // stale handles before deciding that cleanup is incomplete.
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
                // Retaining ownership is safer when the viewer cannot answer.
            }

            if (keep &&
                !_shapes.Any(existing => existing.Id == shape.Id))
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
