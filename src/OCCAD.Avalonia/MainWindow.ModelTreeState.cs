namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    // Keep the existing field referenced until the remaining model-tree wiring
    // is moved completely into CadModelPanelController.
    private bool ModelTreeRefreshState
    {
        get => _refreshingTree;
        set => _refreshingTree = value;
    }
}
