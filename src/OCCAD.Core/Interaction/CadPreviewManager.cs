using OcctNet;

namespace OCCAD;

/// <summary>
/// Semantic tool-preview state. Native viewer objects are owned exclusively by
/// <see cref="CadPreviewPresenter"/>, so preview cleanup has one presentation
/// authority and cannot diverge from logical preview state.
/// </summary>
public sealed class CadPreviewManager
{
    private readonly List<CadEntity> _entities = [];
    private readonly CadDocument? _document;
    private readonly CadPreviewPresenter _presenter;

    public CadPreviewManager()
    {
        _presenter = new CadPreviewPresenter(document: null);
    }

    internal CadPreviewManager(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _presenter = new CadPreviewPresenter(_document);
    }

    public IReadOnlyList<CadEntity> Entities => _entities;
    public CadEntity? Entity => _entities.Count == 1 ? _entities[0] : null;
    public bool HasTransient =>
        IsVisible ||
        _entities.Count > 0 ||
        _presenter.HasReplacementSources;
    public bool IsVisible => _presenter.IsVisible;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Clear();
        _presenter.AttachEngine(engine);
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

        // Logical preview state is invalid as soon as presentation replacement
        // starts. If native construction fails, callers must never observe stale
        // semantic entities that no longer match the viewer scene.
        _entities.Clear();
        _presenter.Rebuild(nextEntities);
        _entities.AddRange(nextEntities);
    }

    public void Update(CadEntity entity) => Show(entity);

    public void Update(IEnumerable<CadEntity> entities) => Show(entities);

    public void ShowReplacement(
        IReadOnlyList<CadEntity> sources,
        IReadOnlyList<CadEntity> replacements)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(replacements);
        ValidatePreviewEntities(replacements);

        try
        {
            _presenter.SetReplacementSources(sources);
            Show(replacements);
        }
        catch
        {
            Clear();
            throw;
        }
    }

    public void Clear()
    {
        try
        {
            _presenter.ClearPresentation();
        }
        finally
        {
            _entities.Clear();
            _presenter.RestoreReplacementSources();
        }
    }

    private void ValidatePreviewEntities(
        IEnumerable<CadEntity> entities)
    {
        foreach (var entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (_document?.Entities.Contains(entity) == true)
            {
                throw new ArgumentException(
                    "Preview requires independent entities, not document entities.",
                    nameof(entities));
            }
        }
    }
}
