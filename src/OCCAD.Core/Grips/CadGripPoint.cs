using OcctNet;

namespace OCCAD;

public enum CadGripKind
{
    Control,
    Vertex,
    Midpoint,
    Center,
    Radius,
    Axis,
    Height
}

public readonly record struct CadGripWorkPlane(
    OcctPoint3d Origin,
    OcctVector3d XAxis,
    OcctVector3d YAxis,
    bool LockPlane = true,
    double? LockedAngleDegrees = null);

public readonly record struct CadGripPoint(
    CadEntity Entity,
    int Index,
    OcctPoint3d Position,
    CadGripWorkPlane? WorkPlane = null,
    OcctPoint3d? ConstraintOrigin = null,
    CadPrecisionInputKind PrecisionInputs =
        CadPrecisionInputKind.LengthAndAngle,
    CadGripKind Kind = CadGripKind.Control);
