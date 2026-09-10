using OcctNet;

namespace OCCAD;

public readonly record struct CadSubshapeFallback(
    string GeometryKind,
    OcctPoint3d Anchor,
    double Measure)
{
    public CadSubshapeFallback Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(GeometryKind);
        if (!Anchor.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(Anchor));
        if (!double.IsFinite(Measure) || Measure < 0.0)
            throw new ArgumentOutOfRangeException(nameof(Measure));
        return this;
    }
}

/// <summary>
/// Stable logical reference to a subshape. Index remains the fast path; the
/// optional fallback descriptor is reserved for matching after topology rebuild.
/// </summary>
public readonly record struct CadSubshapeReference
{
    public CadSubshapeReference(
        Guid entityId,
        OcctShapeType shapeType,
        int index,
        CadSubshapeFallback? fallback = null)
    {
        if (entityId == Guid.Empty)
            throw new ArgumentException(
                "Subshape reference requires an entity id.",
                nameof(entityId));
        if (shapeType == OcctShapeType.Shape)
            throw new ArgumentException(
                "Subshape reference requires a concrete topology type.",
                nameof(shapeType));
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        EntityId = entityId;
        ShapeType = shapeType;
        Index = index;
        Fallback = fallback?.Validate();
    }

    public Guid EntityId { get; }
    public OcctShapeType ShapeType { get; }
    public int Index { get; }
    public CadSubshapeFallback? Fallback { get; }

    public CadSelectionReference ToSelectionReference() =>
        new(EntityId, ShapeType, Index);

    public bool SameIdentity(CadSubshapeReference other) =>
        EntityId == other.EntityId &&
        ShapeType == other.ShapeType &&
        Index == other.Index;

    public static CadSubshapeReference Create(
        CadEntity entity,
        OcctShapeType shapeType,
        int index,
        CadSubshapeFallback? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new CadSubshapeReference(
            entity.Id,
            shapeType,
            index,
            fallback);
    }
}
