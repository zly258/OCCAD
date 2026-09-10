using OcctNet;

namespace OCCAD;

public enum CadPathSegmentType
{
    Line,
    Arc
}

public readonly record struct CadPathSegmentInfo(
    int Index,
    CadPathSegmentType Type,
    OcctPoint3d Start,
    OcctPoint3d End,
    double Length,
    OcctPoint3d? Middle = null,
    double? Radius = null);
