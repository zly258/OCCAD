using System.Drawing;
using OcctNet;

namespace OCCAD;

[Flags]
public enum CadSnapType
{
    None = 0,
    Endpoint = 1 << 0,
    Midpoint = 1 << 1,
    Center = 1 << 2,
    Vertex = 1 << 3,
    Quadrant = 1 << 4,
    Nearest = 1 << 5,
    Intersection = 1 << 6,
    Perpendicular = 1 << 7,
    Tangent = 1 << 8,

    // Keep the default/UI contract conservative. Precision modes remain available
    // internally for geometry combinations with exact implementations, but they
    // are not advertised as globally supported until the remaining conic/spline
    // cases are implemented through the Bridge.
    Default = Endpoint | Midpoint | Center | Vertex | Quadrant
}

public enum CadSnapPlaneMode
{
    KeepEntityPoint,
    ProjectToWorkPlane,
    RequireOnWorkPlane
}

public readonly record struct CadSnapWorkPlane(
    OcctPoint3d Origin,
    OcctVector3d XAxis,
    OcctVector3d YAxis,
    bool LockPlane = false,
    double? LockedAngleDegrees = null);

public readonly record struct CadSnapPoint(
    CadEntity? Entity,
    OcctPoint3d Position,
    CadSnapType Type,
    int Index = -1,
    CadSnapWorkPlane? WorkPlane = null);
