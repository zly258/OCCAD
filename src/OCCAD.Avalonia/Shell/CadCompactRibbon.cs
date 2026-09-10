using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Minimal command surface. It contains no CAD state and no duplicate command
/// handlers: every button resolves to a registered Core Action ID.
/// </summary>
internal sealed class CadCompactRibbon : Border
{
    private readonly CadWorkspace _workspace;
    private readonly Action<string> _feedback;

    public CadCompactRibbon(CadWorkspace workspace, Action<string> feedback)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));

        Background = CadUi.Surface;
        BorderBrush = CadUi.Border;
        BorderThickness = new Thickness(0, 0, 0, 1);
        MinHeight = CadUi.RibbonHeight;
        MaxHeight = CadUi.RibbonHeight;
        Child = BuildTabs();
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl
        {
            FontSize = CadUi.UiFontSize,
            ItemsSource = new[]
            {
                Tab("开始",
                    Action("新建", "file.new"),
                    Action("撤销", "edit.undo"),
                    Action("重做", "edit.redo"),
                    Action("删除", "edit.delete"),
                    Action("全选", "select.all"),
                    Action("反选", "select.invert"),
                    Action("测距", "measure.distance")),

                Tab("绘图",
                    Action("点", "draw.point"),
                    Action("直线", "draw.line"),
                    Action("多段线", "draw.polyline"),
                    Action("圆", "draw.circle"),
                    Action("圆弧", "draw.arc"),
                    Action("矩形", "draw.rectangle"),
                    Action("多边形", "draw.polygon"),
                    Action("正多边形", "draw.regularpolygon"),
                    Action("椭圆", "draw.ellipse"),
                    Action("样条", "draw.spline")),

                Tab("三维",
                    Action("长方体", "solid.box"),
                    Action("圆柱", "solid.cylinder"),
                    Action("圆锥", "solid.cone"),
                    Action("球", "solid.sphere"),
                    Action("椭球", "solid.ellipsoid"),
                    Action("圆环", "solid.torus"),
                    Action("拉伸", "feature.extrude"),
                    Action("旋转", "feature.revolve"),
                    Action("扫掠", "feature.sweep"),
                    Action("放样", "feature.loft")),

                Tab("修改",
                    Action("移动", "modify.move"),
                    Action("复制", "modify.copy"),
                    Action("旋转", "modify.rotate"),
                    Action("缩放", "modify.scale"),
                    Action("镜像", "modify.mirror"),
                    Action("阵列", "modify.array"),
                    Action("偏移", "modify.offset"),
                    Action("修剪", "modify.trim"),
                    Action("延伸", "modify.extend"),
                    Action("圆角", "modify.fillet"),
                    Action("倒角", "modify.chamfer")),

                Tab("标注",
                    Action("中心线", "draw.centerline"),
                    Action("文字", "annotate.text"),
                    Action("长度", "annotate.length"),
                    Action("角度", "annotate.angle"),
                    Action("半径", "annotate.radius"),
                    Action("直径", "annotate.diameter")),

                Tab("视图",
                    Action("适合", "view.fit"),
                    Action("等轴测", "view.isometric"),
                    Action("顶", "view.top"),
                    Action("前", "view.front"),
                    Action("右", "view.right"),
                    Action("线框", "display.wireframe"),
                    Action("着色", "display.shaded"),
                    Action("隐藏", "view.hide"),
                    Action("隔离", "view.isolate"),
                    Action("全部", "view.showall"))
            }
        };

        return tabs;
    }

    private TabItem Tab(string title, params RibbonAction[] actions)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 3)
        };

        foreach (var item in actions)
        {
            var action = _workspace.Actions.Find(item.Id);
            if (action is null)
                continue;

            var button = new Button
            {
                Content = item.Text,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Foreground = CadUi.Text
            };
            CadUi.ConfigureCompactButton(button);

            var shortcut = string.IsNullOrWhiteSpace(action.Shortcut)
                ? string.Empty
                : $"  {action.Shortcut}";
            ToolTip.SetTip(button, $"{action.Description}{shortcut}");
            button.Click += (_, _) => Execute(item.Id, item.Text);
            buttons.Children.Add(button);
        }

        return new TabItem
        {
            Header = title,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = buttons
            }
        };
    }

    private void Execute(string id, string text)
    {
        if (_workspace.Actions.Execute(id))
            return;

        _feedback($"当前不能执行：{text}");
    }

    private static RibbonAction Action(string text, string id) =>
        new(text, id);

    private sealed record RibbonAction(string Text, string Id);
}
