using Avalonia;
using Avalonia.Controls;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _propertyGridRefinementApplied;

    internal void ApplyPropertyGridRefinement()
    {
        if (_propertyGridRefinementApplied)
            return;

        _propertyGridRefinementApplied = true;

        // Property rows and editors are now styled at creation time by
        // CadPropertyInspectorController. Keep only shell-level panel styling
        // here; there is no LayoutUpdated traversal or runtime visual rewrite.
        _propertyPanelBorder.BorderBrush = CadTheme.BorderStrong;
        _propertyPanelBorder.BorderThickness = new Thickness(0);
        _propertyHost.Spacing = 0;
        _propertyHost.Margin = new Thickness(0);

        if (_propertyHost.Parent is ScrollViewer scroll)
        {
            scroll.Background = CadTheme.Surface;
            scroll.Padding = new Thickness(0);
        }
    }
}
