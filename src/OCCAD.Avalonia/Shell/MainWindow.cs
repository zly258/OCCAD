using Avalonia.Controls;

namespace OCCAD.Avalonia;

/// <summary>
/// Deliberately empty application shell.
/// CAD UI is rebuilt only after the core architecture is aligned with
/// OCCTBIM-Source/release-1.0.
/// </summary>
internal sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "OCCAD";
        Width = 1280;
        Height = 800;
        MinWidth = 800;
        MinHeight = 600;
    }
}
