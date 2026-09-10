using System.ComponentModel;
using OcctNet;

namespace OCCAD;

public enum CadFeatureInputMode
{
    CapturedGeometry,
    SourceReference
}

public sealed record CadFeatureInputDescriptor(
    string Id,
    string SourceType,
    CadFeatureInputMode Mode,
    Guid? SourceEntityId = null);

/// <summary>
/// Common contract for modeled features. Inputs explicitly distinguish retained
/// source references from captured geometry so dependency rebuild and standalone
/// persistence can coexist without changing the feature result contract.
/// </summary>
public abstract class CadFeatureEntity : CadEntity, ICadSourceDependentEntity
{
    protected CadFeatureEntity(string entityType)
        : base(entityType)
    {
    }

    [Browsable(false)]
    public abstract IReadOnlyList<CadFeatureInputDescriptor>
        Inputs { get; }

    [Browsable(false)]
    public IReadOnlyCollection<Guid> SourceEntityIds =>
        Inputs
            .Where(static input =>
                input.Mode == CadFeatureInputMode.SourceReference &&
                input.SourceEntityId is not null)
            .Select(static input => input.SourceEntityId!.Value)
            .Distinct()
            .ToArray();

    [Browsable(false)]
    public virtual IReadOnlyList<CadValueDescriptor>
        Parameters =>
        TypeDescriptor
            .GetProperties(this, true)
            .Cast<PropertyDescriptor>()
            .Where(static property =>
                property.IsBrowsable &&
                !property.IsReadOnly &&
                string.Equals(
                    property.Category,
                    "Geometry",
                    StringComparison.OrdinalIgnoreCase))
            .Select(static property =>
                CadValueDescriptor.Create(
                    property.Name,
                    property.PropertyType))
            .ToArray();

    [Browsable(false)]
    public bool SupportsParametricEditing =>
        Parameters.Count > 0;

    [Browsable(false)]
    public bool HasSourceReferences =>
        SourceEntityIds.Count > 0;

    internal sealed override OcctShape BuildShape(
        OcctEngine engine) =>
        BuildFeatureResult(engine);

    protected abstract OcctShape BuildFeatureResult(
        OcctEngine engine);

    public bool RefreshFromSources(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return RefreshSourceReferences(document);
    }

    internal virtual bool RefreshSourceReferences(
        CadDocument document) =>
        false;

    protected static CadFeatureInputDescriptor CapturedInput(
        string id,
        CadEntity source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(source);

        return new CadFeatureInputDescriptor(
            id.Trim(),
            source.EntityType,
            CadFeatureInputMode.CapturedGeometry);
    }

    protected static CadFeatureInputDescriptor SourceInput(
        string id,
        CadEntity source,
        Guid sourceEntityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(source);
        if (sourceEntityId == Guid.Empty)
            throw new ArgumentOutOfRangeException(
                nameof(sourceEntityId));

        return new CadFeatureInputDescriptor(
            id.Trim(),
            source.EntityType,
            CadFeatureInputMode.SourceReference,
            sourceEntityId);
    }

    protected static Guid? ReadSourceId(
        System.Text.Json.Nodes.JsonObject data,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var node = data[propertyName];
        if (node is null)
            return null;

        var value = node.GetValue<Guid>();
        return value == Guid.Empty
            ? null
            : value;
    }
}
