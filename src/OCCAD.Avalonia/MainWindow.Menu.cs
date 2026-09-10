using Avalonia.Controls;
using Avalonia.Input;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private void BuildMenu()
    {
        _actionItems.Clear();
        _snapModeItems.Clear();
        _polarItems.Clear();

        var file = Menu(
            UiText("Cad.Text.File", "File"),
            Plain(UiText("Cad.Text.New", "New"), async () => await NewDocumentAsync()),
            Plain(UiText("Cad.Text.Open", "Open"), async () => await OpenDocumentAsync()),
            MenuSeparator(),
            Plain(UiText("Cad.Text.Save", "Save"), async () => await SaveDocumentAsync(saveAs: false)),
            Plain(UiText("Cad.Text.SaveAs", "Save As"), async () => await SaveDocumentAsync(saveAs: true)),
            MenuSeparator(),
            Plain(UiText("Cad.Text.Exit", "Exit"), Close));

        var edit = Menu(
            UiText("Cad.Text.Edit", "Edit"),
            Action("edit.undo", "Cad.Text.Undo", "Undo"),
            Action("edit.redo", "Cad.Text.Redo", "Redo"),
            MenuSeparator(),
            Action("select", "Cad.Text.Select", "Select"),
            Action("select.all", "Cad.Text.SelectAll", "Select All"),
            Action("select.invert", "Cad.Text.SelectInvert", "Invert Selection"),
            MenuSeparator(),
            Action("edit.delete", "Cad.Text.Delete", "Delete"));

        var modify = Menu(
            UiText("Cad.Text.Modify", "Modify"),
            Action("modify.move", "Cad.Text.Move", "Move"),
            Action("modify.copy", "Cad.Text.Copy", "Copy"),
            Action("modify.rotate", "Cad.Text.Rotate", "Rotate"),
            Action("modify.scale", "Cad.Text.Scale", "Scale"),
            Action("modify.mirror", "Cad.Text.Mirror", "Mirror"));

        var circle = Menu(
            UiText("Cad.Text.Circle", "Circle"),
            Action("draw.circle.centerradius", "Cad.Parameter.circle.Method.CenterRadius", "Center + Radius"),
            Action("draw.circle.centerdiameter", "Cad.Parameter.circle.Method.CenterDiameter", "Center + Diameter"),
            Action("draw.circle.twopoints", "Cad.Parameter.circle.Method.TwoPoints", "Two Points"),
            Action("draw.circle.threepoints", "Cad.Parameter.circle.Method.ThreePoints", "Three Points"),
            Action("draw.circle.pointcenter", "Cad.Parameter.circle.Method.PointCenter", "Point + Center"));

        var arc = Menu(
            UiText("Cad.Text.Arc", "Arc"),
            Action("draw.arc.threepoints", "Cad.Parameter.arc.Method.ThreePoints", "Three Points"),
            Action("draw.arc.centerstartend", "Cad.Parameter.arc.Method.CenterStartEnd", "Center + Start + End"),
            Action("draw.arc.startcenterend", "Cad.Parameter.arc.Method.StartCenterEnd", "Start + Center + End"),
            Action("draw.arc.startendcenter", "Cad.Parameter.arc.Method.StartEndCenter", "Start + End + Center"),
            Action("draw.arc.startendpoint", "Cad.Parameter.arc.Method.StartEndPoint", "Start + End + Point"),
            Action("draw.arc.startendtangent", "Cad.Parameter.arc.Method.StartEndTangent", "Start + End + Tangent"));

        var regularPolygon = Menu(
            UiText("Cad.Text.RegularPolygon", "Regular Polygon"),
            Action("draw.regularpolygon.inscribed", "Cad.Parameter.regularpolygon.mode.Inscribed", "Inscribed"),
            Action("draw.regularpolygon.circumscribed", "Cad.Parameter.regularpolygon.mode.Circumscribed", "Circumscribed"));

        var ellipse = Menu(
            UiText("Cad.Text.Ellipse", "Ellipse"),
            Action("draw.ellipse.centermajor", "Cad.Parameter.ellipse.Method.CenterMajorMinor", "Center + Axes"),
            Action("draw.ellipse.axisendpoints", "Cad.Parameter.ellipse.Method.AxisEndpointsMinor", "Axis Endpoints"));

        var draw = Menu(
            UiText("Cad.Text.Draw", "Draw"),
            Action("draw.point", "Cad.Text.Point", "Point"),
            Action("draw.line", "Cad.Text.Line", "Line"),
            Action("draw.polyline", "Cad.Text.Polyline", "Polyline"),
            MenuSeparator(),
            Action("draw.rectangle", "Cad.Text.Rectangle", "Rectangle"),
            Action("draw.polygon", "Cad.Text.Polygon", "Polygon"),
            regularPolygon,
            MenuSeparator(),
            circle,
            arc,
            ellipse,
            Action("draw.spline", "Cad.Text.Spline", "Spline"));

        var annotate = Menu(
            UiText("Cad.Text.Annotate", "Annotate"),
            Action("annotate.text", "Cad.Text.Text", "Text"),
            MenuSeparator(),
            Action("annotate.length", "Cad.Text.LengthDimension", "Length Dimension"),
            Action("annotate.angle", "Cad.Text.AngleDimension", "Angle Dimension"),
            MenuSeparator(),
            Action("annotate.radius", "Cad.Text.RadiusDimension", "Radius Dimension"),
            Action("annotate.diameter", "Cad.Text.DiameterDimension", "Diameter Dimension"));

        var model = Menu(
            UiText("Cad.Text.Modeling", "Model"),
            Action("solid.box", "Cad.Text.Box", "Box"),
            Action("solid.cylinder", "Cad.Text.Cylinder", "Cylinder"),
            Action("solid.cone", "Cad.Text.Cone", "Cone"),
            Action("solid.frustum", "Cad.Text.Frustum", "Frustum"),
            MenuSeparator(),
            Action("solid.sphere", "Cad.Text.Sphere", "Sphere"),
            Action("solid.ellipsoid", "Cad.Text.Ellipsoid", "Ellipsoid"),
            Action("solid.torus", "Cad.Text.Torus", "Torus"),
            MenuSeparator(),
            Action("curve.helix", "Cad.Text.Helix", "Helix"),
            MenuSeparator(),
            Action("feature.extrude", "Cad.Text.Extrude", "Extrude"));

        var measure = Menu(
            UiText("Cad.Text.Measure", "Measure"),
            Action("measure.distance", "Cad.Text.Distance", "Distance"));

        _modelPanelMenu = CheckItem(
            UiText("Cad.Text.Model", "Model"),
            _modelPanel.IsVisible,
            value => SetModelPanelVisible(value));
        _layerPanelMenu = CheckItem(
            UiText("Cad.Text.Layers", "Layers"),
            _layerPanelBorder.IsVisible,
            SetLayerPanelVisible);
        _propertyPanelMenu = CheckItem(
            UiText("Cad.Text.Properties", "Properties"),
            _propertyPanelBorder.IsVisible,
            SetPropertyPanelVisible);
        _toolPanelMenu = CheckItem(
            UiText("Cad.Text.ToolParameters", "Tool Parameters"),
            IsToolParameterPanelVisible,
            SetToolParameterPanelVisible);

        var panels = Menu(
            UiText("Cad.Text.Panels", "Panels"),
            _modelPanelMenu,
            _layerPanelMenu,
            _propertyPanelMenu,
            MenuSeparator(),
            _toolPanelMenu);

        var view = Menu(
            UiText("Cad.Text.View", "View"),
            Action("view.fit", "Cad.Text.Fit", "Fit"),
            Action("view.isometric", "Cad.Text.Isometric", "Isometric"),
            MenuSeparator(),
            Action("view.top", "Cad.Text.Top", "Top"),
            Action("view.bottom", "Cad.Text.Bottom", "Bottom"),
            Action("view.front", "Cad.Text.Front", "Front"),
            Action("view.back", "Cad.Text.Back", "Back"),
            Action("view.left", "Cad.Text.Left", "Left"),
            Action("view.right", "Cad.Text.Right", "Right"),
            MenuSeparator(),
            Action("display.wireframe", "Cad.Text.Wireframe", "Wireframe"),
            Action("display.shaded", "Cad.Text.Shaded", "Shaded"),
            MenuSeparator(),
            Action("view.hide", "Cad.Text.Hide", "Hide"),
            Action("view.isolate", "Cad.Text.Isolate", "Isolate"),
            Action("view.showall", "Cad.Text.ShowAll", "Show All"),
            MenuSeparator(),
            panels);

        _chineseMenu = RadioItem(
            UiText("Cad.Text.Chinese", "中文"),
            "Language",
            string.Equals(CadLanguageManager.CurrentLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase),
            () => CadLanguageManager.Apply("zh-CN"));
        _englishMenu = RadioItem(
            UiText("Cad.Text.English", "English"),
            "Language",
            string.Equals(CadLanguageManager.CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase),
            () => CadLanguageManager.Apply("en-US"));

        var language = Menu(
            UiText("Cad.Text.Language", "Language"),
            _chineseMenu,
            _englishMenu);

        var snapMenu = Menu(
            UiText("Cad.Text.ObjectSnap", "Object Snap"),
            SnapMode(CadSnapType.Endpoint, "Cad.Text.SnapEndpoint", "Endpoint"),
            SnapMode(CadSnapType.Midpoint, "Cad.Text.SnapMidpoint", "Midpoint"),
            SnapMode(CadSnapType.Center, "Cad.Text.SnapCenter", "Center"),
            SnapMode(CadSnapType.Vertex, "Cad.Text.SnapVertex", "Vertex"),
            SnapMode(CadSnapType.Intersection, "Cad.Text.SnapIntersection", "Intersection"),
            SnapMode(CadSnapType.Perpendicular, "Cad.Text.SnapPerpendicular", "Perpendicular"),
            SnapMode(CadSnapType.Nearest, "Cad.Text.SnapNearest", "Nearest"),
            SnapMode(CadSnapType.Tangent, "Cad.Text.SnapTangent", "Tangent"));

        var polar = Menu(
            UiText("Cad.Text.PolarIncrement", "Polar Increment"),
            PolarItem(15),
            PolarItem(30),
            PolarItem(45),
            PolarItem(90));

        var settings = Menu(
            UiText("Cad.Text.Settings", "Settings"),
            Plain(
                UiText("Cad.Text.Preferences", "Preferences..."),
                async () => await ShowApplicationSettingsAsync()),
            MenuSeparator(),
            language,
            MenuSeparator(),
            snapMenu,
            polar);

        _mainMenu.ItemsSource =
            new object[]
            {
                file,
                edit,
                draw,
                modify,
                annotate,
                model,
                measure,
                view,
                settings
            };

        RefreshActionUi();
        RefreshInteractionUi();
        RefreshPanelMenuState();
    }

    private MenuItem Action(string id, string resourceKey, string fallback)
    {
        if (_workspace.Actions.Find(id) is null)
        {
            return new MenuItem
            {
                Header = CadLanguageManager.Text(resourceKey, fallback),
                IsVisible = false,
                IsEnabled = false
            };
        }

        var item = new MenuItem
        {
            Header = CadLanguageManager.Text(resourceKey, fallback),
            Tag = id
        };
        item.Click += (_, _) => ExecuteAction(id);

        if (!_actionItems.TryGetValue(id, out var items))
        {
            items = [];
            _actionItems.Add(id, items);
        }
        items.Add(item);
        return item;
    }

    private void ExecuteAction(string id)
    {
        var action = _workspace.Actions.Find(id);
        if (action is null)
            return;

        if (!action.CanExecute())
        {
            _toolStatus.Text = UiFormat(
                "Cad.Text.ActionUnavailable",
                "{0} is not available in the current state.",
                CadLanguageManager.Text(
                    $"Cad.Text.{action.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)}",
                    action.DisplayName));
            RefreshActionUi();
            return;
        }

        _workspace.Actions.Execute(id);
        RefreshActionUi();
    }

    private MenuItem SnapMode(CadSnapType mode, string resourceKey, string fallback)
    {
        var item = new MenuItem
        {
            Header = CadLanguageManager.Text(resourceKey, fallback),
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = (_workspace.Snap.Modes & mode) != 0
        };
        item.Click += (_, _) =>
        {
            if (_refreshingUi)
                return;

            if (item.IsChecked)
                _workspace.Snap.Modes |= mode;
            else
                _workspace.Snap.Modes &= ~mode;

            _workspace.Snap.Clear();
            RefreshInteractionUi();
            SaveInteractionPreferences();
        };
        _snapModeItems[mode] = item;
        return item;
    }

    private MenuItem PolarItem(double increment)
    {
        var item = new MenuItem
        {
            Header = CadLanguageManager.Text($"Cad.Text.Polar{increment:0}", $"{increment:0}°"),
            ToggleType = MenuItemToggleType.Radio,
            GroupName = "PolarIncrement",
            IsChecked = Math.Abs(_workspace.Drafting.PolarIncrementDegrees - increment) <= 1e-12
        };
        item.Click += (_, _) =>
        {
            if (_refreshingUi)
                return;

            _workspace.Drafting.PolarIncrementDegrees = increment;
            _workspace.Tracking.Clear();
            RefreshInteractionUi();
            SaveInteractionPreferences();
        };
        _polarItems[increment] = item;
        return item;
    }

    private static global::Avalonia.Controls.Separator MenuSeparator() =>
        new()
        {
            Margin = new global::Avalonia.Thickness(6, 1),
            Background = CadTheme.Border
        };

    private static MenuItem Menu(string header, params object[] children) =>
        new()
        {
            Header = header,
            ItemsSource = children
        };

    private static MenuItem Plain(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem Plain(string header, Func<Task> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) => await action();
        return item;
    }

    private static MenuItem CheckItem(string header, bool value, Action<bool> changed)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = value
        };
        item.Click += (_, _) => changed(item.IsChecked);
        return item;
    }

    private static MenuItem RadioItem(string header, string group, bool value, Action clicked)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            GroupName = group,
            IsChecked = value
        };
        item.Click += (_, _) => clicked();
        return item;
    }

    private void RefreshActionUi()
    {
        foreach (var pair in _actionItems)
        {
            var enabled = _workspace.Actions.Find(pair.Key)?.CanExecute() == true;
            foreach (var item in pair.Value)
                item.IsEnabled = enabled;
        }
    }

    private void RefreshPanelMenuState()
    {
        if (_modelPanelMenu is not null)
            _modelPanelMenu.IsChecked = _modelPanel.IsVisible;
        if (_layerPanelMenu is not null)
            _layerPanelMenu.IsChecked = _layerPanelBorder.IsVisible;
        if (_propertyPanelMenu is not null)
            _propertyPanelMenu.IsChecked = _propertyPanelBorder.IsVisible;
        if (_toolPanelMenu is not null)
        {
            _toolPanelMenu.IsEnabled = CanShowToolParameterPanel;
            _toolPanelMenu.IsChecked = IsToolParameterPanelVisible;
        }
    }

    private void MainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox)
            return;

        var modifiers = e.KeyModifiers;

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            if (e.Key == Key.N)
            {
                _ = NewDocumentAsync();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.O)
            {
                _ = OpenDocumentAsync();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.S)
            {
                _ = SaveDocumentAsync(saveAs: (modifiers & KeyModifiers.Shift) != 0);
                e.Handled = true;
                return;
            }
        }

        if (e.Key is Key.Enter or Key.Space &&
            _workspace.Tools.ActiveTool is null &&
            _workspace.Actions.ExecuteLast())
        {
            e.Handled = true;
            return;
        }

        // Active-tool Enter/Space/Escape/Backspace are intentionally not
        // duplicated here. OcctAvaloniaViewport.PreviewKeyInput owns that
        // route and dispatches it once through CadToolManager.HandleKey().
        if (_workspace.Tools.ActiveTool is not null)
            return;

        var shortcut = ShortcutText(e.Key, modifiers);
        if (shortcut is null)
            return;

        if (_workspace.Actions.ExecuteShortcut(shortcut))
            e.Handled = true;
    }

    private static string? ShortcutText(Key key, KeyModifiers modifiers)
    {
        if (key == Key.None)
            return null;

        var parts = new List<string>(4);
        if ((modifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((modifiers & KeyModifiers.Meta) != 0) parts.Add("Meta");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
