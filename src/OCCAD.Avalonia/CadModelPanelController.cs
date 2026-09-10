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

            var entities = _workspace.Document.Entities
                .Where(static entity => VisibleEntityTypes.Contains(entity.EntityType))
                .Where(entity => MatchesFilter(entity, filter))
                .ToArray();

            var categories = entities
                .GroupBy(entity => CategoryFor(entity.EntityType))
                .OrderBy(static group => group.Key.Order)
                .Select(group =>
                {
                    var typeGroups = group
                        .GroupBy(static entity => entity.EntityType)
                        .OrderBy(typeGroup => EntityOrder(typeGroup.Key))
                        .ThenBy(
                            typeGroup => typeGroup.Key,
                            StringComparer.CurrentCultureIgnoreCase)
                        .Select(typeGroup => CreateTypeGroup(typeGroup, selected))
                        .ToArray();

                    var category = new TreeViewItem
                    {
                        Header = $"{CadLanguageManager.Text(group.Key.ResourceKey, group.Key.Fallback)} [{group.Count()}]",
                        IsExpanded = true,
                        ItemsSource = typeGroups
                    };
                    return category;
                })
                .ToArray();

            _tree.ItemsSource = categories;
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
                change.EntityChangeKind is CadEntityChangeKind.Metadata or CadEntityChangeKind.Appearance))
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

        // Category/type nodes are navigation nodes. Selecting one must not
        // unexpectedly clear the current CAD selection.
        if (_tree.SelectedItem is not TreeViewItem { Tag: CadEntity entity } item)
            return;

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
                !entity.Visible
                    ? CadLanguageManager.Text("Cad.Text.EntityHiddenMessage", "The entity is hidden.")
                    : !entity.Selectable
                        ? CadLanguageManager.Text("Cad.Text.EntityLockedMessage", "The entity is locked.")
                        : layer.Visible
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
        if (_tree.ItemsSource is not IEnumerable<TreeViewItem> roots)
            return;

        _refreshing = true;
        try
        {
            foreach (var root in roots)
                SelectEntityRecursive(root, entity);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private TreeViewItem CreateTypeGroup(
        IGrouping<string, CadEntity> group,
        CadEntity? selected)
    {
        var entities = group
            .OrderBy(
                static entity => entity.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var groupItem = new TreeViewItem
        {
            Header = $"{LocalizeEntityType(entities[0])} [{entities.Length}]",
            IsExpanded = true
        };

        groupItem.ItemsSource = entities
            .Select(entity => CreateEntityItem(entity, selected))
            .ToArray();
        return groupItem;
    }

    private TreeViewItem CreateEntityItem(CadEntity entity, CadEntity? selected)
    {
        var item = new TreeViewItem
        {
            Header = $"{entity.Name}  [{entity.Layer}]",
            Tag = entity,
            IsSelected = ReferenceEquals(entity, selected),
            Opacity = _workspace.Document.IsEntitySelectable(entity)
                ? 1.0
                : 0.55
        };
        item.ContextMenu = BuildEntityContextMenu(entity);
        return item;
    }

    private ContextMenu BuildEntityContextMenu(CadEntity entity)
    {
        var properties = new MenuItem
        {
            Header = CadLanguageManager.Text("Cad.Text.Properties", "Properties")
        };
        properties.Click += (_, _) => SelectForAction(entity);

        var visibility = new MenuItem
        {
            Header = CadLanguageManager.Text(
                entity.Visible ? "Cad.Text.Hide" : "Cad.Text.Show",
                entity.Visible ? "Hide" : "Show")
        };
        visibility.Click += (_, _) => ApplyEntityFlag(entity, nameof(CadEntity.Visible), !entity.Visible);

        var locking = new MenuItem
        {
            Header = CadLanguageManager.Text(
                entity.Selectable ? "Cad.Text.Lock" : "Cad.Text.Unlock",
                entity.Selectable ? "Lock" : "Unlock")
        };
        locking.Click += (_, _) => ApplyEntityFlag(entity, nameof(CadEntity.Selectable), !entity.Selectable);

        var delete = new MenuItem
        {
            Header = CadLanguageManager.Text("Cad.Text.Delete", "Delete"),
            IsEnabled = _workspace.Document.IsEntitySelectable(entity)
        };
        delete.Click += (_, _) =>
        {
            if (!SelectForAction(entity))
                return;
            _workspace.Actions.Execute("edit.delete");
        };

        return new ContextMenu
        {
            ItemsSource = new object[]
            {
                properties,
                visibility,
                locking,
                new Separator(),
                delete
            }
        };
    }

    private bool SelectForAction(CadEntity entity)
    {
        if (!_workspace.Document.IsEntitySelectable(entity))
        {
            _publishStatus(
                CadLanguageManager.Text(
                    "Cad.Text.EntityNotSelectable",
                    "The entity is not selectable."));
            return false;
        }

        _workspace.Selection.Select(entity);
        return true;
    }

    private void ApplyEntityFlag(CadEntity entity, string propertyName, bool value)
    {
        if (CadPropertyTransaction.TryApply(
                _workspace,
                [entity],
                propertyName,
                value,
                out var error))
            return;

        if (error is not null)
            _publishStatus(error.GetBaseException().Message);
    }

    private static bool SelectEntityRecursive(TreeViewItem item, CadEntity? entity)
    {
        var selected =
            entity is not null &&
            item.Tag is CadEntity candidate &&
            ReferenceEquals(candidate, entity);
        item.IsSelected = selected;

        var descendantSelected = false;
        if (item.ItemsSource is IEnumerable<TreeViewItem> children)
        {
            foreach (var child in children)
                descendantSelected |= SelectEntityRecursive(child, entity);
        }

        if (selected || descendantSelected)
            item.IsExpanded = true;
        return selected || descendantSelected;
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

    private static ModelCategory CategoryFor(string entityType) =>
        entityType.ToUpperInvariant() switch
        {
            "POINT" => new("Cad.Text.ModelPoints", "Points", 10),
            "TEXT" or
            "LENGTH DIMENSION" or
            "ANGLE DIMENSION" or
            "CIRCULAR DIMENSION" => new("Cad.Text.ModelAnnotations", "Annotations", 40),
            "BOX" or
            "CYLINDER" or
            "CONE" or
            "FRUSTUM" or
            "SPHERE" or
            "ELLIPSOID" or
            "TORUS" or
            "EXTRUDE" => new("Cad.Text.ModelSolids", "Solids", 30),
            _ => new("Cad.Text.ModelCurves", "Curves", 20)
        };

    private static int EntityOrder(string entityType) =>
        entityType.ToUpperInvariant() switch
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
            "HELIX" => 110,
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
            "EXTRUDE" => 310,
            _ => int.MaxValue
        };

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

    private sealed record ModelCategory(
        string ResourceKey,
        string Fallback,
        int Order);
}
