namespace OCCAD;

/// <summary>
/// Contract for entities whose geometry depends on one or more source entities.
/// This is the Core equivalent of source-side entity reactors: dependency
/// identity is explicit, document-owned, and independent from any UI/viewer.
/// </summary>
public interface ICadSourceDependentEntity
{
    IReadOnlyCollection<Guid> SourceEntityIds { get; }

    /// <summary>
    /// Re-evaluates dependent geometry from the current document sources.
    /// Returns true when the dependent geometry changed.
    /// </summary>
    bool RefreshFromSources(CadDocument document);
}
