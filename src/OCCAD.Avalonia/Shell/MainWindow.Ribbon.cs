using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, List<Button>> _ribbonActionButtons =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RibbonActionMenuBinding> _ribbonActionMenus = [];
    private readonly Dictionary<string, ToggleButton> _ribbonPanelToggles =
        new(StringComparer.OrdinalIgnoreCase);
    private Border? _ribbonHost;
    private bool _ribbonApplied;

    internal bool IsRibbonApplied => _ribbonApplied;

    internal void ApplyRibbon()
    {
        if (_ribbonApplied || Content is not DockPanel root)
            return;

        // The shell is Ribbon-first. Layer selection is hosted by Home while
        // drafting/work-plane controls are hosted by the status surface.
        DetachRibbonControl(_layerCombo);

        _ribbonHost = new Border
        {
            Background = CadTheme.Toolbar,
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        DockPanel.SetDock(_ribbonHost, Dock.Top);
        root.Children.Insert(0, _ribbonHost);

        _ribbonApplied = true;
        RebuildRibbon();
        RefreshRibbonActionUi();
        RefreshRibbonPanelState();

        CadDiagnostics.Trace("Native Avalonia CAD ribbon applied.");
    }

    private void RebuildRibbon()
    {
        if (!_ribbonApplied || _ribbonHost is null)
            return;

        DetachRibbonControl(_layerCombo);

        _ribbonActionButtons.Clear();
        _ribbonActionMenus.Clear();
        _ribbonPanelToggles.Clear();
        _ribbonHost.Child = BuildRibbonContent();
        RefreshRibbonActionUi();
        RefreshRibbonPanelState();
    }

    private Control BuildRibbonContent()
    {
        var synchronizingSelection = false;
        var filterCombo = new ComboBox
        {
            Width = 96,
            ItemsSource = new[]
            {
                CadLanguageManager.Text("Cad.Text.EntityFilterAll", "All entities"),
                CadLanguageManager.Text("Cad.Text.EntityFilterPoints", "Points"),
                CadLanguageManager.Text("Cad.Text.EntityFilterCurves", "Curves"),
                CadLanguageManager.Text("Cad.Text.EntityFilterRegions", "Regions"),
                CadLanguageManager.Text("Cad.Text.EntityFilterSolids", "Solids")
            },
            SelectedIndex = (int)_workspace.Selection.FilterKind
        };
        filterCombo.Classes.Add("cad-input");
        filterCombo.SelectionChanged += (_, _) =>
        {
            if (!synchronizingSelection && filterCombo.SelectedIndex >= 0)
                _workspace.Selection.FilterKind = (CadEntityFilterKind)filterCombo.SelectedIndex;
        };

        var scopeMasks = new[] { CadSubshapeMask.None, CadSubshapeMask.Vertex,
            CadSubshapeMask.Edge, CadSubshapeMask.Wire, CadSubshapeMask.Face,
            CadSubshapeMask.Shell, CadSubshapeMask.Solid, CadSubshapeMask.All };
        var scopeCombo = new ComboBox
        {
            Width = 96,
            ItemsSource = new[] { "Entity", "Vertex", "Edge", "Wire", "Face", "Shell", "Solid", "Subobject" }
                .Select(name => CadLanguageManager.Text($"Cad.Text.SelectionScope{name}", name)).ToArray(),
            SelectedIndex = _workspace.Selection.Scope == CadSelectionScope.Entity ? 0 :
                Math.Max(1, Array.IndexOf(scopeMasks, _workspace.Selection.SubshapeMask))
        };
        scopeCombo.Classes.Add("cad-input");
        scopeCombo.SelectionChanged += (_, _) =>
        {
            if (synchronizingSelection || scopeCombo.SelectedIndex < 0) return;
            _workspace.Selection.SetScope(scopeCombo.SelectedIndex == 0
                ? CadSelectionScope.Entity : CadSelectionScope.Subobject,
                scopeCombo.SelectedIndex == 0 ? CadSubshapeMask.All : scopeMasks[scopeCombo.SelectedIndex]);
        };

        void SynchronizeSelectionControls(object? sender, EventArgs args)
        {
            synchronizingSelection = true;
            try
            {
                filterCombo.SelectedIndex = (int)_workspace.Selection.FilterKind;
                scopeCombo.SelectedIndex = _workspace.Selection.Scope == CadSelectionScope.Entity ? 0 :
                    Math.Max(1, Array.IndexOf(scopeMasks, _workspace.Selection.SubshapeMask));
                scopeCombo.IsEnabled = _workspace.Tools.ActiveTool is null;
                filterCombo.IsEnabled = _workspace.Selection.Scope == CadSelectionScope.Entity;
            }
            finally { synchronizingSelection = false; }
        }
        scopeCombo.AttachedToVisualTree += (_, _) =>
        {
            _workspace.Selection.FilterChanged += SynchronizeSelectionControls;
            _workspace.Tools.ToolChanged += SynchronizeSelectionControls;
            SynchronizeSelectionControls(null, EventArgs.Empty);
        };
        scopeCombo.DetachedFromVisualTree += (_, _) =>
        {
            _workspace.Selection.FilterChanged -= SynchronizeSelectionControls;
            _workspace.Tools.ToolChanged -= SynchronizeSelectionControls;
        };

        var tabs = new TabControl
        {
            Background = CadTheme.Toolbar,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinHeight = 120,
            MaxHeight = 128
        };

        tabs.ItemsSource = new object[]
        {
            RibbonTab(
                "Cad.Text.File",
                "File",
                RibbonGroup(
                    "Cad.Text.File",
                    "File",
                    FileButton("Cad.Text.New", "New", async () => await NewDocumentAsync()),
                    FileButton("Cad.Text.Open", "Open", async () => await OpenDocumentAsync()),
                    FileButton("Cad.Text.Save", "Save", async () => await SaveDocumentAsync(saveAs: false)),
                    FileButton("Cad.Text.SaveAs", "Save As", async () => await SaveDocumentAsync(saveAs: true)),
                    FileButton("Cad.Text.Import", "Import", async () => await ImportDocumentAsync()),
                    FileButton("Cad.Text.Export", "Export", async () => await ExportDocumentAsync()),
                    FileButton("Cad.Text.Preferences", "Settings", async () => await ShowApplicationSettingsAsync()),
                    RibbonAction("file.clear"),
                    FileButton("Cad.Text.Exit", "Exit", () =>
                    {
                        Close();
                        return Task.CompletedTask;
                    }))),

            RibbonTab(
                "Cad.Text.Home",
                "Home",
                RibbonGroup(
                    "Cad.Text.Edit",
                    "Edit",
                    RibbonAction("edit.undo"),
                    RibbonAction("edit.redo"),
                    RibbonAction("edit.delete")),
                RibbonGroup(
                    "Cad.Text.Selection",
                    "Selection",
                    RibbonAction("select"),
                    RibbonAction("select.all"),
                    RibbonAction("select.invert"),
                    RibbonLabel("Cad.Text.SelectionFilter", "Filter"),
                    filterCombo,
                    RibbonLabel("Cad.Text.SelectionScope", "Scope"),
                    scopeCombo),
                RibbonGroup(
                    "Cad.Text.Layer",
                    "Layer",
                    RibbonLabel("Cad.Text.CurrentLayer", "Current Layer"),
                    _layerCombo,
                    FileButton("Cad.Text.Layers", "Layer Manager", () =>
                    {
                        SetLayerPanelVisible(true);
                        return Task.CompletedTask;
                    }))),

            RibbonTab(
                "Cad.Text.Draw",
                "Draw",
                RibbonGroup(
                    "Cad.Text.Basic",
                    "Basic",
                    RibbonAction("draw.point"),
                    RibbonAction("draw.line"),
                    RibbonAction("draw.polyline"),
                    RibbonAction("draw.rectangle"),
                    RibbonAction("draw.polygon"),
                    RibbonAction("draw.spline")),
                RibbonGroup(
                    "Cad.Text.Construction",
                    "Construction",
                    RibbonActionMenu(
                        "Cad.Text.Circle",
                        "Circle",
                        new("draw.circle.centerradius"),
                        new("draw.circle.centerdiameter"),
                        new("draw.circle.twopoints"),
                        new("draw.circle.threepoints"),
                        new("draw.circle.pointcenter")),
                    RibbonActionMenu(
                        "Cad.Text.ArcFamily",
                        "Arc",
                        new("draw.arc.threepoints"),
                        new("draw.arc.centerstartend"),
                        new("draw.arc.startcenterend"),
                        new("draw.arc.startendcenter"),
                        new("draw.arc.startendpoint"),
                        new("draw.arc.startendtangent")),
                    RibbonActionMenu(
                        "Cad.Text.RegularPolygon",
                        "Regular Polygon",
                        new("draw.regularpolygon.inscribed"),
                        new("draw.regularpolygon.circumscribed")),
                    RibbonActionMenu(
                        "Cad.Text.Ellipse",
                        "Ellipse",
                        new("draw.ellipse.centermajor"),
                        new("draw.ellipse.axisendpoints"))),
                RibbonGroup(
                    "Cad.Text.Modeling",
                    "Model",
                    RibbonActionMenu(
                        "Cad.Text.Primitives",
                        "Primitives",
                        new("solid.box"),
                        new("solid.cylinder"),
                        new("solid.cone"),
                        new("solid.frustum"),
                        new("solid.sphere"),
                        new("solid.ellipsoid"),
                        new("solid.torus")),
                    RibbonAction("curve.helix"),
                    RibbonAction("feature.extrude"),
                    RibbonAction("feature.revolve"),
                    RibbonAction("feature.sweep"),
                    RibbonAction("feature.loft"))),

            RibbonTab(
                "Cad.Text.Modify",
                "Modify",
                RibbonGroup(
                    "Cad.Text.Transform",
                    "Transform",
                    RibbonAction("modify.move"),
                    RibbonAction("modify.copy"),
                    RibbonAction("modify.rotate"),
                    RibbonAction("modify.scale"),
                    RibbonAction("modify.mirror"),
                    RibbonAction("modify.array")),
                RibbonGroup(
                    "Cad.Text.Edit",
                    "Edit",
                    RibbonAction("modify.offset"),
                    RibbonAction("modify.trim"),
                    RibbonAction("modify.extend"),
                    RibbonAction("modify.fillet"),
                    RibbonAction("modify.chamfer"))),

            RibbonTab(
                "Cad.Text.Annotate",
                "Annotate",
                RibbonGroup(
                    "Cad.Text.Annotation",
                    "Annotation",
                    RibbonAction("annotate.text"),
                    RibbonAction("annotate.length"),
                    RibbonAction("annotate.angle"),
                    RibbonAction("annotate.radius"),
                    RibbonAction("annotate.diameter")),
                RibbonGroup(
                    "Cad.Text.Measure",
                    "Measure",
                    RibbonAction("measure.distance"))),

            RibbonTab(
                "Cad.Text.View",
                "View",
                RibbonGroup(
                    "Cad.Text.StandardViews",
                    "Standard Views",
                    RibbonAction("view.fit"),
                    RibbonAction("view.isometric"),
                    RibbonAction("view.top"),
                    RibbonAction("view.bottom"),
                    RibbonAction("view.front"),
                    RibbonAction("view.back"),
                    RibbonAction("view.left"),
                    RibbonAction("view.right")),
                RibbonGroup(
                    "Cad.Text.Display",
                    "Display",
                    RibbonAction("display.wireframe"),
                    RibbonAction("display.shaded"),
                    RibbonAction("display.transparent"),
                    RibbonAction("display.hiddenline"),
                    RibbonAction("view.hide"),
                    RibbonAction("view.isolate"),
                    RibbonAction("view.showall"))),

            RibbonTab(
                "Cad.Text.Manage",
                "Manage",
                RibbonGroup(
                    "Cad.Text.Panels",
                    "Panels",
                    RibbonPanelToggle("model", "Cad.Text.Model", "Model", value => SetModelPanelVisible(value)),
                    RibbonPanelToggle("layers", "Cad.Text.Layers", "Layers", SetLayerPanelVisible),
                    RibbonPanelToggle("properties", "Cad.Text.Properties", "Properties", SetPropertyPanelVisible),
                    RibbonPanelToggle("tool", "Cad.Text.ToolParameters", "Tool Parameters", SetToolParameterPanelVisible)),
                RibbonGroup(
                    "Cad.Text.Settings",
                    "Settings",
                    FileButton("Cad.Text.Preferences", "Preferences...", async () => await ShowApplicationSettingsAsync()),
                    FileButton("Cad.Text.Chinese", "中文", () =>
                    {
                        CadLanguageManager.Apply("zh-CN");
                        return Task.CompletedTask;
                    }),
                    FileButton("Cad.Text.English", "English", () =>
                    {
                        CadLanguageManager.Apply("en-US");
                        return Task.CompletedTask;
                    })))
        };

        return tabs;
    }

    private TabItem RibbonTab(
        string resourceKey,
        string fallback,
        params Control[] groups)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(4, 2, 4, 2)
        };
        foreach (var group in groups)
            panel.Children.Add(group);

        return new TabItem
        {
            Header = UiText(resourceKey, fallback),
            Content = new ScrollViewer
            {
                Content = panel,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = CadTheme.Toolbar
            }
        };
    }

    private static Border RibbonGroup(
        string resourceKey,
        string fallback,
        params Control[] controls)
    {
        const int rows = 3;
        var body = new Grid
        {
            RowSpacing = 2,
            ColumnSpacing = 2,
            Margin = new Thickness(3, 2, 3, 2)
        };
        for (var row = 0; row < rows; row++)
            body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var columns = Math.Max(1, (int)Math.Ceiling(controls.Length / (double)rows));
        for (var column = 0; column < columns; column++)
            body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        for (var index = 0; index < controls.Length; index++)
        {
            var control = controls[index];
            Grid.SetRow(control, index % rows);
            Grid.SetColumn(control, index / rows);
            body.Children.Add(control);
        }

        var divider = new Border
        {
            Height = 1,
            Background = CadTheme.Border,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var caption = new TextBlock
        {
            Text = CadLanguageManager.Text(resourceKey, fallback),
            FontSize = 9.5,
            FontWeight = FontWeight.Medium,
            Foreground = CadTheme.Muted,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 1, 4, 1)
        };

        var captionBar = new Border
        {
            Background = CadTheme.Panel,
            CornerRadius = new CornerRadius(0, 0, 2, 2),
            Height = 16,
            Child = caption
        };

        var panel = new StackPanel { Spacing = 0 };
        panel.Children.Add(body);
        panel.Children.Add(divider);
        panel.Children.Add(captionBar);

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(2, 2, 2, 3),
            Child = panel
        };
    }

    private string CommandCaption(string id)
    {
        var descriptor = _workspace.Actions.Describe(id)!;
        return CadLanguageManager.Text(descriptor.DisplayNameKey, descriptor.EnglishName);
    }

    private Button RibbonAction(string id)
    {
        var command = _workspace.Actions.Describe(id) ?? throw new InvalidOperationException($"Unknown command {id}.");
        var button = RibbonButton(CadLanguageManager.Text(command.DisplayNameKey, command.EnglishName), id);
        ToolTip.SetTip(button, command.Shortcut is { } shortcut
            ? $"{command.EnglishName} ({shortcut})" : command.EnglishName);
        button.Tag = id;
        button.Click += (_, _) =>
        {
            ExecuteAction(id);
            _viewport.Focus();
        };

        if (!_ribbonActionButtons.TryGetValue(id, out var buttons))
        {
            buttons = [];
            _ribbonActionButtons.Add(id, buttons);
        }
        buttons.Add(button);
        return button;
    }

    private Button RibbonActionMenu(
        string resourceKey,
        string fallback,
        params RibbonMenuAction[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (actions.Length == 0)
            throw new ArgumentException("Ribbon action menu requires at least one action.", nameof(actions));

        var button = RibbonButton(
            CadLanguageManager.Text(resourceKey, fallback) + " ▾",
            resourceKey);
        var items = actions
            .Select(action =>
            {
                var item = new MenuItem
                {
                    Header = CommandCaption(action.Id),
                    Tag = action.Id
                };
                item.Click += (_, _) =>
                {
                    ExecuteAction(action.Id);
                    _viewport.Focus();
                };
                return item;
            })
            .ToArray();

        var menu = new ContextMenu
        {
            ItemsSource = items
        };
        menu.Opening += (_, _) =>
        {
            for (var index = 0; index < actions.Length; index++)
            {
                items[index].IsEnabled =
                    _workspace.Actions.Find(actions[index].Id)?.CanExecute() == true;
            }
        };
        button.ContextMenu = menu;
        button.Click += (_, _) => menu.Open(button);

        _ribbonActionMenus.Add(new RibbonActionMenuBinding(button, actions));
        return button;
    }

    private static Button RibbonButton(string text, string? iconKey = null)
    {
        var button = new Button
        {
            MinWidth = 56,
            Height = 23,
            Padding = new Thickness(5, 1),
            Margin = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("cad-ribbon-button");

        var label = new TextBlock
        {
            Text = text,
            FontSize = 11.0,
            Foreground = CadTheme.Text,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (CadRibbonIcons.CreateIcon(iconKey) is { } icon)
        {
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(icon);
            content.Children.Add(label);
            button.Content = content;
        }
        else
        {
            button.Content = label;
        }

        return button;
    }

    private static TextBlock RibbonLabel(string resourceKey, string fallback) =>
        new()
        {
            Text = CadLanguageManager.Text(resourceKey, fallback),
            Foreground = CadTheme.Muted,
            FontSize = CadTheme.CaptionFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0)
        };

    private Button FileButton(
        string resourceKey,
        string fallback,
        Func<Task> action)
    {
        var button = RibbonButton(CadLanguageManager.Text(resourceKey, fallback), resourceKey);
        button.Click += async (_, _) => await action();
        return button;
    }

    private ToggleButton RibbonPanelToggle(
        string id,
        string resourceKey,
        string fallback,
        Action<bool> changed)
    {
        var button = new ToggleButton
        {
            MinWidth = 62,
            Height = 23,
            Padding = new Thickness(5, 1),
            Margin = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("cad-ribbon-toggle");

        var label = new TextBlock
        {
            Text = CadLanguageManager.Text(resourceKey, fallback),
            FontSize = 11.0,
            Foreground = CadTheme.Text,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (CadRibbonIcons.CreateIcon(id) is { } icon)
        {
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(icon);
            content.Children.Add(label);
            button.Content = content;
        }
        else
        {
            button.Content = label;
        }

        button.Click += (_, _) => changed(button.IsChecked == true);
        _ribbonPanelToggles[id] = button;
        return button;
    }

    private void RefreshRibbonActionUi()
    {
        if (!_ribbonApplied)
            return;

        foreach (var pair in _ribbonActionButtons)
        {
            var enabled = _workspace.Actions.Find(pair.Key)?.CanExecute() == true;
            foreach (var button in pair.Value)
                button.IsEnabled = enabled;
        }

        foreach (var binding in _ribbonActionMenus)
        {
            binding.Button.IsEnabled = binding.Actions.Any(action =>
                _workspace.Actions.Find(action.Id)?.CanExecute() == true);
        }
    }

    private void RefreshRibbonPanelState()
    {
        if (!_ribbonApplied)
            return;

        if (_ribbonPanelToggles.TryGetValue("model", out var model))
            model.IsChecked = _modelPanel.IsVisible;
        if (_ribbonPanelToggles.TryGetValue("layers", out var layers))
            layers.IsChecked = _layerPanelBorder.IsVisible;
        if (_ribbonPanelToggles.TryGetValue("properties", out var properties))
            properties.IsChecked = _propertyPanelBorder.IsVisible;
        if (_ribbonPanelToggles.TryGetValue("tool", out var tool))
        {
            tool.IsEnabled = CanShowToolParameterPanel;
            tool.IsChecked = IsToolParameterPanelVisible;
        }
    }

    private void RefreshRibbonLanguage()
    {
        if (_ribbonApplied)
            RebuildRibbon();
    }

    private static void DetachRibbonControl(Control control)
    {
        if (control.Parent is Panel panel)
            panel.Children.Remove(control);
        else if (control.Parent is ContentControl content && ReferenceEquals(content.Content, control))
            content.Content = null;
    }

    private sealed record RibbonMenuAction(string Id);

    private sealed record RibbonActionMenuBinding(
        Button Button,
        IReadOnlyList<RibbonMenuAction> Actions);
}
