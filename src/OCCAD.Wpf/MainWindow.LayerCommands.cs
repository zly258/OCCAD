using System.Windows;

namespace OCCAD.Wpf;

public partial class MainWindow
{
    private void CreateLayerClick(object sender, RoutedEventArgs e)
    {
        var suggestedName = NextLayerName();
        var dialog = new LayerNameDialog(this, suggestedName, creating: true);
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var layer = _workspace.AddLayer(dialog.LayerName);
            ToolStatus.Text = UiFormat(
                "Cad.Text.LayerCreated",
                "Layer created: {0}",
                layer.Name);
        }
        catch (InvalidOperationException exception)
        {
            ToolStatus.Text = exception.Message;
        }
        catch (ArgumentException exception)
        {
            ToolStatus.Text = exception.Message;
        }
    }

    private string NextLayerName()
    {
        for (var index = 1; ; index++)
        {
            var name = $"Layer{index}";
            if (_workspace.Layers.TryGet(name) is null)
                return name;
        }
    }
}
