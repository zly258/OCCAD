namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private void WorkspaceDomainChanged(object? sender, OCCAD.CadDomainEventArgs args) => Ui(() =>
    {
        switch (args.Kind)
        {
            case OCCAD.CadDomainEventKind.SelectionChanged:
                ApplySelection((OCCAD.CadSelectionChangedEventArgs)args.Payload); break;
            case OCCAD.CadDomainEventKind.SubobjectSelectionChanged:
                ApplySubobjectSelection((OCCAD.CadSubobjectSelectionChangedEventArgs)args.Payload); break;
            case OCCAD.CadDomainEventKind.DocumentChanged:
                ApplyDocumentChangeSet((OCCAD.CadDocumentChangeSetEventArgs)args.Payload); break;
            case OCCAD.CadDomainEventKind.ActiveToolChanged:
            case OCCAD.CadDomainEventKind.ToolStageChanged:
                UpdateToolUi(_workspace.Tools.ActiveTool); break;
            case OCCAD.CadDomainEventKind.LayerChanged:
                LayerManagerChanged((OCCAD.CadLayerManagerChangedEventArgs)args.Payload); break;
            case OCCAD.CadDomainEventKind.DocumentModifiedChanged:
                UpdateWindowTitle(); break;
        }
    });
}
