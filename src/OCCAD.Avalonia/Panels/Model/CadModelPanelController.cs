using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

internal sealed class CadModelPanelController
{
    private readonly CadWorkspace _workspace;
    private readonly TextBox _search;
    private readonly TreeView _tree;
    private readonly Action<CadEntity> _inspectEntity;
    private readonly Action<string> _publishStatus;
    private bool _refreshing;

    public CadModelPanelController(
        CadWorkspace workspace,
        TextBox search,
        TreeView tree,
        Action<CadEntity> inspectEntity,
        Action<string> publishStatus)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _inspectEntity = inspectEntity ?? throw new ArgumentNullException(nameof(inspectEntity));
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

            // The model browser follows the authoritative entity registry rather
            // than maintaining a second feature whitelist. This keeps newly
            // registered OCCAD entities visible automatically while excluding
            // internal Path support entities used only by feature persistence.
            var entities = _workspace.Document.Entities
                .Where(IsVisibleInModelTree)
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

                    return new TreeViewItem
                    {
                        Header = $"{CadLanguageManager.Text(group.Key.ResourceKey, group.Key.Fallback)} [{group.Count()}]",
                        IsExpanded = true,
                        ItemsSource = typeGroups
                    };
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

    private bool IsVisibleInModelTree(CadEntity entity)
    {
        if (entity.EntityType.Equals("Path", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            _workspace.Entities.GetRequired(entity);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
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
            Tag = entity,
            IsSelected = ReferenceEquals(entity, selected),
            Opacity = _workspace.Document.IsEntitySelectable(entity)
                ? 1.0
                : 0.55
        };

        var container = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameText = new TextBlock
        {
            Text = entity.Name,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = CadTheme.FontSize,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        container.Children.Add(nameText);

        var layerText = new TextBlock
        {
            Text = $"[{entity.Layer}]",
            Foreground = CadTheme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = CadTheme.SmallFontSize,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        container.Children.Add(layerText);

        var editBox = new TextBox
        {
            Text = entity.Name,
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Height = CadTheme.ControlHeight,
            MinWidth = 90
        };
        container.Children.Add(editBox);

        void StartRename()
        {
            nameText.IsVisible = false;
            layerText.IsVisible = false;
            editBox.Text = entity.Name;
            editBox.IsVisible = true;
            editBox.Focus();
            editBox.SelectAll();
        }

        void CommitRename()
        {
            if (!editBox.IsVisible)
                return;

            var newName = editBox.Text?.Trim();
            editBox.IsVisible = false;
            nameText.IsVisible = true;
            layerText.IsVisible = true;
            if (!string.IsNullOrWhiteSpace(newName) && newName != entity.Name)
            {
                if (CadPropertyTransaction.TryApply(
                        _workspace,
                        [entity],
                        nameof(CadEntity.Name),
                        newName,
                        out var error))
                {
                    nameText.Text = entity.Name;
                    Refresh();
                }
                else if (error is not null)
                {
                    _publishStatus(error.GetBaseException().Message);
                }
            }
        }

        void CancelRename()
        {
            editBox.IsVisible = false;
            nameText.IsVisible = true;
            layerText.IsVisible = true;
        }

        editBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitRename();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelRename();
                e.Handled = true;
            }
        };
        editBox.LostFocus += (_, _) => CommitRename();

        item.Header = container;
        item.ContextMenu = BuildEntityContextMenu(entity, StartRename);
        return item;
    }

    private ContextMenu BuildEntityContextMenu(CadEntity entity, Action startRename)
    {
        var properties = new MenuItem
        {
            Header = CadLanguageManager.Text("Cad.Text.Properties", "Properties")
        };
        properties.Click += (_, _) => _inspectEntity(entity);

        var visibility = new MenuItem
        {
            Header = CadLanguageManager.Text(
                entity.Visible ? "Cad.Text.Hide" : "Cad.Text.Show",
                entity.Visible ? "Hide" : "Show")
        };
        visibility.Click += (_, _) =>
            ApplyEntityFlag(entity, nameof(CadEntity.Visible), !entity.Visible);

        var locking = new MenuItem
        {
            Header = CadLanguageManager.Text(
                entity.Selectable ? "Cad.Text.Lock" : "Cad.Text.Unlock",
                entity.Selectable ? "Lock" : "Unlock")
        };
        locking.Click += (_, _) =>
            ApplyEntityFlag(entity, nameof(CadEntity.Selectable), !entity.Selectable);

        var rename = new MenuItem
        {
            Header = CadLanguageManager.Text("Cad.Text.Rename", "Rename")
        };
        rename.Click += (_, _) => startRename();

        // Editing actions such as delete/transform are intentionally absent from
        // the initial release surface. The model tree remains a selection,
        // visibility, metadata and inspection surface only.
        return new ContextMenu
        {
            ItemsSource = new object[]
            {
                properties,
                visibility,
                locking,
                rename
            }
        };
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
            "BOX" or
            "CYLINDER" or
            "CONE" or
            "FRUSTUM" or
            "SPHERE" or
            "ELLIPSOID" or
            "TORUS" or
            "EXTRUDE" or
            "REVOLVE" or
            "SWEEP" or
            "LOFT" => new("Cad.Text.ModelSolids", "Solids", 30),
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
            "BOX" => 210,
            "CYLINDER" => 220,
            "CONE" => 230,
            "FRUSTUM" => 240,
            "SPHERE" => 250,
            "ELLIPSOID" => 260,
            "TORUS" => 270,
            "EXTRUDE" => 310,
            "REVOLVE" => 320,
            "SWEEP" => 330,
            "LOFT" => 340,
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
