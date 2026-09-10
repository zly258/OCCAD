using OcctNet;

namespace OCCAD;

public interface ICadPointInputTool
{
    bool TryAcceptPoint(OcctPoint3d point);
}
