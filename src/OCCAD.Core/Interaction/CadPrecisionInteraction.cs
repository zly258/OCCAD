namespace OCCAD;

/// <summary>
/// Bridges typed precision input to the active interactive tool without
/// duplicating drawing logic in the UI layer.
/// </summary>
public static class CadPrecisionInteraction
{
    public static bool RefreshPreview(CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var tool = workspace.Tools.ActiveTool;
        if (tool is null || workspace.LastPointerPosition is not { } pointer)
            return false;

        tool.RefreshPreviewFromLastPointer(pointer);
        return true;
    }
}
