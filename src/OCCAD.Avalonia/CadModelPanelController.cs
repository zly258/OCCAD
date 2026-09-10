using Avalonia.Controls;
using OCCAD;

namespace OCCAD.Avalonia;

internal sealed class CadModelPanelController
{
    private static readonly HashSet<string> VisibleEntityTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // 2D scene entities.
            "Point",
            "Line",
            "Polyline",
            "Rectangle",
            "Polygon",
            "Regular Polygon",
            "Circle",
            "Arc",
            "Ellipse",
            "Spline",

            // Annotation entities with complete interactive tools.
            "Text",
            "Length Dimension",
            "Angle Dimension",
            "Circular Dimension",

            // 3D/spatial scene entities.
            "Box",
            "Cylinder",
            "Cone",
            "Frustum",
            "Sphere",
            "Ellipsoid",
            "Torus",
            "Helix",

            // Feature entities with complete interactive tools.
            "Extrude"
        };

    private readonly CadWorkspace _workspace;
    private readonly TextBox _search;
    private readonly TreeView _tree;
    private readonly Action<string> _publishStatus;
    private bool _refreshing;

    public CadModelPanelController(
        CadWorkspace workspace,
        TextBox search,
        TreeView tree,
        Action<string> publishStatus)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _publishStatus = publishStatus ?? throw new ArgumentNullException(nameof(publishStatus));
    }

    public void Refresh()
    {
        if (_refreshing)
            return;

        _refreshing = true;
        try
        {
            var filter = _search.Text?.Trim() ?? string.Empty;
            var selected = _workspace.Selection.Primary;

            var groups = _workspace.Document.Entities
                .Where(static entity => VisibleEntityTypes.Contains(entity.EntityType))
                .GroupBy(static entity => entity.EntityType)
                .Select(group =>
                {
                    var entities = group
                        .Where(entity => MatchesFilter(entity, filter))
                        .OrderBy(
                            static entity => entity.Name,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();

                    if (entities.Length == 0)
                        return null;

                    var groupItem = new TreeViewItem
                    {
                        Header = $"{LocalizeEntityType(entities[0])} [{entities.Length}]",
                        IsExpanded = true
                    };

                    groupItem.ItemsSource = entities
                        .Select(entity => new TreeViewItem
                        {
                            Header = $"{entity.Name}  [{entity.Layer}]",
                            Tag = entity,
                            IsSelected = ReferenceEquals(entity, selected),
                            Opacity = _workspace.Document.IsEntitySelectable(entity)
                                ? 1.0
                                : 0.55
                        })
                        .ToArray();

                    return groupItem;
                })
                .Where(static item => item is not null)
                .Cast<TreeViewItem>()
                .OrderBy(
                    item => EntityOrder(item.ItemsSource),
                    Comparer<int>.Default)
                .ThenBy(
                    item => item.Header?.ToString(),
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            _tree.ItemsSource = groups;
        }
        finally
        {
            _refreshing = false;
        }
    }

    public void RefreshLanguage()
    {
        _search.PlaceholderText = CadLanguageManager.Text(
            "Cad.Text.FilterModel",
            "Filter model tree");
        Refresh();
    }

    public void ApplyDocumentChangeSet(CadDocumentChangeSetEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Contains(CadDocumentChangeKind.Added) ||
            args.Contains(CadDocumentChangeKind.Removed) ||
            args.Contains(CadDocumentChangeKind.Reset) ||
            args.Changes.Any(change =>
                change.Kind == CadDocumentChangeKind.Changed &&
                change.EntityChangeKind == CadEntityChangeKind.Metadata))
        {
            Refresh();
        }
    }

    public void ApplyLayerManagerChange(CadLayerManagerChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Kind != CadLayerManagerChangeKind.CurrentChanged)
            Refresh();
    }

    public void HandleSelectionChanged()
    {
        if (_refreshing)
            return;

        if (_workspace.Tools.ActiveTool is { } activeTool &&
            !activeTool.InteractionPolicy.SelectionEnabled)
        {
            _workspace.Tools.CancelCurrent();
        }

        if (_tree.SelectedItem is not TreeViewItem { Tag: CadEntity entity } item)
        {
            _workspace.Selection.Clear();
            return;
        }

        if (!_workspace.Document.IsEntitySelectable(entity))
        {
            _refreshing = true;
            try
            {
                item.IsSelected = false;
            }
            finally
            {
                _refreshing = false;
            }

            var layer = _workspace.Layers.GetRequired(entity.Layer);
            _publishStatus(
                layer.Visible
                    ? Format(
                        "Cad.Text.LayerLockedMessage",
                        "Layer {0} is locked.",
                        layer.Name)
                    : Format(
                        "Cad.Text.LayerHiddenMessage",
                        "Layer {0} is hidden.",
                        layer.Name));
            return;
        }

        _workspace.Selection.Select(entity);
    }

    public void SelectEntity(CadEntity? entity)
    {
        if (_tree.ItemsSource is not IEnumerable<TreeViewItem> groups)
            return;

        _refreshing = true;
        try
        {
            foreach (var group in groups)
            {
                if (group.ItemsSource is not IEnumerable<TreeViewItem> items)
                    continue;

                foreach (var item in items)
                {
                    var selected =
                        entity is not null &&
                        ReferenceEquals(item.Tag, entity);
                    item.IsSelected = selected;
                    if (selected)
                        group.IsExpanded = true;
                }
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private bool MatchesFilter(CadEntity entity, string filter) =>
        filter.Length == 0 ||
        entity.Name.Contains(
            filter,
            StringComparison.CurrentCultureIgnoreCase) ||
        entity.Layer.Contains(
            filter,
            StringComparison.CurrentCultureIgnoreCase) ||
        LocalizeEntityType(entity).Contains(
            filter,
            StringComparison.CurrentCultureIgnoreCase);

    private string LocalizeEntityType(CadEntity entity)
    {
        var descriptor = _workspace.Entities.GetRequired(entity);
        return CadLanguageManager.Text(
            descriptor.LocalizationKey,
            descriptor.DisplayName);
    }

    private static int EntityOrder(object? itemsSource)
    {
        if (itemsSource is not IEnumerable<TreeViewItem> items ||
            items.FirstOrDefault()?.Tag is not CadEntity entity)
            return int.MaxValue;

        return entity.EntityType.ToUpperInvariant() switch
        {
            "POINT" => 10,
            "LINE" => 20,
            "POLYLINE" => 30,
            "RECTANGLE" => 40,
            "POLYGON" => 50,
            "REGULAR POLYGON" => 60,
            "CIRCLE" => 70,
            "ARC" => 80,
            "ELLIPSE" => 90,
            "SPLINE" => 100,
            "TEXT" => 150,
            "LENGTH DIMENSION" => 160,
            "ANGLE DIMENSION" => 170,
            "CIRCULAR DIMENSION" => 180,
            "BOX" => 210,
            "CYLINDER" => 220,
            "CONE" => 230,
            "FRUSTUM" => 240,
            "SPHERE" => 250,
            "ELLIPSOID" => 260,
            "TORUS" => 270,
            "HELIX" => 280,
            "EXTRUDE" => 310,
            _ => int.MaxValue
        };
    }

    private static string Format(
        string key,
        string fallback,
        params object?[] arguments)
    {
        var template = CadLanguageManager.Text(key, fallback);
        try
        {
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                template,
                arguments);
        }
        catch (FormatException)
        {
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                fallback,
                arguments);
        }
    }
}
