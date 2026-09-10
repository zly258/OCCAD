using Avalonia;
using Avalonia.Controls;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private bool _ribbonShellApplied;
    private CadRibbon? _ribbon;

    internal void ApplyRibbonShell()
    {
        if (_ribbonShellApplied)
            return;

        if (Content is not DockPanel root)
        {
            CadDiagnostics.Trace(
                "Ribbon shell was not applied because MainWindow content is not a DockPanel.");
            return;
        }

        _ribbonShellApplied = true;

        // Keep the old menu/toolbar implementation alive as a migration
        // compatibility layer, but remove it from the visual tree. Existing
        // commands, shortcuts and action registration therefore remain intact.
        root.Children.Remove(_mainMenu);

        var legacyToolbar = _layerCombo.Parent as StackPanel;
        if (legacyToolbar?.Parent is Border legacyToolbarBorder)
            root.Children.Remove(legacyToolbarBorder);

        _ribbon = new CadRibbon(BuildRibbonTabs(), selectedIndex: 1);
        DockPanel.SetDock(_ribbon, Dock.Top);
        root.Children.Insert(0, _ribbon);

        CadLanguageManager.Changed += RibbonLanguageChanged;
        Closed += RibbonClosed;

        _workspace.Actions.ActionFinished += (_, _) => RefreshRibbonState();
        _workspace.Selection.Changed += (_, _) => RefreshRibbonState();
        _workspace.Tools.ToolChanged += (_, _) => RefreshRibbonState();
        _workspace.Tools.ToolUpdated += (_, _) => RefreshRibbonState();
        _workspace.History.Changed += (_, _) => RefreshRibbonState();
        _workspace.Layers.Changed += (_, _) => RefreshRibbonState();
        _toolPanel.PanelVisibilityChanged += (_, _) => RefreshRibbonState();

        _ribbon.RefreshState();
        CadDiagnostics.Trace("Custom CAD ribbon shell applied.");
    }

    private void RibbonLanguageChanged(object? sender, EventArgs e) =>
        Ui(RebuildRibbon);

    private void RibbonClosed(object? sender, EventArgs e)
    {
        CadLanguageManager.Changed -= RibbonLanguageChanged;
        Closed -= RibbonClosed;
    }

    private void RebuildRibbon()
    {
        if (_ribbon is null)
            return;

        var selectedIndex = _ribbon.SelectedIndex;
        _ribbon.SetTabs(BuildRibbonTabs(), selectedIndex);
    }

    private void RefreshRibbonState() =>
        Ui(() => _ribbon?.RefreshState());

    private IReadOnlyList<CadRibbonTabDefinition> BuildRibbonTabs() =>
    [
        RibbonTab(
            "Cad.Text.File",
            "文件",
            "File",
            RibbonGroup(
                "Cad.Text.File",
                "文件",
                "File",
                RibbonCommand("Cad.Text.New", "新建", "New", () => _ = NewDocumentAsync(), "Ctrl+N"),
                RibbonCommand("Cad.Text.Open", "打开", "Open", () => _ = OpenDocumentAsync(), "Ctrl+O"),
                RibbonCommand("Cad.Text.Save", "保存", "Save", () => _ = SaveDocumentAsync(saveAs: false), "Ctrl+S"),
                RibbonCommand("Cad.Text.SaveAs", "另存为", "Save As", () => _ = SaveDocumentAsync(saveAs: true)),
                RibbonCommand("Cad.Text.Exit", "退出", "Exit", Close))),

        RibbonTab(
            "Cad.Text.Home",
            "主页",
            "Home",
            RibbonGroup(
                "Cad.Text.Edit",
                "编辑",
                "Edit",
                RibbonAction("edit.undo", "Cad.Text.Undo", "撤销", "Undo"),
                RibbonAction("edit.redo", "Cad.Text.Redo", "重做", "Redo"),
                RibbonAction("edit.delete", "Cad.Text.Delete", "删除", "Delete")),
            RibbonGroup(
                "Cad.Text.Select",
                "选择",
                "Select",
                RibbonAction("select", "Cad.Text.Select", "选择", "Select"),
                RibbonAction("select.all", "Cad.Text.SelectAll", "全选", "Select All"),
                RibbonAction("select.invert", "Cad.Text.SelectInvert", "反选", "Invert")),
            RibbonGroup(
                "Cad.Text.Modify",
                "修改",
                "Modify",
                RibbonAction("modify.move", "Cad.Text.Move", "移动", "Move"),
                RibbonAction("modify.copy", "Cad.Text.Copy", "复制", "Copy"),
                RibbonAction("modify.rotate", "Cad.Text.Rotate", "旋转", "Rotate"),
                RibbonAction("modify.scale", "Cad.Text.Scale", "缩放", "Scale"),
                RibbonAction("modify.mirror", "Cad.Text.Mirror", "镜像", "Mirror")),
            RibbonGroup(
                "Cad.Text.Display",
                "显示",
                "Display",
                RibbonAction("view.fit", "Cad.Text.Fit", "适合窗口", "Fit"),
                RibbonToggle("Cad.Text.Properties", "属性", "Properties", () => _propertyPanelBorder.IsVisible, () => SetPropertyPanelVisible(!_propertyPanelBorder.IsVisible)),
                RibbonToggle("Cad.Text.Layers", "图层", "Layers", () => _layerPanelBorder.IsVisible, () => SetLayerPanelVisible(!_layerPanelBorder.IsVisible)))),

        RibbonTab(
            "Cad.Text.Draw",
            "绘图",
            "Draw",
            RibbonGroup(
                "Cad.Text.Basic",
                "基本绘图",
                "Basic",
                RibbonAction("draw.point", "Cad.Text.Point", "点", "Point"),
                RibbonAction("draw.line", "Cad.Text.Line", "直线", "Line"),
                RibbonAction("draw.polyline", "Cad.Text.Polyline", "多段线", "Polyline"),
                RibbonAction("draw.rectangle", "Cad.Text.Rectangle", "矩形", "Rectangle"),
                RibbonAction("draw.polygon", "Cad.Text.Polygon", "多边形", "Polygon"),
                RibbonAction("draw.spline", "Cad.Text.Spline", "样条曲线", "Spline")),
            RibbonGroup(
                "Cad.Text.Circle",
                "圆 / 椭圆",
                "Circle / Ellipse",
                RibbonAction("draw.circle.centerradius", "Cad.Parameter.circle.Method.CenterRadius", "圆心-半径", "Center-Radius"),
                RibbonAction("draw.circle.centerdiameter", "Cad.Parameter.circle.Method.CenterDiameter", "圆心-直径", "Center-Diameter"),
                RibbonAction("draw.circle.twopoints", "Cad.Parameter.circle.Method.TwoPoints", "两点圆", "Two Points"),
                RibbonAction("draw.circle.threepoints", "Cad.Parameter.circle.Method.ThreePoints", "三点圆", "Three Points"),
                RibbonAction("draw.circle.pointcenter", "Cad.Parameter.circle.Method.PointCenter", "点-圆心", "Point-Center"),
                RibbonAction("draw.ellipse.centermajor", "Cad.Parameter.ellipse.Method.CenterMajorMinor", "中心-轴", "Center-Axes"),
                RibbonAction("draw.ellipse.axisendpoints", "Cad.Parameter.ellipse.Method.AxisEndpointsMinor", "轴端点", "Axis Endpoints")),
            RibbonGroup(
                "Cad.Text.Arc",
                "圆弧",
                "Arc",
                RibbonAction("draw.arc.threepoints", "Cad.Parameter.arc.Method.ThreePoints", "三点圆弧", "Three Points"),
                RibbonAction("draw.arc.centerstartend", "Cad.Parameter.arc.Method.CenterStartEnd", "圆心-起点-终点", "Center-Start-End"),
                RibbonAction("draw.arc.startcenterend", "Cad.Parameter.arc.Method.StartCenterEnd", "起点-圆心-终点", "Start-Center-End"),
                RibbonAction("draw.arc.startendcenter", "Cad.Parameter.arc.Method.StartEndCenter", "起点-终点-圆心", "Start-End-Center"),
                RibbonAction("draw.arc.startendpoint", "Cad.Parameter.arc.Method.StartEndPoint", "起点-终点-过点", "Start-End-Point"),
                RibbonAction("draw.arc.startendtangent", "Cad.Parameter.arc.Method.StartEndTangent", "起点-终点-切向", "Start-End-Tangent")),
            RibbonGroup(
                "Cad.Text.RegularPolygon",
                "正多边形",
                "Regular Polygon",
                RibbonAction("draw.regularpolygon.inscribed", "Cad.Parameter.regularpolygon.mode.Inscribed", "内接", "Inscribed"),
                RibbonAction("draw.regularpolygon.circumscribed", "Cad.Parameter.regularpolygon.mode.Circumscribed", "外切", "Circumscribed"))),

        RibbonTab(
            "Cad.Text.Modify",
            "修改",
            "Modify",
            RibbonGroup(
                "Cad.Text.Transform",
                "变换",
                "Transform",
                RibbonAction("modify.move", "Cad.Text.Move", "移动", "Move"),
                RibbonAction("modify.copy", "Cad.Text.Copy", "复制", "Copy"),
                RibbonAction("modify.rotate", "Cad.Text.Rotate", "旋转", "Rotate"),
                RibbonAction("modify.scale", "Cad.Text.Scale", "缩放", "Scale"),
                RibbonAction("modify.mirror", "Cad.Text.Mirror", "镜像", "Mirror")),
            RibbonGroup(
                "Cad.Text.Selection",
                "选择",
                "Selection",
                RibbonAction("select", "Cad.Text.Select", "选择", "Select"),
                RibbonAction("select.all", "Cad.Text.SelectAll", "全选", "Select All"),
                RibbonAction("edit.delete", "Cad.Text.Delete", "删除", "Delete"))),

        RibbonTab(
            "Cad.Text.Annotate",
            "标注",
            "Annotate",
            RibbonGroup(
                "Cad.Text.Annotation",
                "注释",
                "Annotation",
                RibbonAction("annotate.text", "Cad.Text.Text", "文字", "Text"),
                RibbonAction("annotate.length", "Cad.Text.LengthDimension", "长度标注", "Length Dimension"),
                RibbonAction("annotate.angle", "Cad.Text.AngleDimension", "角度标注", "Angle Dimension"),
                RibbonAction("annotate.radius", "Cad.Text.RadiusDimension", "半径标注", "Radius Dimension"),
                RibbonAction("annotate.diameter", "Cad.Text.DiameterDimension", "直径标注", "Diameter Dimension")),
            RibbonGroup(
                "Cad.Text.Measure",
                "测量",
                "Measure",
                RibbonAction("measure.distance", "Cad.Text.Distance", "距离", "Distance"))),

        RibbonTab(
            "Cad.Text.Modeling",
            "建模",
            "Model",
            RibbonGroup(
                "Cad.Text.Primitives",
                "基本实体",
                "Primitives",
                RibbonAction("solid.box", "Cad.Text.Box", "长方体", "Box"),
                RibbonAction("solid.cylinder", "Cad.Text.Cylinder", "圆柱体", "Cylinder"),
                RibbonAction("solid.cone", "Cad.Text.Cone", "圆锥体", "Cone"),
                RibbonAction("solid.frustum", "Cad.Text.Frustum", "圆台", "Frustum"),
                RibbonAction("solid.sphere", "Cad.Text.Sphere", "球体", "Sphere"),
                RibbonAction("solid.ellipsoid", "Cad.Text.Ellipsoid", "椭球体", "Ellipsoid"),
                RibbonAction("solid.torus", "Cad.Text.Torus", "圆环体", "Torus")),
            RibbonGroup(
                "Cad.Text.Features",
                "曲线 / 特征",
                "Curve / Feature",
                RibbonAction("curve.helix", "Cad.Text.Helix", "螺旋线", "Helix"),
                RibbonAction("feature.extrude", "Cad.Text.Extrude", "拉伸", "Extrude"))),

        RibbonTab(
            "Cad.Text.View",
            "视图",
            "View",
            RibbonGroup(
                "Cad.Text.StandardViews",
                "标准视图",
                "Standard Views",
                RibbonAction("view.fit", "Cad.Text.Fit", "适合窗口", "Fit"),
                RibbonAction("view.isometric", "Cad.Text.Isometric", "轴测", "Isometric"),
                RibbonAction("view.top", "Cad.Text.Top", "顶", "Top"),
                RibbonAction("view.bottom", "Cad.Text.Bottom", "底", "Bottom"),
                RibbonAction("view.front", "Cad.Text.Front", "前", "Front"),
                RibbonAction("view.back", "Cad.Text.Back", "后", "Back"),
                RibbonAction("view.left", "Cad.Text.Left", "左", "Left"),
                RibbonAction("view.right", "Cad.Text.Right", "右", "Right")),
            RibbonGroup(
                "Cad.Text.Display",
                "显示模式",
                "Display",
                RibbonAction("display.wireframe", "Cad.Text.Wireframe", "线框", "Wireframe"),
                RibbonAction("display.shaded", "Cad.Text.Shaded", "着色", "Shaded"),
                RibbonAction("view.hide", "Cad.Text.Hide", "隐藏", "Hide"),
                RibbonAction("view.isolate", "Cad.Text.Isolate", "隔离", "Isolate"),
                RibbonAction("view.showall", "Cad.Text.ShowAll", "全部显示", "Show All"))),

        RibbonTab(
            "Cad.Text.Manage",
            "管理",
            "Manage",
            RibbonGroup(
                "Cad.Text.Panels",
                "面板",
                "Panels",
                RibbonToggle("Cad.Text.Model", "模型树", "Model", () => _modelPanel.IsVisible, () => SetModelPanelVisible(!_modelPanel.IsVisible)),
                RibbonToggle("Cad.Text.Layers", "图层", "Layers", () => _layerPanelBorder.IsVisible, () => SetLayerPanelVisible(!_layerPanelBorder.IsVisible)),
                RibbonToggle("Cad.Text.Properties", "属性", "Properties", () => _propertyPanelBorder.IsVisible, () => SetPropertyPanelVisible(!_propertyPanelBorder.IsVisible)),
                RibbonToggle("Cad.Text.ToolParameters", "工具参数", "Tool Parameters", () => _toolPanel.IsPanelVisible, ToggleToolPanel, () => _toolPanel.CanDisplayCurrentTool)),
            RibbonGroup(
                "Cad.Text.Drafting",
                "绘图辅助",
                "Drafting",
                RibbonToggle("Cad.Text.ObjectSnap", "对象捕捉", "Object Snap", () => _workspace.Snap.Enabled, ToggleSnap),
                RibbonToggle("Cad.Text.Ortho", "正交", "Ortho", () => _workspace.Drafting.OrthogonalTrackingEnabled, ToggleOrtho),
                RibbonToggle("Cad.Text.Polar", "极轴", "Polar", () => _workspace.Drafting.PolarTrackingEnabled, TogglePolar)),
            RibbonGroup(
                "Cad.Text.Language",
                "语言 / 设置",
                "Language / Settings",
                RibbonToggle("Cad.Text.Chinese", "中文", "Chinese", () => string.Equals(CadLanguageManager.CurrentLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase), () => CadLanguageManager.Apply("zh-CN")),
                RibbonToggle("Cad.Text.English", "英文", "English", () => string.Equals(CadLanguageManager.CurrentLanguage, "en-US", StringComparison.OrdinalIgnoreCase), () => CadLanguageManager.Apply("en-US")),
                RibbonCommand("Cad.Text.Preferences", "设置", "Preferences", () => _ = ShowApplicationSettingsAsync())))
    ];

    private CadRibbonTabDefinition RibbonTab(
        string resourceKey,
        string chinese,
        string english,
        params CadRibbonGroupDefinition[] groups) =>
        new(RibbonText(resourceKey, chinese, english), groups);

    private CadRibbonGroupDefinition RibbonGroup(
        string resourceKey,
        string chinese,
        string english,
        params CadRibbonItemDefinition[] items) =>
        new(RibbonText(resourceKey, chinese, english), items);

    private CadRibbonItemDefinition RibbonAction(
        string id,
        string resourceKey,
        string chinese,
        string english) =>
        new(
            RibbonText(resourceKey, chinese, english),
            () => ExecuteAction(id),
            () => _workspace.Actions.Find(id)?.CanExecute() == true);

    private CadRibbonItemDefinition RibbonCommand(
        string resourceKey,
        string chinese,
        string english,
        Action execute,
        string? toolTip = null) =>
        new(
            RibbonText(resourceKey, chinese, english),
            execute,
            ToolTip: toolTip);

    private CadRibbonItemDefinition RibbonToggle(
        string resourceKey,
        string chinese,
        string english,
        Func<bool> isChecked,
        Action execute,
        Func<bool>? canExecute = null) =>
        new(
            RibbonText(resourceKey, chinese, english),
            execute,
            canExecute,
            isChecked);

    private string RibbonText(
        string resourceKey,
        string chinese,
        string english)
    {
        var fallback = CadLanguageManager.CurrentLanguage.StartsWith(
            "zh",
            StringComparison.OrdinalIgnoreCase)
            ? chinese
            : english;
        return CadLanguageManager.Text(resourceKey, fallback);
    }

    private void ToggleToolPanel()
    {
        if (_toolPanel.IsPanelVisible)
            _toolPanel.HidePanel();
        else
            _toolPanel.ShowPanel();
    }

    private void ToggleSnap()
    {
        _workspace.Snap.Enabled = !_workspace.Snap.Enabled;
        if (!_workspace.Snap.Enabled)
            _workspace.Snap.Clear();
        RefreshInteractionUi();
        SaveInteractionPreferences();
    }

    private void ToggleOrtho()
    {
        _workspace.Drafting.OrthogonalTrackingEnabled =
            !_workspace.Drafting.OrthogonalTrackingEnabled;
        _workspace.Tracking.Clear();
        RefreshInteractionUi();
        SaveInteractionPreferences();
    }

    private void TogglePolar()
    {
        _workspace.Drafting.PolarTrackingEnabled =
            !_workspace.Drafting.PolarTrackingEnabled;
        _workspace.Tracking.Clear();
        RefreshInteractionUi();
        SaveInteractionPreferences();
    }
}
