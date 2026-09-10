using System.Windows;
using OCCAD;

namespace OCCAD.Wpf;

public partial class MainWindow
{
    private bool _operationStatusInitialized;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        InstallOperationStatusUi();
    }

    private void InstallOperationStatusUi()
    {
        if (_operationStatusInitialized) return;
        _operationStatusInitialized = true;

        RefineCadUiLayout();

        // The command toolbar is reserved for controls and precision input.
        // Operation-stage guidance is owned by the status area.
        ToolStatus.MinWidth = 240;
        ToolStatus.MaxWidth = 560;
        ToolStatus.TextTrimming = TextTrimming.CharacterEllipsis;

        CadLanguageManager.Changed += (_, _) =>
            Dispatcher.InvokeAsync(() => RefreshOperationStatus(_workspace.Tools.ActiveTool));

        RefreshOperationStatus(_workspace.Tools.ActiveTool);
    }

    private void RefreshOperationStatus(CadTool? tool)
    {
        var text = tool switch
        {
            null => UiText("Cad.Text.Ready", "Ready"),
            { Prompt: { Message.Length: > 0 } prompt } => LocalizeToolPrompt(prompt),
            _ => UiFormat(
                "Cad.Text.ToolActive",
                "{0}: active",
                LocalizeToolName(tool))
        };

        ToolStatus.Text = text;
        ToolStatus.ToolTip = text;
    }
}
