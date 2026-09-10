namespace OCCAD;

public enum CadDomainEventKind
{
    EntityAdded, EntityRemoved, EntityGeometryChanged, EntityAppearanceChanged, EntityMetadataChanged,
    DocumentChanged, DocumentReset, DocumentModifiedChanged,
    SelectionChanged, SubobjectSelectionChanged, ActiveToolChanged, ToolStageChanged, LayerChanged
}

public sealed class CadDomainEventArgs(CadDomainEventKind kind, EventArgs payload) : EventArgs
{
    public CadDomainEventKind Kind { get; } = kind;
    public EventArgs Payload { get; } = payload;
}

public sealed class CadWorkspaceEvents : IDisposable
{
    private readonly CadWorkspace _workspace;
    internal CadWorkspaceEvents(CadWorkspace workspace)
    {
        _workspace = workspace;
        workspace.Document.Changed += EntityChanged;
        workspace.Document.ChangeSetCommitted += DocumentChanged;
        workspace.Selection.Changed += SelectionChanged;
        workspace.Subobjects.Changed += SubobjectsChanged;
        workspace.Tools.ToolChanged += ToolChanged;
        workspace.Tools.ToolUpdated += ToolUpdated;
        workspace.Layers.Changed += LayerChanged;
        workspace.ModifiedChanged += ModifiedChanged;
    }
    public event EventHandler<CadDomainEventArgs>? Changed;
    private void Publish(CadDomainEventKind kind, EventArgs args) => Changed?.Invoke(this, new(kind, args));
    private void EntityChanged(object? sender, CadDocumentChangedEventArgs args) => Publish(args.Kind switch
    {
        CadDocumentChangeKind.Added => CadDomainEventKind.EntityAdded,
        CadDocumentChangeKind.Removed => CadDomainEventKind.EntityRemoved,
        CadDocumentChangeKind.Reset => CadDomainEventKind.DocumentReset,
        _ => args.EntityChangeKind switch
        {
            CadEntityChangeKind.Geometry => CadDomainEventKind.EntityGeometryChanged,
            CadEntityChangeKind.Appearance => CadDomainEventKind.EntityAppearanceChanged,
            _ => CadDomainEventKind.EntityMetadataChanged
        }
    }, args);
    private void DocumentChanged(object? sender, CadDocumentChangeSetEventArgs args) => Publish(CadDomainEventKind.DocumentChanged, args);
    private void SelectionChanged(object? sender, CadSelectionChangedEventArgs args) => Publish(CadDomainEventKind.SelectionChanged, args);
    private void SubobjectsChanged(object? sender, CadSubobjectSelectionChangedEventArgs args) => Publish(CadDomainEventKind.SubobjectSelectionChanged, args);
    private void ToolChanged(object? sender, CadToolChangedEventArgs args) => Publish(CadDomainEventKind.ActiveToolChanged, args);
    private void ToolUpdated(object? sender, CadToolChangedEventArgs args) => Publish(CadDomainEventKind.ToolStageChanged, args);
    private void LayerChanged(object? sender, CadLayerManagerChangedEventArgs args) => Publish(CadDomainEventKind.LayerChanged, args);
    private void ModifiedChanged(object? sender, EventArgs args) => Publish(CadDomainEventKind.DocumentModifiedChanged, args);
    public void Dispose()
    {
        _workspace.Document.Changed -= EntityChanged;
        _workspace.Document.ChangeSetCommitted -= DocumentChanged;
        _workspace.Selection.Changed -= SelectionChanged;
        _workspace.Subobjects.Changed -= SubobjectsChanged;
        _workspace.Tools.ToolChanged -= ToolChanged;
        _workspace.Tools.ToolUpdated -= ToolUpdated;
        _workspace.Layers.Changed -= LayerChanged;
        _workspace.ModifiedChanged -= ModifiedChanged;
    }
}
