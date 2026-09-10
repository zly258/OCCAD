using System.Drawing;
using System.Windows.Forms;
using OCCAD;

namespace OCCAD.Wpf;

internal sealed class CadPropertyInspectorController : IDisposable
{
    private readonly CadWorkspace _workspace;
    private readonly PropertyGrid _propertyGrid;
    private CadEntity[] _entities = [];
    private IReadOnlyList<CadEntity>? _entityEditBefore;
    private IDisposable? _documentChangeSet;
    private CadLayer? _layer;
    private CadLayerState? _layerEditBefore;
    private bool _disposed;

    public CadPropertyInspectorController(CadWorkspace workspace, PropertyGrid propertyGrid)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _propertyGrid = propertyGrid ?? throw new ArgumentNullException(nameof(propertyGrid));

        ConfigureAppearance();
        _propertyGrid.PropertyValueChanged += PropertyValueChanged;
    }

    public bool IsInspectingLayer => _layer is not null;

    public void InspectEntities(IReadOnlyList<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        EnsureNotDisposed();
        DetachCurrent();

        _layer = null;
        _layerEditBefore = null;
        _entityEditBefore = null;
        _entities = entities.Distinct().ToArray();

        foreach (var entity in _entities)
            entity.Changing += EntityChanging;

        if (_entities.Length == 0)
            _propertyGrid.SelectedObject = null;
        else if (_entities.Length == 1)
            _propertyGrid.SelectedObject =
                new CadLocalizedPropertyObject(
                    _entities[0],
                    _workspace,
                    _entities);
        else
            _propertyGrid.SelectedObjects = _entities
                .Select(entity => (object)new CadLocalizedPropertyObject(
                    entity,
                    _workspace,
                    _entities))
                .ToArray();
    }

    public void InspectLayer(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        EnsureNotDisposed();
        DetachCurrent();

        _entities = [];
        _entityEditBefore = null;
        _layerEditBefore = null;
        _layer = layer;
        _layer.Changing += LayerChanging;
        _propertyGrid.SelectedObject =
            new CadLocalizedPropertyObject(layer, _workspace);
    }

    public void Refresh() => _propertyGrid.Refresh();

    public void ApplyDocumentChangeSet(CadDocumentChangeSetEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureNotDisposed();

        if (_layer is not null)
        {
            if (args.Contains(CadDocumentChangeKind.LayerChanged) || args.Contains(CadDocumentChangeKind.Reset))
                _propertyGrid.Refresh();
            return;
        }

        if (args.Contains(CadDocumentChangeKind.Reset))
        {
            InspectEntities(_workspace.Selection.Selected.ToArray());
            return;
        }

        if (args.Entities.Any(entity => _entities.Contains(entity)))
            _propertyGrid.Refresh();
    }

    public void ApplyLayerManagerChange(CadLayerManagerChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureNotDisposed();

        if (_layer is null)
            return;

        if (args.Kind == CadLayerManagerChangeKind.CurrentChanged)
            InspectLayer(_workspace.Layers.Current);
        else
            _propertyGrid.Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DetachCurrent();
        _propertyGrid.PropertyValueChanged -= PropertyValueChanged;
        _propertyGrid.SelectedObject = null;
    }

    private void ConfigureAppearance()
    {
        var panel = Color.FromArgb(247, 248, 249);
        var text = Color.FromArgb(32, 39, 45);
        var border = Color.FromArgb(174, 181, 187);

        _propertyGrid.BackColor = panel;
        _propertyGrid.ForeColor = text;
        _propertyGrid.ViewBackColor = panel;
        _propertyGrid.ViewForeColor = text;
        _propertyGrid.LineColor = border;
        _propertyGrid.CategoryForeColor = text;
        _propertyGrid.HelpBackColor = panel;
        _propertyGrid.HelpForeColor = text;
        _propertyGrid.CommandsBackColor = panel;
        _propertyGrid.CommandsForeColor = text;
        _propertyGrid.ToolbarVisible = false;
        _propertyGrid.HelpVisible = true;
    }

    private void EntityChanging(object? sender, CadEntityChangingEventArgs args)
    {
        if (_entityEditBefore is not null || _entities.Length == 0 || !_propertyGrid.ContainsFocus)
            return;

        _documentChangeSet ??= _workspace.Document.BeginChangeSet();
        _entityEditBefore = _workspace.CaptureEntityStates(_entities);
    }

    private void LayerChanging(object? sender, CadLayerChangedEventArgs args)
    {
        if (_layerEditBefore is not null || _layer is null || !_propertyGrid.ContainsFocus)
            return;

        _layerEditBefore = _workspace.CaptureLayerState(_layer);
    }

    private void PropertyValueChanged(object? sender, PropertyValueChangedEventArgs args)
    {
        try
        {
            var propertyName = args.ChangedItem?.PropertyDescriptor?.Name;
            var historyName = string.IsNullOrWhiteSpace(propertyName)
                ? "Property Edit"
                : $"Property {propertyName}";

            if (string.Equals(
                    propertyName,
                    nameof(CadEntity.Layer),
                    StringComparison.Ordinal))
            {
                _entityEditBefore = null;
            }
            else if (_entityEditBefore is not null && _entities.Length > 0)
            {
                var before = _entityEditBefore;
                _entityEditBefore = null;
                _workspace.RecordEntityStateChange(_entities, before, historyName);
            }
            else if (_layerEditBefore is { } layerBefore && _layer is not null)
            {
                _layerEditBefore = null;
                _workspace.RecordLayerStateChange(_layer, layerBefore, historyName);
            }

            _propertyGrid.Refresh();
        }
        finally
        {
            EndDocumentChangeSet();
        }
    }

    private void DetachCurrent()
    {
        foreach (var entity in _entities)
            entity.Changing -= EntityChanging;

        if (_layer is not null)
            _layer.Changing -= LayerChanging;

        _entityEditBefore = null;
        _layerEditBefore = null;
        EndDocumentChangeSet();
    }

    private void EndDocumentChangeSet()
    {
        var changeSet = _documentChangeSet;
        _documentChangeSet = null;
        changeSet?.Dispose();
    }

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
