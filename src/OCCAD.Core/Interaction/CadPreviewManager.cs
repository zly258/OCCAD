using OcctNet;

namespace OCCAD;

public sealed class CadPreviewManager
{
    private readonly List<CadEntity> _entities = [];
    private readonly List<IOcctObject> _shapes = [];
    private OcctEngine? _engine;

    public IReadOnlyList<CadEntity> Entities => _entities;
    public CadEntity? Entity => _entities.Count == 1 ? _entities[0] : null;
    public bool IsVisible => _shapes.Count > 0;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Clear();
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
        if (_engine is { IsInitialized: true } engine && _shapes.Count > 0)
        {
            using var batch = engine.BeginDisplayBatch();
            DeleteObjects(engine, _shapes);
        }

        _shapes.Clear();
        _entities.Clear();
    }

    private void Rebuild(IReadOnlyList<CadEntity> entities)
    {
        var engine = _engine ??
            throw new InvalidOperationException("No OCCT engine is attached.");

        var nextShapes = new List<IOcctObject>(entities.Count);
        using var batch = engine.BeginDisplayBatch();

        try
        {
            foreach (var entity in entities)
            {
                var shape = entity.BuildPresentation(engine);
                nextShapes.Add(shape);
                engine.SetLocalTransformation(
                    shape,
                    entity.Placement.Transform);
                engine.SetObjectSelectable(shape, false);
                engine.SetObjectColor(shape, entity.Color);
                engine.SetObjectTransparency(
                    shape,
                    Math.Clamp(entity.Transparency, 0.0, 1.0));
                if (shape is OcctShape)
                {
                    engine.SetObjectLineWidth(
                        shape,
                        Math.Max(0.1, entity.LineWidth));
                    engine.SetObjectDisplayMode(shape, entity.DisplayMode);
                    engine.SetObjectMaterial(shape, entity.Material);
                }
            }
        }
        catch
        {
            TryDeleteObjects(engine, nextShapes);
            throw;
        }

        try
        {
            DeleteObjects(engine, _shapes);
        }
        catch
        {
            // Replacement is atomic from the manager's point of view: if the
            // previous preview cannot be removed, discard the new frame and keep
            // the previous shape list authoritative.
            TryDeleteObjects(engine, nextShapes);
            throw;
        }

        _shapes.Clear();
        _shapes.AddRange(nextShapes);
    }

    private static void DeleteObjects(
        OcctEngine engine,
        IEnumerable<IOcctObject> shapes)
    {
        var existing = shapes
            .Where(shape => engine.ContainsObject(shape.Id))
            .ToArray();
        if (existing.Length > 0)
            engine.Delete(existing);
    }

    private static void TryDeleteObjects(
        OcctEngine engine,
        IEnumerable<IOcctObject> shapes)
    {
        try
        {
            DeleteObjects(engine, shapes);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
