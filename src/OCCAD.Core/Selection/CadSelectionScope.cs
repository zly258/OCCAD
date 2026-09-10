using OcctNet;

namespace OCCAD;

public enum CadSelectionScope { Entity, Subobject }

[Flags]
public enum CadSubshapeMask
{
    None = 0,
    Vertex = 1,
    Edge = 2,
    Wire = 4,
    Face = 8,
    Shell = 16,
    Solid = 32,
    All = Vertex | Edge | Wire | Face | Shell | Solid
}

public static class CadSubshapeMasks
{
    public static bool Allows(this CadSubshapeMask mask, OcctShapeType type) =>
        (mask & (type switch
        {
            OcctShapeType.Vertex => CadSubshapeMask.Vertex,
            OcctShapeType.Edge => CadSubshapeMask.Edge,
            OcctShapeType.Wire => CadSubshapeMask.Wire,
            OcctShapeType.Face => CadSubshapeMask.Face,
            OcctShapeType.Shell => CadSubshapeMask.Shell,
            OcctShapeType.Solid => CadSubshapeMask.Solid,
            _ => CadSubshapeMask.None
        })) != 0;
}
