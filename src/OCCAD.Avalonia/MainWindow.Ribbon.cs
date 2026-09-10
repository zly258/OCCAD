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
    private readonly Dictionary<string, ToggleButton> _ribbonPanelToggles =
        new(StringComparer.OrdinalIgnoreCase);
    private Border? _ribbonHost;
    private bool _ribbonApplied;

    internal bool IsRibbonApplied => _ribbonApplied;

    internal void ApplyRibbon()
    {
        if (_ribbonApplied || Content is not DockPanel root)
            return;

        var legacyToolbar = root.Children
            .OfType<Border>()
            .FirstOrDefault(border =>
                border.Child is StackPanel panel &&
                panel.Children.Contains(_snapToggle));

        // Layer selection belongs to Home. Drafting toggles intentionally stay
        // attached to the legacy toolbar until CommandStatus moves them to the
        // status bar; this prevents Ribbon language rebuilds from stealing the
        // persistent SNAP/ORTHO/POLAR controls back from the status surface.
        DetachRibbonControl(_layerCombo);

        root.Children.Remove(_mainMenu);
        ReleaseLegacyMenuState();
        if (legacyToolbar is not null)
            root.Children.Remove(legacyToolbar);

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
        _ribbonPanelToggles.Clear();
        _ribbonHost.Child = BuildRibbonContent();
        RefreshRibbonActionUi();
        RefreshRibbonPanelState();
    }

    private Control BuildRibbonContent()
    {
        var tabs = new TabControl
        {
            Background = CadTheme.Toolbar,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinHeight = 94,
            MaxHeight = 112
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
                    RibbonAction("file.clear", "Cad.Text.ClearModel", "Clear Model"),
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
                    RibbonAction("edit.undo", "Cad.Text.Undo", "Undo"),
                    RibbonAction("edit.redo", "Cad.Text.Redo", "Redo"),
                    RibbonAction("select", "Cad.Text.Select", "Select"),
                    RibbonAction("select.all", "Cad.Text.SelectAll", "Select All"),
                    RibbonAction("select.invert", "Cad.Text.SelectInvert", "Invert"),
                    RibbonAction("edit.delete", "Cad.Text.Delete", "Delete")),
                RibbonGroup(
                    "Cad.Text.Layer",
                    "Layer",
                    RibbonLabel("Cad.Text.CurrentLayer", "Current Layer"),
                    _layerCombo)),

            RibbonTab(
                "Cad.Text.Draw",
                "Draw",
                RibbonGroup(
                    "Cad.Text.Basic",
                    "Basic",
                    RibbonAction("draw.point", "Cad.Text.Point", "Point"),
                    RibbonAction("draw.line", "Cad.Text.Line", "Line"),
                    RibbonAction("draw.polyline", "Cad.Text.Polyline", "Polyline"),
                    RibbonAction("draw.rectangle", "Cad.Text.Rectangle", "Rectangle"),
                    RibbonAction("draw.polygon", "Cad.Text.Polygon", "Polygon"),
                    RibbonAction("draw.spline", "Cad.Text.Spline", "Spline")),
                RibbonGroup(
                    "Cad.Text.Circle",
                    "Circle",
                    RibbonAction("draw.circle.centerradius", "Cad.Parameter.circle.Method.CenterRadius", "Center + Radius"),
                    RibbonAction("draw.circle.centerdiameter", "Cad.Parameter.circle.Method.CenterDiameter", "Center + Diameter"),
                    RibbonAction("draw.circle.twopoints", "Cad.Parameter.circle.Method.TwoPoints", "Two Points"),
                    RibbonAction("draw.circle.threepoints", "Cad.Parameter.circle.Method.ThreePoints", "Three Points"),
                    RibbonAction("draw.circle.pointcenter", "Cad.Parameter.circle.Method.PointCenter", "Point + Center")),
                RibbonGroup(
                    "Cad.Text.Arc",
                    "Arc",
                    RibbonAction("draw.arc.threepoints", "Cad.Parameter.arc.Method.ThreePoints", "Three Points"),
                    RibbonAction("draw.arc.centerstartend", "Cad.Parameter.arc.Method.CenterStartEnd", "Center + Start + End"),
                    RibbonAction("draw.arc.startcenterend", "Cad.Parameter.arc.Method.StartCenterEnd", "Start + Center + End"),
                    RibbonAction("draw.arc.startendcenter", "Cad.Parameter.arc.Method.StartEndCenter", "Start + End + Center"),
                    RibbonAction("draw.arc.startendpoint", "Cad.Parameter.arc.Method.StartEndPoint", "Start + End + Point"),
                    RibbonAction("draw.arc.startendtangent", "Cad.Parameter.arc.Method.StartEndTangent", "Start + End + Tangent")),
                RibbonGroup(
                    "Cad.Text.Curves",
                    "Curves",
                    RibbonAction("draw.regularpolygon.inscribed", "Cad.Parameter.regularpolygon.mode.Inscribed", "Polygon Inscribed"),
                    RibbonAction("draw.regularpolygon.circumscribed", "Cad.Parameter.regularpolygon.mode.Circumscribed", "Polygon Circumscribed"),
                    RibbonAction("draw.ellipse.centermajor", "Cad.Parameter.ellipse.Method.CenterMajorMinor", "Ellipse Center + Axes"),
                    RibbonAction("draw.ellipse.axisendpoints", "Cad.Parameter.ellipse.Method.AxisEndpointsMinor", "Ellipse Axis Endpoints")),
                RibbonGroup(
                    "Cad.Text.Modeling",
                    "Model",
                    RibbonAction("solid.box", "Cad.Text.Box", "Box"),
                    RibbonAction("solid.cylinder", "Cad.Text.Cylinder", "Cylinder"),
                    RibbonAction("solid.cone", "Cad.Text.Cone", "Cone"),
                    RibbonAction("solid.frustum", "Cad.Text.Frustum", "Frustum"),
                    RibbonAction("solid.sphere", "Cad.Text.Sphere", "Sphere"),
                    RibbonAction("solid.ellipsoid", "Cad.Text.Ellipsoid", "Ellipsoid"),
                    RibbonAction("solid.torus", "Cad.Text.Torus", "Torus"),
                    RibbonAction("curve.helix", "Cad.Text.Helix", "Helix"),
                    RibbonAction("feature.extrude", "Cad.Text.Extrude", "Extrude"))),

            RibbonTab(
                "Cad.Text.Modify",
                "Modify",
                RibbonGroup(
                    "Cad.Text.Transform",
                    "Transform",
                    RibbonAction("modify.move", "Cad.Text.Move", "Move"),
                    RibbonAction("modify.copy", "Cad.Text.Copy", "Copy"),
                    RibbonAction("modify.rotate", "Cad.Text.Rotate", "Rotate"),
                    RibbonAction("modify.scale", "Cad.Text.Scale", "Scale"),
                    RibbonAction("modify.mirror", "Cad.Text.Mirror", "Mirror"))),

            RibbonTab(
                "Cad.Text.Annotate",
                "Annotate",
                RibbonGroup(
                    "Cad.Text.Annotation",
                    "Annotation",
                    RibbonAction("annotate.text", "Cad.Text.Text", "Text"),
                    RibbonAction("annotate.length", "Cad.Text.LengthDimension", "Length Dimension"),
                    RibbonAction("annotate.angle", "Cad.Text.AngleDimension", "Angle Dimension"),
                    RibbonAction("annotate.radius", "Cad.Text.RadiusDimension", "Radius Dimension"),
                    RibbonAction("annotate.diameter", "Cad.Text.DiameterDimension", "Diameter Dimension")),
                RibbonGroup(
                    "Cad.Text.Measure",
                    "Measure",
                    RibbonAction("measure.distance", "Cad.Text.Distance", "Distance"))),

            RibbonTab(
                "Cad.Text.View",
                "View",
                RibbonGroup(
                    "Cad.Text.StandardViews",
                    "Standard Views",
                    RibbonAction("view.fit", "Cad.Text.Fit", "Fit"),
                    RibbonAction("view.isometric", "Cad.Text.Isometric", "Isometric"),
                    RibbonAction("view.top", "Cad.Text.Top", "Top"),
                    RibbonAction("view.bottom", "Cad.Text.Bottom", "Bottom"),
                    RibbonAction("view.front", "Cad.Text.Front", "Front"),
                    RibbonAction("view.back", "Cad.Text.Back", "Back"),
                    RibbonAction("view.left", "Cad.Text.Left", "Left"),
                    RibbonAction("view.right", "Cad.Text.Right", "Right")),
                RibbonGroup(
                    "Cad.Text.Display",
                    "Display",
                    RibbonAction("display.wireframe", "Cad.Text.Wireframe", "Wireframe"),
                    RibbonAction("display.shaded", "Cad.Text.Shaded", "Shaded"),
                    RibbonAction("view.hide", "Cad.Text.Hide", "Hide"),
                    RibbonAction("view.isolate", "Cad.Text.Isolate", "Isolate"),
                    RibbonAction("view.showall", "Cad.Text.ShowAll", "Show All"))),

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
            Spacing = 0,
            Margin = new Thickness(3, 2, 3, 1)
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
            RowSpacing = 1,
            ColumnSpacing = 2,
            Margin = new Thickness(3, 2, 3, 1)
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

        var caption = new TextBlock
        {
            Text = CadLanguageManager.Text(resourceKey, fallback),
            FontSize = CadTheme.CaptionFontSize,
            Foreground = CadTheme.Muted,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 0, 4, 1)
        };

        var panel = new StackPanel { Spacing = 0 };
        panel.Children.Add(body);
        panel.Children.Add(caption);

        return new Border
        {
            Background = CadTheme.Toolbar,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(1, 0),
            Child = panel
        };
    }

    private Button RibbonAction(string id, string resourceKey, string fallback)
    {
        var button = RibbonButton(CadLanguageManager.Text(resourceKey, fallback));
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

    private static Button RibbonButton(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 66,
            Height = 22,
            Padding = new Thickness(6, 0),
            Margin = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("cad-compact");
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
        var button = RibbonButton(CadLanguageManager.Text(resourceKey, fallback));
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
            Content = CadLanguageManager.Text(resourceKey, fallback),
            MinWidth = 72,
            Height = 22,
            Padding = new Thickness(6, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("cad-toggle");
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

    private void ReleaseLegacyMenuState()
    {
        _mainMenu.ItemsSource = null;
        _actionItems.Clear();
        _snapModeItems.Clear();
        _polarItems.Clear();
        _chineseMenu = null;
        _englishMenu = null;
        _modelPanelMenu = null;
        _layerPanelMenu = null;
        _propertyPanelMenu = null;
        _toolPanelMenu = null;
    }

    private static void DetachRibbonControl(Control control)
    {
        if (control.Parent is Panel panel)
            panel.Children.Remove(control);
        else if (control.Parent is ContentControl content && ReferenceEquals(content.Content, control))
            content.Content = null;
    }
}
