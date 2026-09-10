using OcctNet;

namespace OCCAD;

public sealed class CadPreviewManager
{
    private readonly List<CadEntity> _entities = [];
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
        Rebuild(nextEntities);
        _entities.Clear();
        _entities.AddRange(nextEntities);
    }

    public void Update(CadEntity entity) => Show(entity);

    public void Update(IEnumerable<CadEntity> entities) => Show(entities);

    public void Clear()
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
            TryDeleteObjects(engine, nextShapes);
            PurgeMissingOwnership(engine);
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
            bool exists;
            try
            {
                exists = shape is not null && engine.ContainsObject(shape.Id);
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
