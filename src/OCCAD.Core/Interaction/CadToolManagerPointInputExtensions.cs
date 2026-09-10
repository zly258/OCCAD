using OcctNet;

namespace OCCAD;

internal static class CadToolManagerPointInputExtensions
{
    public static bool CommitPoint(this CadToolManager tools, OcctPoint3d point)
    {
        ArgumentNullException.ThrowIfNull(tools);
        return point.IsFinite &&
               tools.ActiveTool is ICadPointInputTool pointTool &&
               pointTool.TryAcceptPoint(point);
    }
}
