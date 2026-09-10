using OcctNet;

namespace OCCAD;

public sealed class CadPreviewManager
{
    private readonly List<CadEntity> _entities = [];
    private readonly CadDocument? _document;
    private readonly List<IOcctObject> _shapes = [];
    private OcctEngine? _engine;

    public CadPreviewManager()
    {
    }

    internal CadPreviewManager(CadDocument document)
    {
        _document =
            document ?? throw new ArgumentNullException(nameof(document));
    }

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

        using var batch = engine.BeginDisplayBatch();

        DeleteObjects(engine, _shapes);
        _shapes.Clear();
        _entities.Clear();

        var nextShapes = new List<IOcctObject>(entities.Count);
        try
        {
            foreach (var entity in entities)
            {
                var shape = entity.BuildPresentation(engine);
                nextShapes.Add(shape);
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
            throw;
        }

        _shapes.AddRange(nextShapes);
    }

    private CadResolvedAppearance ResolveAppearance(
        CadEntity entity) =>
        _document is null
            ? new CadResolvedAppearance(
                entity.Color,
                entity.LineWidth,
                entity.LineStyle,
                entity.Visible,
                entity.Selectable)
            : _document.ResolveAppearance(entity);

    private static void DeleteObjects(
        OcctEngine engine,
        IEnumerable<IOcctObject> shapes)
    {
        var values = shapes.ToArray();
        if (values.Length > 0)
            engine.Delete(values);
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
