using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private static readonly IBrush ReferenceWindowBrush = ReferenceBrush("#E8EDF2");
    private static readonly IBrush ReferenceSurfaceBrush = ReferenceBrush("#F8FAFC");
    private static readonly IBrush ReferencePanelBrush = ReferenceBrush("#F5F7F9");
    private static readonly IBrush ReferencePanelHeaderBrush = ReferenceBrush("#E8EDF2");
    private static readonly IBrush ReferenceRibbonBrush = ReferenceBrush("#F7F9FB");
    private static readonly IBrush ReferenceRibbonBodyBrush = ReferenceBrush("#F2F5F8");
    private static readonly IBrush ReferenceAccentBrush = ReferenceBrush("#1774C7");
    private static readonly IBrush ReferenceAccentSoftBrush = ReferenceBrush("#DCECF9");
    private static readonly IBrush ReferenceBorderBrush = ReferenceBrush("#C8D0D8");
    private static readonly IBrush ReferenceTextBrush = ReferenceBrush("#26323D");
    private static readonly IBrush ReferenceMutedBrush = ReferenceBrush("#697987");
    private static readonly IBrush ReferenceViewportToolbarBrush = ReferenceBrush("#394651");

    private bool _referenceVisualShellApplied;
    private Border? _referenceBrandBar;
    private TextBlock? _referenceBrandSubtitle;
    private TextBox? _referenceCommandSearch;
    private TabControl? _referenceRibbonTabs;
    private Grid? _referenceViewportFrame;
    private Border? _referenceViewportToolbar;
    private int _referenceSelectedRibbonIndex = 1;

    private readonly Dictionary<string, List<Button>> _referenceActionButtons =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ReferenceActionMenuBinding> _referenceActionMenus = [];
    private readonly HashSet<string> _referenceKnownActions =
        new(StringComparer.OrdinalIgnoreCase);

    internal void ApplyReferenceVisualShell()
    {
        if (_referenceVisualShellApplied || Content is not DockPanel root)
            return;

        _referenceVisualShellApplied = true;
        CadReferenceVisualTheme.Apply();

        Background = ReferenceWindowBrush;
        root.Background = ReferenceWindowBrush;

        ApplyReferencePanelLayout();
        ApplyReferenceViewportFrame();

        if (_ribbonHost is not null)
        {
            _ribbonHost.Background = ReferenceRibbonBrush;
            _ribbonHost.BorderBrush = ReferenceBorderBrush;
            _ribbonHost.BorderThickness = new Thickness(0, 0, 0, 1);
            _ribbonHost.Child = BuildReferenceRibbon();
        }

        _referenceBrandBar = BuildReferenceBrandBar();
        DockPanel.SetDock(_referenceBrandBar, Dock.Top);
        root.Children.Insert(0, _referenceBrandBar);

        AddReferenceViewportToolbar();
        EnableReferenceViewCube();

        CadLanguageManager.Changed += ReferenceLanguageChanged;
        KeyDown += ReferenceShellKeyDown;
        Closed += ReferenceShellClosed;
        _workspace.Events.Changed += (_, _) => Ui(RefreshReferenceCommandState);

        RefreshReferenceCommandState();
        CadDiagnostics.Trace("Reference industrial CAD visual shell applied.");
    }

    private Border BuildReferenceBrandBar()
    {
        var productMark = CadRibbonIcons.CreateIcon("solid.box");
        if (productMark is not null)
        {
            productMark.Width = 22;
            productMark.Height = 22;
        }

        var mark = new Border
        {
            Width = 30,
            Height = 30,
            Margin = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(6),
            Background = ReferenceAccentSoftBrush,
            BorderBrush = ReferenceAccentBrush,
            BorderThickness = new Thickness(1),
            Child = productMark ?? new TextBlock
            {
                Text = "O",
                Foreground = ReferenceAccentBrush,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var productName = new TextBlock
        {
            Text = "OCCAD",
            FontSize = 19,
            FontWeight = FontWeight.Bold,
            Foreground = ReferenceAccentBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        _referenceBrandSubtitle = new TextBlock
        {
            Text = ReferenceBrandSubtitle(),
            FontSize = 10.5,
            Foreground = ReferenceMutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 1, 0, 0)
        };

        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 0
        };
        brand.Children.Add(mark);
        brand.Children.Add(productName);
        brand.Children.Add(new Border
        {
            Width = 1,
            Height = 20,
            Background = ReferenceBorderBrush,
            Margin = new Thickness(14, 0, 0, 0)
        });
        brand.Children.Add(_referenceBrandSubtitle);

        _referenceCommandSearch = new TextBox
        {
            Width = 260,
            Height = 28,
            PlaceholderText = ReferenceSearchPlaceholder(),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = ReferenceSurfaceBrush,
            BorderBrush = ReferenceBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(9, 0),
            Margin = new Thickness(0, 5, 8, 5)
        };
        _referenceCommandSearch.KeyDown += ReferenceSearchKeyDown;

        var settingsButton = ReferenceHeaderButton("Cad.Text.Preferences", "设置", "Preferences", "settings");
        settingsButton.Click += async (_, _) => await ShowApplicationSettingsAsync();

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 3
        };
        right.Children.Add(_referenceCommandSearch);
        right.Children.Add(settingsButton);

        var grid = new Grid
        {
            Height = 40,
            Background = ReferenceRibbonBrush
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(brand);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Background = ReferenceRibbonBrush,
            BorderBrush = ReferenceBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }

    private Control BuildReferenceRibbon()
    {
        DetachRibbonControl(_layerCombo);
        _referenceActionButtons.Clear();
        _referenceActionMenus.Clear();
        _referenceKnownActions.Clear();

        var tabs = new TabControl
        {
            Background = ReferenceRibbonBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinHeight = 126,
            MaxHeight = 132
        };
        tabs.Classes.Add("occad-reference-ribbon");

        tabs.ItemsSource = new object[]
        {
            ReferenceRibbonTab(
                "Cad.Text.File",
                "文件",
                "File",
                ReferenceRibbonGroup(
                    "Cad.Text.File",
                    "文件",
                    "File",
                    ReferenceFileButton("Cad.Text.New", "新建", "New", "new", async () => await NewDocumentAsync()),
                    ReferenceFileButton("Cad.Text.Open", "打开", "Open", "open", async () => await OpenDocumentAsync()),
                    ReferenceFileButton("Cad.Text.Save", "保存", "Save", "save", async () => await SaveDocumentAsync(saveAs: false)),
                    ReferenceFileButton("Cad.Text.SaveAs", "另存为", "Save As", "saveas", async () => await SaveDocumentAsync(saveAs: true)),
                    ReferenceFileButton("Cad.Text.Import", "导入", "Import", "import", async () => await ImportDocumentAsync()),
                    ReferenceFileButton("Cad.Text.Export", "导出", "Export", "export", async () => await ExportDocumentAsync())),
                ReferenceRibbonGroup(
                    "Cad.Text.Settings",
                    "应用",
                    "Application",
                    ReferenceActionButton("file.clear"),
                    ReferenceFileButton("Cad.Text.Preferences", "设置", "Settings", "settings", async () => await ShowApplicationSettingsAsync()),
                    ReferenceFileButton("Cad.Text.Exit", "退出", "Exit", "exit", () =>
                    {
                        Close();
                        return System.Threading.Tasks.Task.CompletedTask;
                    }))),

            ReferenceRibbonTab(
                "Cad.Text.Modeling",
                "建模",
                "Model",
                ReferenceRibbonGroup(
                    "Cad.Text.Draw",
                    "二维绘制",
                    "2D Draw",
                    ReferenceActionButton("draw.point"),
                    ReferenceActionButton("draw.line"),
                    ReferenceActionMenu("Cad.Text.Circle", "圆", "Circle", "draw.circle.centerradius",
                        "draw.circle.centerradius", "draw.circle.centerdiameter", "draw.circle.twopoints", "draw.circle.threepoints", "draw.circle.pointcenter"),
                    ReferenceActionMenu("Cad.Text.ArcFamily", "圆弧", "Arc", "draw.arc.threepoints",
                        "draw.arc.threepoints", "draw.arc.centerstartend", "draw.arc.startcenterend", "draw.arc.startendcenter", "draw.arc.startendpoint", "draw.arc.startendtangent"),
                    ReferenceActionButton("draw.polyline"),
                    ReferenceActionButton("draw.rectangle"),
                    ReferenceActionButton("draw.spline")),
                ReferenceRibbonGroup(
                    "Cad.Text.Modeling",
                    "三维创建",
                    "3D Create",
                    ReferenceActionButton("feature.extrude"),
                    ReferenceActionButton("feature.revolve"),
                    ReferenceActionButton("feature.sweep"),
                    ReferenceActionButton("feature.loft"),
                    ReferenceActionButton("solid.box"),
                    ReferenceActionButton("solid.cylinder"),
                    ReferenceActionButton("solid.sphere"),
                    ReferenceActionButton("solid.torus")),
                ReferenceRibbonGroup(
                    "Cad.Text.Layer",
                    "图层",
                    "Layer",
                    ReferenceRibbonLabel("Cad.Text.CurrentLayer", "当前图层", "Current Layer"),
                    _layerCombo,
                    ReferenceUtilityButton("Cad.Text.Layers", "图层管理", "Layer Manager", "layers", () => SetLayerPanelVisible(true)))),

            ReferenceRibbonTab(
                "Cad.Text.Modify",
                "修改",
                "Modify",
                ReferenceRibbonGroup(
                    "Cad.Text.Transform",
                    "变换",
                    "Transform",
                    ReferenceActionButton("modify.move"),
                    ReferenceActionButton("modify.copy"),
                    ReferenceActionButton("modify.rotate"),
                    ReferenceActionButton("modify.scale"),
                    ReferenceActionButton("modify.array"),
                    ReferenceActionButton("modify.mirror")),
                ReferenceRibbonGroup(
                    "Cad.Text.Edit",
                    "编辑",
                    "Edit",
                    ReferenceActionButton("modify.offset"),
                    ReferenceActionButton("modify.trim"),
                    ReferenceActionButton("modify.extend"),
                    ReferenceActionButton("modify.chamfer"),
                    ReferenceActionButton("modify.fillet"),
                    ReferenceActionButton("edit.delete"))),

            ReferenceRibbonTab(
                "Cad.Text.Annotate",
                "标注",
                "Annotate",
                ReferenceRibbonGroup(
                    "Cad.Text.Annotation",
                    "标注",
                    "Annotation",
                    ReferenceActionButton("annotate.text"),
                    ReferenceActionButton("annotate.length"),
                    ReferenceActionButton("annotate.angle"),
                    ReferenceActionButton("annotate.radius"),
                    ReferenceActionButton("annotate.diameter")),
                ReferenceRibbonGroup(
                    "Cad.Text.Measure",
                    "测量",
                    "Measure",
                    ReferenceActionButton("measure.distance"))),

            ReferenceRibbonTab(
                "Cad.Text.View",
                "视图",
                "View",
                ReferenceRibbonGroup(
                    "Cad.Text.StandardViews",
                    "标准视图",
                    "Standard Views",
                    ReferenceActionButton("view.fit"),
                    ReferenceActionButton("view.isometric"),
                    ReferenceActionButton("view.top"),
                    ReferenceActionButton("view.front"),
                    ReferenceActionButton("view.right"),
                    ReferenceActionButton("view.left")),
                ReferenceRibbonGroup(
                    "Cad.Text.Display",
                    "显示",
                    "Display",
                    ReferenceActionButton("display.wireframe"),
                    ReferenceActionButton("display.shaded"),
                    ReferenceActionButton("display.transparent"),
                    ReferenceActionButton("display.hiddenline"),
                    ReferenceActionButton("view.hide"),
                    ReferenceActionButton("view.isolate"),
                    ReferenceActionButton("view.showall"))),

            ReferenceRibbonTab(
                "Cad.Text.Tools",
                "工具",
                "Tools",
                ReferenceRibbonGroup(
                    "Cad.Text.Selection",
                    "选择",
                    "Selection",
                    ReferenceActionButton("select"),
                    ReferenceActionButton("select.all"),
                    ReferenceActionButton("select.invert")),
                ReferenceRibbonGroup(
                    "Cad.Text.WorkPlane",
                    "工作平面",
                    "Work Plane",
                    ReferenceWorkPlaneButton("XY", CadWorkPlanePreset.XY),
                    ReferenceWorkPlaneButton("YZ", CadWorkPlanePreset.YZ),
                    ReferenceWorkPlaneButton("XZ", CadWorkPlanePreset.XZ)),
                ReferenceRibbonGroup(
                    "Cad.Text.Panels",
                    "面板",
                    "Panels",
                    ReferenceUtilityButton("Cad.Text.Model", "模型浏览器", "Model Browser", "model", () => SetModelPanelVisible(true)),
                    ReferenceUtilityButton("Cad.Text.Properties", "属性", "Properties", "properties", () => SetPropertyPanelVisible(true)),
                    ReferenceUtilityButton("Cad.Text.Layers", "图层", "Layers", "layers", () => SetLayerPanelVisible(true)),
                    ReferenceUtilityButton("Cad.Text.ToolParameters", "工具参数", "Tool Parameters", "tool", () => SetToolParameterPanelVisible(true)))),

            ReferenceRibbonTab(
                "Cad.Text.Settings",
                "设置",
                "Settings",
                ReferenceRibbonGroup(
                    "Cad.Text.Settings",
                    "首选项",
                    "Preferences",
                    ReferenceFileButton("Cad.Text.Preferences", "应用设置", "Application Settings", "settings", async () => await ShowApplicationSettingsAsync()),
                    ReferenceUtilityButton("Cad.Text.Chinese", "中文", "Chinese", "language", () => CadLanguageManager.Apply("zh-CN")),
                    ReferenceUtilityButton("Cad.Text.English", "English", "English", "language", () => CadLanguageManager.Apply("en-US")))),

            ReferenceRibbonTab(
                "Cad.Text.Help",
                "帮助",
                "Help",
                ReferenceRibbonGroup(
                    "Cad.Text.Help",
                    "帮助",
                    "Help",
                    ReferenceUtilityButton("Cad.Text.Help", "快捷键与命令", "Commands & Shortcuts", "select", () =>
                        _commandLine.ShowFeedback(ReferenceHelpText())),
                    ReferenceUtilityButton("Cad.Text.About", "关于 OCCAD", "About OCCAD", "solid.box", () =>
                        _commandLine.ShowFeedback("OCCAD · OCCT 7.9.0"))))
        };

        tabs.SelectedIndex = Math.Clamp(_referenceSelectedRibbonIndex, 0, 7);
        tabs.SelectionChanged += (_, _) =>
        {
            if (tabs.SelectedIndex >= 0)
                _referenceSelectedRibbonIndex = tabs.SelectedIndex;
        };
        _referenceRibbonTabs = tabs;
        return tabs;
    }

    private TabItem ReferenceRibbonTab(
        string resourceKey,
        string chineseFallback,
        string englishFallback,
        params Control[] groups)
    {
        var groupStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            Margin = new Thickness(5, 2, 5, 2),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        foreach (var group in groups)
            groupStrip.Children.Add(group);

        var scroll = new ScrollViewer
        {
            Content = groupStrip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = ReferenceRibbonBodyBrush
        };

        var tab = new TabItem
        {
            Header = ReferenceLocalized(resourceKey, chineseFallback, englishFallback),
            Content = scroll,
            MinHeight = 30,
            Padding = new Thickness(13, 4)
        };
        tab.Classes.Add("occad-reference-ribbon-tab");
        return tab;
    }

    private Border ReferenceRibbonGroup(
        string resourceKey,
        string chineseFallback,
        string englishFallback,
        params Control[] controls)
    {
        var body = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(4, 2, 4, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        foreach (var control in controls)
            body.Children.Add(control);

        var caption = new TextBlock
        {
            Text = ReferenceLocalized(resourceKey, chineseFallback, englishFallback),
            Height = 17,
            FontSize = 9.5,
            Foreground = ReferenceMutedBrush,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0)
        };

        var grid = new Grid
        {
            MinWidth = 70,
            Background = ReferenceRibbonBodyBrush
        };
        grid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.Children.Add(body);
        Grid.SetRow(caption, 1);
        grid.Children.Add(caption);

        return new Border
        {
            Background = ReferenceRibbonBodyBrush,
            BorderBrush = ReferenceBorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Margin = new Thickness(0, 0, 2, 0),
            Child = grid
        };
    }

    private Button ReferenceActionButton(string id)
    {
        var descriptor = _workspace.Actions.Describe(id);
        var button = ReferenceRibbonButton(
            descriptor is null
                ? id
                : CadLanguageManager.Text(descriptor.DisplayNameKey, descriptor.EnglishName),
            id);
        button.Tag = id;
        button.IsVisible = descriptor is not null;
        button.Click += (_, _) =>
        {
            ExecuteAction(id);
            RefreshReferenceCommandState();
            _viewport.Focus();
        };

        if (!_referenceActionButtons.TryGetValue(id, out var buttons))
        {
            buttons = [];
            _referenceActionButtons.Add(id, buttons);
        }
        buttons.Add(button);
        _referenceKnownActions.Add(id);
        return button;
    }

    private Button ReferenceActionMenu(
        string resourceKey,
        string chineseFallback,
        string englishFallback,
        string iconKey,
        params string[] actionIds)
    {
        var button = ReferenceRibbonButton(
            ReferenceLocalized(resourceKey, chineseFallback, englishFallback) + " ▾",
            iconKey);

        var menuItems = new List<MenuItem>();
        foreach (var id in actionIds)
        {
            _referenceKnownActions.Add(id);
            var descriptor = _workspace.Actions.Describe(id);
            if (descriptor is null)
                continue;

            var item = new MenuItem
            {
                Header = CadLanguageManager.Text(descriptor.DisplayNameKey, descriptor.EnglishName),
                Tag = id
            };
            item.Click += (_, _) =>
            {
                ExecuteAction(id);
                RefreshReferenceCommandState();
                _viewport.Focus();
            };
            menuItems.Add(item);
        }

        var menu = new ContextMenu
        {
            ItemsSource = menuItems
        };
        menu.Opening += (_, _) =>
        {
            foreach (var item in menuItems)
            {
                if (item.Tag is string id)
                    item.IsEnabled = _workspace.Actions.Find(id)?.CanExecute() == true;
            }
        };
        button.ContextMenu = menu;
        button.Click += (_, _) => menu.Open(button);
        _referenceActionMenus.Add(new ReferenceActionMenuBinding(button, actionIds));
        return button;
    }

    private Button ReferenceFileButton(
        string resourceKey,
        string chineseFallback,
        string englishFallback,
        string iconKey,
        Func<System.Threading.Tasks.Task> action)
    {
        var button = ReferenceRibbonButton(
            ReferenceLocalized(resourceKey, chineseFallback, englishFallback),
            iconKey);
        button.Click += async (_, _) => await action();
        return button;
    }

    private Button ReferenceUtilityButton(
        string resourceKey,
        string chineseFallback,
        string englishFallback,
        string iconKey,
        Action action)
    {
        var button = ReferenceRibbonButton(
            ReferenceLocalized(resourceKey, chineseFallback, englishFallback),
            iconKey);
        button.Click += (_, _) => action();
        return button;
    }

    private Button ReferenceWorkPlaneButton(string text, CadWorkPlanePreset preset)
    {
        var button = ReferenceRibbonButton(text, "view.top");
        button.MinWidth = 54;
        button.Click += (_, _) =>
        {
            if (!_workspace.Tools.TryChangeDrawingPlane(preset))
            {
                _commandLine.ShowFeedback(
                    ReferenceLocalized(
                        "Cad.Text.WorkPlaneChangeBlocked",
                        "请先结束当前工具，再切换工作平面。",
                        "Finish the active tool before changing the work plane."));
            }
            RefreshWorkPlaneUi();
            _viewport.Focus();
        };
        return button;
    }

    private Button ReferenceRibbonButton(string text, string iconKey)
    {
        var icon = CadRibbonIcons.CreateIcon(iconKey);
        if (icon is not null)
        {
            icon.Width = 23;
            icon.Height = 23;
            icon.Margin = new Thickness(0, 2, 0, 2);
        }

        var label = new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            Foreground = ReferenceTextBrush,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 76
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (icon is not null)
            content.Children.Add(icon);
        else
            content.Children.Add(new Border { Height = 27 });
        content.Children.Add(label);

        var button = new Button
        {
            Content = content,
            MinWidth = 62,
            Height = 64,
            Padding = new Thickness(5, 1),
            Margin = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("occad-reference-ribbon-button");
        ToolTip.SetTip(button, text);
        return button;
    }

    private Button ReferenceHeaderButton(
        string resourceKey,
        string chineseFallback,
        string englishFallback,
        string iconKey)
    {
        var icon = CadRibbonIcons.CreateIcon(iconKey);
        if (icon is not null)
        {
            icon.Width = 16;
            icon.Height = 16;
        }

        var button = new Button
        {
            Width = 30,
            Height = 28,
            Padding = new Thickness(0),
            Content = icon,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("occad-reference-header-button");
        ToolTip.SetTip(button, ReferenceLocalized(resourceKey, chineseFallback, englishFallback));
        return button;
    }

    private TextBlock ReferenceRibbonLabel(
        string resourceKey,
        string chineseFallback,
        string englishFallback) =>
        new()
        {
            Text = ReferenceLocalized(resourceKey, chineseFallback, englishFallback),
            FontSize = 10.0,
            Foreground = ReferenceMutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0)
        };

    private void ApplyReferencePanelLayout()
    {
        _modelColumn.Width = new GridLength(250);
        _rightColumn.Width = new GridLength(336);

        _modelPanel.Background = ReferenceSurfaceBrush;
        _modelPanel.BorderBrush = ReferenceBorderBrush;
        _rightPanel.Background = ReferenceSurfaceBrush;
        _rightPanel.BorderBrush = ReferenceBorderBrush;
        _layerPanelBorder.Background = ReferenceSurfaceBrush;
        _propertyPanelBorder.Background = ReferenceSurfaceBrush;

        _modelSearch.Margin = new Thickness(7, 7, 7, 5);
        _modelSearch.Background = ReferenceSurfaceBrush;
        _modelSearch.BorderBrush = ReferenceBorderBrush;
        _modelTree.Margin = new Thickness(6, 0, 6, 6);
        _modelTree.Background = ReferenceSurfaceBrush;

        _modelHeaderText!.Text = ReferenceLocalized("Cad.Text.ModelBrowser", "模型浏览器", "Model Browser");
        _modelHeaderText.FontSize = 11.5;
        _layerHeaderText!.FontSize = 11.5;
        _propertyHeaderText!.FontSize = 11.5;

        ApplyReferencePanelHeader(_modelPanel);
        ApplyReferencePanelHeader(_layerPanelBorder);
        ApplyReferencePanelHeader(_propertyPanelBorder);

        if (_rightPanel.Child is Grid rightGrid)
        {
            rightGrid.Children.Remove(_layerPanelBorder);
            rightGrid.Children.Remove(_propertyPanelBorder);

            rightGrid.RowDefinitions.Clear();
            rightGrid.RowDefinitions.Add(_propertyPanelRow);
            rightGrid.RowDefinitions.Add(_rightPanelSplitterRow);
            rightGrid.RowDefinitions.Add(_layerPanelRow);

            _propertyPanelRow.Height = new GridLength(0.55, GridUnitType.Star);
            _layerPanelRow.Height = new GridLength(0.45, GridUnitType.Star);

            Grid.SetRow(_propertyPanelBorder, 0);
            rightGrid.Children.Add(_propertyPanelBorder);
            Grid.SetRow(_layerPanelBorder, 2);
            rightGrid.Children.Add(_layerPanelBorder);
        }
    }

    private static void ApplyReferencePanelHeader(Border panel)
    {
        if (panel.Child is not Grid grid || grid.Children.Count == 0)
            return;

        if (grid.Children[0] is Border headerBorder)
        {
            headerBorder.BorderBrush = ReferenceBorderBrush;
            if (headerBorder.Child is DockPanel headerPanel)
                headerPanel.Background = ReferencePanelHeaderBrush;
        }
    }

    private void ApplyReferenceViewportFrame()
    {
        if (_referenceViewportFrame is not null || _viewportHost.Parent is not Grid workspaceGrid)
            return;

        var viewportColumn = Grid.GetColumn(_viewportHost);
        workspaceGrid.Children.Remove(_viewportHost);

        var documentStrip = BuildReferenceDocumentStrip();
        var frame = new Grid
        {
            Background = ReferenceBorderBrush
        };
        frame.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        frame.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        frame.Children.Add(documentStrip);
        Grid.SetRow(_viewportHost, 1);
        frame.Children.Add(_viewportHost);
        Grid.SetColumn(frame, viewportColumn);
        workspaceGrid.Children.Add(frame);
        _referenceViewportFrame = frame;
    }

    private Control BuildReferenceDocumentStrip()
    {
        var modelTab = new Border
        {
            Height = 29,
            MinWidth = 96,
            Padding = new Thickness(12, 0),
            Background = ReferenceAccentSoftBrush,
            BorderBrush = ReferenceAccentBrush,
            BorderThickness = new Thickness(0, 0, 0, 2),
            Child = new TextBlock
            {
                Text = ReferenceLocalized("Cad.Text.Model", "模型", "Model"),
                Foreground = ReferenceAccentBrush,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };

        var newButton = new Button
        {
            Content = "+",
            Width = 31,
            Height = 27,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 1),
            FontSize = 17,
            Foreground = ReferenceMutedBrush
        };
        newButton.Classes.Add("occad-reference-document-button");
        newButton.Click += async (_, _) => await NewDocumentAsync();
        ToolTip.SetTip(newButton, ReferenceLocalized("Cad.Text.New", "新建", "New"));

        var tabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 0
        };
        tabs.Children.Add(modelTab);
        tabs.Children.Add(newButton);

        return new Border
        {
            Height = 30,
            Background = ReferencePanelBrush,
            BorderBrush = ReferenceBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = tabs
        };
    }

    private void AddReferenceViewportToolbar()
    {
        if (_referenceViewportToolbar is not null)
            return;

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 1,
            Margin = new Thickness(0)
        };
        foreach (var id in new[] { "view.fit", "view.isometric", "view.top", "view.front", "view.right" })
            stack.Children.Add(ReferenceViewportToolButton(id));

        _referenceViewportToolbar = new Border
        {
            Background = ReferenceViewportToolbarBrush,
            BorderBrush = ReferenceBrush("#5D6A75"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = stack
        };
        _viewportHost.Children.Add(_referenceViewportToolbar);
    }

    private Button ReferenceViewportToolButton(string id)
    {
        var descriptor = _workspace.Actions.Describe(id);
        var icon = CadRibbonIcons.CreateIcon(id);
        if (icon is not null)
        {
            icon.Width = 16;
            icon.Height = 16;
        }

        var button = new Button
        {
            Width = 29,
            Height = 29,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = icon
        };
        button.Classes.Add("occad-reference-viewport-button");
        button.Click += (_, _) =>
        {
            ExecuteAction(id);
            _viewport.Focus();
        };
        if (descriptor is not null)
            ToolTip.SetTip(button, CadLanguageManager.Text(descriptor.DisplayNameKey, descriptor.EnglishName));
        return button;
    }

    private void EnableReferenceViewCube()
    {
        if (_workspace.Engine is { IsInitialized: true } currentEngine)
            currentEngine.SetViewCubeVisible(true);

        _viewport.EngineRecreated += (_, args) =>
            Ui(() => args.Engine.SetViewCubeVisible(true));
    }

    private void RefreshReferenceCommandState()
    {
        foreach (var pair in _referenceActionButtons)
        {
            var enabled = _workspace.Actions.Find(pair.Key)?.CanExecute() == true;
            foreach (var button in pair.Value)
                button.IsEnabled = enabled;
        }

        foreach (var menu in _referenceActionMenus)
        {
            var enabled = false;
            foreach (var id in menu.ActionIds)
            {
                if (_workspace.Actions.Find(id)?.CanExecute() == true)
                {
                    enabled = true;
                    break;
                }
            }
            menu.Button.IsEnabled = enabled;
        }
    }

    private void ReferenceSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox search)
            return;

        var query = search.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        foreach (var id in _referenceKnownActions)
        {
            var descriptor = _workspace.Actions.Describe(id);
            if (descriptor is null)
                continue;

            var localized = CadLanguageManager.Text(descriptor.DisplayNameKey, descriptor.EnglishName);
            if (!string.Equals(query, id, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(query, descriptor.EnglishName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(query, localized, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_workspace.Actions.Find(id)?.CanExecute() == true)
            {
                ExecuteAction(id);
                search.Text = string.Empty;
                _viewport.Focus();
            }
            else
            {
                _commandLine.ShowFeedback(
                    ReferenceLocalized(
                        "Cad.Text.ActionUnavailable",
                        "该命令在当前状态下不可用。",
                        "The command is not available in the current state."));
            }

            e.Handled = true;
            return;
        }

        _commandLine.ShowFeedback(
            ReferenceLocalized(
                "Cad.Text.CommandNotFound",
                $"未找到命令：{query}",
                $"Command not found: {query}"));
        e.Handled = true;
    }

    private void ReferenceShellKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Q && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            _referenceCommandSearch?.Focus();
            e.Handled = true;
        }
    }

    private void ReferenceLanguageChanged(object? sender, EventArgs e) =>
        Ui(() =>
        {
            _referenceSelectedRibbonIndex = _referenceRibbonTabs?.SelectedIndex ?? _referenceSelectedRibbonIndex;
            if (_ribbonHost is not null)
                _ribbonHost.Child = BuildReferenceRibbon();

            if (_referenceBrandSubtitle is not null)
                _referenceBrandSubtitle.Text = ReferenceBrandSubtitle();
            if (_referenceCommandSearch is not null)
                _referenceCommandSearch.PlaceholderText = ReferenceSearchPlaceholder();
            if (_modelHeaderText is not null)
                _modelHeaderText.Text = ReferenceLocalized("Cad.Text.ModelBrowser", "模型浏览器", "Model Browser");

            RefreshReferenceCommandState();
        });

    private void ReferenceShellClosed(object? sender, EventArgs e)
    {
        CadLanguageManager.Changed -= ReferenceLanguageChanged;
        KeyDown -= ReferenceShellKeyDown;
        Closed -= ReferenceShellClosed;
        if (_referenceCommandSearch is not null)
            _referenceCommandSearch.KeyDown -= ReferenceSearchKeyDown;
    }

    private static string ReferenceBrandSubtitle() =>
        string.Equals(CadLanguageManager.CurrentLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? "工业设计专业 CAD"
            : "Professional CAD for Industrial Design";

    private static string ReferenceSearchPlaceholder() =>
        string.Equals(CadLanguageManager.CurrentLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? "搜索命令 (Ctrl+Q)"
            : "Search commands (Ctrl+Q)";

    private static string ReferenceHelpText() =>
        string.Equals(CadLanguageManager.CurrentLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? "OCCAD：可通过 Ribbon、命令行和快捷键执行命令；Ctrl+Q 聚焦命令搜索。"
            : "OCCAD: use the Ribbon, command line, and shortcuts; Ctrl+Q focuses command search.";

    private static string ReferenceLocalized(
        string resourceKey,
        string chineseFallback,
        string englishFallback) =>
        string.Equals(CadLanguageManager.CurrentLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? CadLanguageManager.Text(resourceKey, chineseFallback)
            : CadLanguageManager.Text(resourceKey, englishFallback);

    private static IBrush ReferenceBrush(string value) =>
        new SolidColorBrush(Color.Parse(value));

    private sealed record ReferenceActionMenuBinding(
        Button Button,
        IReadOnlyList<string> ActionIds);
}

internal static class CadReferenceVisualTheme
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied || Application.Current is not { } app)
            return;

        _applied = true;
        var accent = new SolidColorBrush(Color.Parse("#1774C7"));
        var accentSoft = new SolidColorBrush(Color.Parse("#E0EEF9"));
        var border = new SolidColorBrush(Color.Parse("#C8D0D8"));
        var text = new SolidColorBrush(Color.Parse("#26323D"));
        var hover = new SolidColorBrush(Color.Parse("#E7F1FA"));
        var surface = new SolidColorBrush(Color.Parse("#F8FAFC"));

        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-ribbon-button"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, Brushes.Transparent),
                new Setter(Button.BorderBrushProperty, Brushes.Transparent),
                new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                new Setter(Button.ForegroundProperty, text),
                new Setter(Button.CornerRadiusProperty, new CornerRadius(4))
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-ribbon-button").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, hover),
                new Setter(Button.BorderBrushProperty, accentSoft)
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-ribbon-button").Class(":pressed"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, accentSoft),
                new Setter(Button.BorderBrushProperty, accent)
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-ribbon-button").Class(":disabled"))
        {
            Setters =
            {
                new Setter(Button.OpacityProperty, 0.42)
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-header-button"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, Brushes.Transparent),
                new Setter(Button.BorderBrushProperty, Brushes.Transparent),
                new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                new Setter(Button.CornerRadiusProperty, new CornerRadius(4))
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-header-button").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, hover),
                new Setter(Button.BorderBrushProperty, border)
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-document-button"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, Brushes.Transparent),
                new Setter(Button.BorderBrushProperty, Brushes.Transparent),
                new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                new Setter(Button.CornerRadiusProperty, new CornerRadius(3))
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-document-button").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, hover)
            }
        });
        app.Styles.Add(new Style(x => x.OfType<Button>().Class("occad-reference-viewport-button").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.Parse("#53616D")))
            }
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class("occad-reference-ribbon-tab"))
        {
            Setters =
            {
                new Setter(TabItem.ForegroundProperty, text),
                new Setter(TabItem.FontSizeProperty, 11.5),
                new Setter(TabItem.FontWeightProperty, FontWeight.Medium),
                new Setter(TabItem.BackgroundProperty, surface)
            }
        });
        app.Styles.Add(new Style(x => x.OfType<TabItem>().Class("occad-reference-ribbon-tab").Class(":selected"))
        {
            Setters =
            {
                new Setter(TabItem.ForegroundProperty, accent),
                new Setter(TabItem.FontWeightProperty, FontWeight.SemiBold),
                new Setter(TabItem.BackgroundProperty, surface)
            }
        });
    }
}
