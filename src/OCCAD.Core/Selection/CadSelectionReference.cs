using OcctNet;

namespace OCCAD;

/// <summary>
/// Shared identity for whole-entity and subobject selection.
/// </summary>
public readonly record struct CadSelectionReference
{
    public CadSelectionReference(
        Guid entityId,
        OcctShapeType? subshapeType = null,
        int subshapeIndex = -1)
    {
        if (entityId == Guid.Empty)
            throw new ArgumentException(
                "Selection reference requires an entity id.",
                nameof(entityId));

        if (subshapeType is null)
        {
            if (subshapeIndex >= 0)
                throw new ArgumentException(
                    "Whole-entity selection cannot define a subshape index.",
                    nameof(subshapeIndex));
        }
        else
        {
            if (subshapeType == OcctShapeType.Shape)
                throw new ArgumentException(
                    "Subobject selection requires a concrete subshape type.",
                    nameof(subshapeType));
            if (subshapeIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(subshapeIndex));
        }

        EntityId = entityId;
        SubshapeType = subshapeType;
        SubshapeIndex = subshapeType is null ? -1 : subshapeIndex;
    }

    public Guid EntityId { get; }
    public OcctShapeType? SubshapeType { get; }
    public int SubshapeIndex { get; }
    public bool IsSubobject => SubshapeType is not null;

    public static CadSelectionReference Entity(CadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new CadSelectionReference(entity.Id);
    }

    public static CadSelectionReference Subobject(
        CadEntity entity,
        OcctShapeType subshapeType,
        int subshapeIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new CadSelectionReference(
            entity.Id,
            subshapeType,
            subshapeIndex);
    }

    public static CadSelectionReference Subobject(
        CadSubshapeReference reference) =>
        reference.ToSelectionReference();
}
