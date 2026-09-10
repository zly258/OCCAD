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

    Default = Endpoint | Midpoint | Center | Vertex | Quadrant
}

public enum CadSnapPlaneMode
{
    KeepEntityPoint,
    RequireOnWorkPlane
}

public readonly record struct CadSnapResolvePolicy(
    CadSnapPlaneMode PlaneMode,
    double PlaneTolerance)
{
    public static CadSnapResolvePolicy KeepExactPoint =>
        new(CadSnapPlaneMode.KeepEntityPoint, 1e-6);

    public static CadSnapResolvePolicy RequireOnWorkPlane(
        double tolerance = 1e-6) =>
        new(CadSnapPlaneMode.RequireOnWorkPlane, tolerance);

    public void Validate()
    {
        if (!Enum.IsDefined(PlaneMode))
            throw new ArgumentOutOfRangeException(
                nameof(PlaneMode));
        if (!double.IsFinite(PlaneTolerance) ||
            PlaneTolerance < 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(PlaneTolerance));
    }
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
