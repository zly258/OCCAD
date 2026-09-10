using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, List<Control>> _classicActionControls =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly StackPanel _classicToolOptions = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Margin = new Thickness(6, 3),
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Border _classicToolOptionsSurface = new()
    {
        IsVisible = false
    };

    private StackPanel? _classicShellHost;
    private bool _classicShellApplied;
    private MenuItem? _modelPanelMenuItem;
    private MenuItem? _layersPanelMenuItem;
    private MenuItem? _propertiesPanelMenuItem;
    private MenuItem? _chineseMenuItem;
    private MenuItem? _englishMenuItem;

    internal void ApplyClassicShell()
    {
        if (_classicShellApplied || Content is not DockPanel root)
            return;

        _classicShellApplied = true;
        _classicShellHost = BuildClassicShellHost();
        DockPanel.SetDock(_classicShellHost, Dock.Top);
        root.Children.Insert(0, _classicShellHost);

        DisableViewCube();
        _viewport.EngineRecreated += (_, args) =>
        {
            if (args.Engine.IsInitialized)
                args.Engine.SetViewCubeVisible(false);
        };

        _workspace.Tools.ToolChanged += (_, _) =>
            Ui(() =>
            {
                RefreshClassicShellActions();
                RefreshClassicToolOptions();
                RefreshOperationStatus();
            });
        _workspace.Tools.ToolUpdated += (_, _) =>
            Ui(() =>
            {
                RefreshClassicToolOptions();
                RefreshOperationStatus();
            });
        _workspace.Preselection.Changed += (_, _) => RefreshOperationStatus();

        RefreshClassicShellActions();
        RefreshClassicToolOptions();
        RefreshOperationStatus();
    }

    private StackPanel BuildClassicShellHost()
    {
        var host = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };
        host.Children.Add(BuildClassicMenu());
        host.Children.Add(BuildClassicToolbar());
        host.Children.Add(BuildClassicToolOptionsSurface());
        return host;
    }

    private Menu BuildClassicMenu()
    {
        var file = new MenuItem
        {
            Header = Label("文件", "File"),
            ItemsSource = new object[]
            {
                UtilityMenu(Label("新建", "New"), async () => await NewDocumentAsync()),
                UtilityMenu(Label("打开…", "Open…"), async () => await OpenDocumentAsync()),
                UtilityMenu(Label("保存", "Save"), async () => await SaveDocumentAsync(saveAs: false)),
                UtilityMenu(Label("另存为…", "Save As…"), async () => await SaveDocumentAsync(saveAs: true)),
                new Separator(),
                UtilityMenu(Label("设置…", "Settings…"), async () => await ShowApplicationSettingsAsync()),
                new Separator(),
                UtilityMenu(Label("退出", "Exit"), () =>
                {
                    Close();
                    return Task.CompletedTask;
                })
            }
        };

        var circle = new MenuItem
        {
            Header = Label("圆", "Circle"),
            ItemsSource = new object[]
            {
                ActionMenu("draw.circle.centerradius", Label("圆心-半径", "Center-Radius")),
                ActionMenu("draw.circle.centerdiameter", Label("圆心-直径", "Center-Diameter")),
                ActionMenu("draw.circle.twopoints", Label("两点", "Two Points")),
                ActionMenu("draw.circle.threepoints", Label("三点", "Three Points")),
                ActionMenu("draw.circle.pointcenter", Label("点-圆心", "Point-Center"))
            }
        };

        var arc = new MenuItem
        {
            Header = Label("圆弧", "Arc"),
            ItemsSource = new object[]
            {
                ActionMenu("draw.arc.threepoints", Label("三点", "Three Points")),
                ActionMenu("draw.arc.centerstartend", Label("圆心-起点-终点", "Center-Start-End")),
                ActionMenu("draw.arc.startcenterend", Label("起点-圆心-终点", "Start-Center-End")),
                ActionMenu("draw.arc.startendcenter", Label("起点-终点-圆心", "Start-End-Center")),
                ActionMenu("draw.arc.startendpoint", Label("起点-终点-点", "Start-End-Point")),
                ActionMenu("draw.arc.startendtangent", Label("起点-终点-切线", "Start-End-Tangent"))
            }
        };

        var ellipse = new MenuItem
        {
            Header = Label("椭圆", "Ellipse"),
            ItemsSource = new object[]
            {
                ActionMenu("draw.ellipse.centermajor", Label("中心-轴", "Center-Axes")),
                ActionMenu("draw.ellipse.axisendpoints", Label("轴端点", "Axis Endpoints"))
            }
        };

        var regularPolygon = new MenuItem
        {
            Header = Label("正多边形", "Regular Polygon"),
            ItemsSource = new object[]
            {
                ActionMenu("draw.regularpolygon.inscribed", Label("内接", "Inscribed")),
                ActionMenu("draw.regularpolygon.circumscribed", Label("外切", "Circumscribed"))
            }
        };

        var draw = new MenuItem
        {
            Header = Label("绘图", "Draw"),
            ItemsSource = new object[]
            {
                ActionMenu("draw.point", Label("点", "Point")),
                ActionMenu("draw.line", Label("直线", "Line")),
                ActionMenu("draw.polyline", Label("多段线", "Polyline")),
                ActionMenu("draw.polygon", Label("自由多边形", "Free Polygon")),
                regularPolygon,
                ActionMenu("draw.rectangle", Label("矩形", "Rectangle")),
                circle,
                arc,
                ellipse,
                ActionMenu("draw.spline", Label("样条", "Spline"))
            }
        };

        var model = new MenuItem
        {
            Header = Label("建模", "Model"),
            ItemsSource = new object[]
            {
                ActionMenu("solid.box", Label("长方体", "Box")),
                ActionMenu("solid.cylinder", Label("圆柱体", "Cylinder")),
                ActionMenu("solid.cone", Label("圆锥体", "Cone")),
                ActionMenu("solid.frustum", Label("圆台", "Frustum")),
                ActionMenu("solid.sphere", Label("球体", "Sphere")),
                ActionMenu("solid.ellipsoid", Label("椭球", "Ellipsoid")),
                ActionMenu("solid.torus", Label("圆环体", "Torus")),
                ActionMenu("curve.helix", Label("螺旋线", "Helix")),
                new Separator(),
                ActionMenu("feature.extrude", Label("拉伸", "Extrude")),
                ActionMenu("feature.revolve", Label("旋转", "Revolve")),
                ActionMenu("feature.sweep", Label("扫掠", "Sweep")),
                ActionMenu("feature.loft", Label("放样", "Loft"))
            }
        };

        var isometric = new MenuItem
        {
            Header = Label("轴测", "Isometric"),
            ItemsSource = new object[]
            {
                ActionMenu("view.iso.ne", "NE"),
                ActionMenu("view.iso.nw", "NW"),
                ActionMenu("view.iso.se", "SE"),
                ActionMenu("view.iso.sw", "SW")
            }
        };

        var view = new MenuItem
        {
            Header = Label("视图", "View"),
            ItemsSource = new object[]
            {
                ActionMenu("view.fit", Label("适合窗口", "Fit")),
                new Separator(),
                ActionMenu("view.top", Label("上", "Top")),
                ActionMenu("view.bottom", Label("下", "Bottom")),
                ActionMenu("view.front", Label("前", "Front")),
                ActionMenu("view.back", Label("后", "Back")),
                ActionMenu("view.left", Label("左", "Left")),
                ActionMenu("view.right", Label("右", "Right")),
                isometric,
                new Separator(),
                ActionMenu("display.wireframe", Label("线框", "Wireframe")),
                ActionMenu("display.shaded", Label("着色", "Shaded"))
            }
        };

        _modelPanelMenuItem = PanelMenuItem(Label("模型树", "Model Tree"),
            () => SetModelPanelVisible(!_modelPanel.IsVisible));
        _layersPanelMenuItem = PanelMenuItem(Label("图层", "Layers"),
            () => SetLayerPanelVisible(!_layerPanelBorder.IsVisible));
        _propertiesPanelMenuItem = PanelMenuItem(Label("属性", "Properties"),
            () => SetPropertyPanelVisible(!_propertyPanelBorder.IsVisible));

        var panels = new MenuItem
        {
            Header = Label("窗口", "Window"),
            ItemsSource = new object[]
            {
                _modelPanelMenuItem,
                _layersPanelMenuItem,
                _propertiesPanelMenuItem
            }
        };

        _chineseMenuItem = LanguageMenuItem("中文", "zh-CN");
        _englishMenuItem = LanguageMenuItem("English", "en-US");
        var language = new MenuItem
        {
            Header = Label("语言", "Language"),
            ItemsSource = new object[] { _chineseMenuItem, _englishMenuItem }
        };

        var menu = new Menu
        {
            ItemsSource = new object[] { file, draw, model, view, panels, language }
        };
        RefreshClassicPanelState();
        return menu;
    }

    private Control BuildClassicToolbar()
    {
        var strip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(4, 3),
            VerticalAlignment = VerticalAlignment.Center
        };

        strip.Children.Add(UtilityToolbar(Label("新建", "New"), async () => await NewDocumentAsync()));
        strip.Children.Add(UtilityToolbar(Label("打开", "Open"), async () => await OpenDocumentAsync()));
        strip.Children.Add(UtilityToolbar(Label("保存", "Save"), async () => await SaveDocumentAsync(saveAs: false)));

        strip.Children.Add(ActionToolbar("draw.line", Label("直线", "Line")));
        strip.Children.Add(ActionToolbar("draw.polyline", Label("多段线", "Polyline")));
        strip.Children.Add(ActionToolbar("draw.regularpolygon.inscribed", Label("正多边形", "Polygon")));
        strip.Children.Add(ActionToolbar("draw.rectangle", Label("矩形", "Rectangle")));
        strip.Children.Add(ActionToolbar("draw.circle.centerradius", Label("圆", "Circle")));
        strip.Children.Add(ActionToolbar("draw.arc.threepoints", Label("圆弧", "Arc")));

        strip.Children.Add(ActionToolbar("solid.box", Label("长方体", "Box")));
        strip.Children.Add(ActionToolbar("solid.cylinder", Label("圆柱体", "Cylinder")));
        strip.Children.Add(ActionToolbar("feature.extrude", Label("拉伸", "Extrude")));
        strip.Children.Add(ActionToolbar("feature.revolve", Label("旋转", "Revolve")));
        strip.Children.Add(ActionToolbar("view.fit", Label("适合", "Fit")));

        _layerCombo.Width = 150;
        _layerCombo.Margin = new Thickness(4, 0, 0, 0);
        ToolTip.SetTip(_layerCombo, Label("当前图层", "Current Layer"));
        strip.Children.Add(_layerCombo);

        strip.Children.Add(BuildLanguagePicker());

        return new ScrollViewer
        {
            Content = strip,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private Control BuildClassicToolOptionsSurface()
    {
        _classicToolOptionsSurface.Child = new ScrollViewer
        {
            Content = _classicToolOptions,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        return _classicToolOptionsSurface;
    }

    private void RefreshClassicToolOptions()
    {
        _classicToolOptions.Children.Clear();

        var tool = _workspace.Tools.ActiveTool;
        var panel = tool?.ParameterPanel;
        if (tool is null || panel is null || panel.Parameters.Count == 0)
        {
            _classicToolOptionsSurface.IsVisible = false;
            return;
        }

        _classicToolOptionsSurface.IsVisible = true;
        _classicToolOptions.Children.Add(new TextBlock
        {
            Text = CadLanguageManager.Text(tool.LocalizationKey, panel.Title) + ":",
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        foreach (var parameter in panel.Parameters)
        {
            _classicToolOptions.Children.Add(new TextBlock
            {
                Text = ToolParameterLabel(tool, parameter),
                VerticalAlignment = VerticalAlignment.Center
            });
            _classicToolOptions.Children.Add(CreateToolParameterEditor(tool, parameter));
        }
    }

    private Control CreateToolParameterEditor(CadTool tool, CadToolParameterDescriptor parameter)
    {
        switch (parameter)
        {
            case CadBooleanToolParameterDescriptor boolean:
            {
                var check = new CheckBox
                {
                    IsChecked = boolean.Value,
                    VerticalAlignment = VerticalAlignment.Center
                };
                check.IsCheckedChanged += (_, _) =>
                {
                    if (check.IsChecked is not { } value)
                        return;
                    ApplyToolParameter(tool, parameter.Id, value ? "true" : "false");
                };
                return check;
            }

            case CadChoiceToolParameterDescriptor choice:
            {
                var items = choice.Choices
                    .Select(value => new ToolChoice(
                        value,
                        CadLanguageManager.Text(
                            $"Cad.Parameter.{tool.Id}.{parameter.Id}.{value}",
                            value)))
                    .ToArray();
                var combo = new ComboBox
                {
                    MinWidth = 100,
                    ItemsSource = items,
                    SelectedItem = items.FirstOrDefault(item =>
                        string.Equals(item.Value, choice.Value, StringComparison.OrdinalIgnoreCase))
                };
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedItem is ToolChoice selected)
                        ApplyToolParameter(tool, parameter.Id, selected.Value);
                };
                return combo;
            }

            case CadIntegerToolParameterDescriptor integer:
                return ToolParameterTextBox(
                    tool,
                    parameter.Id,
                    integer.Value.ToString(CultureInfo.CurrentCulture),
                    width: 64);

            case CadDoubleToolParameterDescriptor number:
                return ToolParameterTextBox(
                    tool,
                    parameter.Id,
                    number.Value.ToString("0.######", CultureInfo.CurrentCulture),
                    width: 78);

            case CadOptionalDoubleToolParameterDescriptor optional:
                return ToolParameterTextBox(
                    tool,
                    parameter.Id,
                    optional.Value?.ToString("0.######", CultureInfo.CurrentCulture) ?? string.Empty,
                    width: 78);

            case CadStringToolParameterDescriptor text:
                return ToolParameterTextBox(tool, parameter.Id, text.Value, width: 140);

            default:
                return new TextBlock { Text = "—", VerticalAlignment = VerticalAlignment.Center };
        }
    }

    private TextBox ToolParameterTextBox(
        CadTool tool,
        string parameterId,
        string value,
        double width)
    {
        var editor = new TextBox
        {
            Width = width,
            Text = value,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var submitted = value;

        void Commit()
        {
            var text = editor.Text?.Trim() ?? string.Empty;
            if (string.Equals(text, submitted, StringComparison.Ordinal))
                return;
            if (tool.TrySetParameter(parameterId, text))
            {
                submitted = text;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshClassicToolOptions);
                _viewport.Focus();
                return;
            }

            ShowStatusFeedback(Label("参数值无效", "Invalid parameter value"));
            global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshClassicToolOptions);
        }

        editor.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter)
                return;
            Commit();
            args.Handled = true;
        };
        editor.LostFocus += (_, _) => Commit();
        return editor;
    }

    private void ApplyToolParameter(CadTool tool, string id, string value)
    {
        if (!ReferenceEquals(tool, _workspace.Tools.ActiveTool))
            return;

        if (!tool.TrySetParameter(id, value))
            ShowStatusFeedback(Label("参数值无效", "Invalid parameter value"));

        global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshClassicToolOptions);
        _viewport.Focus();
    }

    private static string ToolParameterLabel(CadTool tool, CadToolParameterDescriptor parameter) =>
        CadLanguageManager.Text(
            $"Cad.Parameter.{tool.Id}.{parameter.Id}",
            parameter.Label);

    private MenuItem ActionMenu(string id, string caption)
    {
        var item = new MenuItem { Header = caption };
        var action = _workspace.Actions.Find(id);
        if (action is null)
        {
            item.IsEnabled = false;
            return item;
        }

        item.Click += (_, _) =>
        {
            ExecuteAction(id);
            _viewport.Focus();
        };
        TrackClassicAction(id, item);
        return item;
    }

    private Button ActionToolbar(string id, string caption)
    {
        var button = new Button { Content = caption };
        ToolTip.SetTip(button, caption);
        var action = _workspace.Actions.Find(id);
        if (action is null)
        {
            button.IsEnabled = false;
            return button;
        }

        button.Click += (_, _) =>
        {
            ExecuteAction(id);
            _viewport.Focus();
        };
        TrackClassicAction(id, button);
        return button;
    }

    private static MenuItem UtilityMenu(string caption, Func<Task> action)
    {
        var item = new MenuItem { Header = caption };
        item.Click += async (_, _) => await action();
        return item;
    }

    private Button UtilityToolbar(string caption, Func<Task> action)
    {
        var button = new Button { Content = caption };
        ToolTip.SetTip(button, caption);
        button.Click += async (_, _) =>
        {
            await action();
            _viewport.Focus();
        };
        return button;
    }

    private MenuItem PanelMenuItem(string caption, Action action)
    {
        var item = new MenuItem
        {
            Header = caption,
            ToggleType = MenuItemToggleType.CheckBox
        };
        item.Click += (_, _) =>
        {
            action();
            RefreshClassicPanelState();
        };
        return item;
    }

    private MenuItem LanguageMenuItem(string caption, string language)
    {
        var item = new MenuItem
        {
            Header = caption,
            ToggleType = MenuItemToggleType.Radio,
            GroupName = "UiLanguage"
        };
        item.Click += (_, _) =>
        {
            if (!CadLanguageManager.CurrentLanguage.Equals(language, StringComparison.OrdinalIgnoreCase))
                CadLanguageManager.Apply(language);
            SaveInteractionPreferences();
        };
        return item;
    }

    private ComboBox BuildLanguagePicker()
    {
        var combo = new ComboBox
        {
            Width = 96,
            Margin = new Thickness(4, 0, 0, 0),
            ItemsSource = new[] { "中文", "English" },
            SelectedIndex = CadLanguageManager.CurrentLanguage.Equals(
                "zh-CN", StringComparison.OrdinalIgnoreCase) ? 0 : 1
        };
        ToolTip.SetTip(combo, Label("界面语言", "Language"));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0)
                return;

            var language = combo.SelectedIndex == 0 ? "zh-CN" : "en-US";
            if (CadLanguageManager.CurrentLanguage.Equals(language, StringComparison.OrdinalIgnoreCase))
                return;

            CadLanguageManager.Apply(language);
            SaveInteractionPreferences();
        };
        return combo;
    }

    private void TrackClassicAction(string id, Control control)
    {
        if (!_classicActionControls.TryGetValue(id, out var controls))
        {
            controls = [];
            _classicActionControls[id] = controls;
        }
        controls.Add(control);
    }

    private void RefreshClassicShellActions()
    {
        foreach (var pair in _classicActionControls)
        {
            var enabled = _workspace.Actions.Find(pair.Key)?.CanExecute() == true;
            foreach (var control in pair.Value)
                control.IsEnabled = enabled;
        }

        RefreshClassicPanelState();
    }

    private void RefreshClassicPanelState()
    {
        if (_modelPanelMenuItem is not null)
            _modelPanelMenuItem.IsChecked = _modelPanel.IsVisible;
        if (_layersPanelMenuItem is not null)
            _layersPanelMenuItem.IsChecked = _layerPanelBorder.IsVisible;
        if (_propertiesPanelMenuItem is not null)
            _propertiesPanelMenuItem.IsChecked = _propertyPanelBorder.IsVisible;
        if (_chineseMenuItem is not null)
            _chineseMenuItem.IsChecked = CadLanguageManager.CurrentLanguage.Equals(
                "zh-CN", StringComparison.OrdinalIgnoreCase);
        if (_englishMenuItem is not null)
            _englishMenuItem.IsChecked = CadLanguageManager.CurrentLanguage.Equals(
                "en-US", StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildClassicShell()
    {
        if (!_classicShellApplied || _classicShellHost is null)
            return;

        if (_layerCombo.Parent is Panel layerParent)
            layerParent.Children.Remove(_layerCombo);
        if (_classicToolOptionsSurface.Parent is Panel optionsParent)
            optionsParent.Children.Remove(_classicToolOptionsSurface);

        _classicActionControls.Clear();
        _classicShellHost.Children.Clear();
        _classicShellHost.Children.Add(BuildClassicMenu());
        _classicShellHost.Children.Add(BuildClassicToolbar());
        _classicShellHost.Children.Add(BuildClassicToolOptionsSurface());
        RefreshClassicShellActions();
        RefreshClassicToolOptions();
    }

    private void RebuildCleanToolbar() => RebuildClassicShell();
    private void RefreshCleanShellActions() => RefreshClassicShellActions();

    private static string Label(string chinese, string english) =>
        CadLanguageManager.CurrentLanguage.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
            ? chinese
            : english;

    private void DisableViewCube()
    {
        if (_workspace.Engine is { IsInitialized: true } engine)
            engine.SetViewCubeVisible(false);
    }

    private sealed record ToolChoice(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
