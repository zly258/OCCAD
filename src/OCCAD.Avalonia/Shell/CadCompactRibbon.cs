using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Deliberately compact command surface. It follows the OCCTBIM-Source command
/// grouping but exposes only actions that actually exist in OCCAD Core.
/// </summary>
internal sealed class CadCompactRibbon : Border
{
    private readonly CadWorkspace _workspace;
    private readonly Action<string> _feedback;
    private readonly Dictionary<string, Button> _buttons =
        new(StringComparer.OrdinalIgnoreCase);

    public CadCompactRibbon(CadWorkspace workspace, Action<string> feedback)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));

        Background = new SolidColorBrush(Color.Parse("#F7F8FA"));
        BorderBrush = new SolidColorBrush(Color.Parse("#D3D8DE"));
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(4, 0, 4, 2);

        Child = new TabControl
        {
            MinHeight = 78,
            MaxHeight = 86,
            ItemsSource = new[]
            {
                Tab("开始", new[]
                {
                    A("edit.undo", "撤销"),
                    A("edit.redo", "重做"),
                    A("edit.delete", "删除"),
                    A("select.all", "全选"),
                    A("select.invert", "反选")
                }),
                Tab("绘图", new[]
                {
                    A("draw.line", "直线"),
                    A("draw.polyline", "多段线"),
                    A("draw.circle", "圆"),
                    A("draw.arc", "圆弧"),
                    A("draw.ellipse", "椭圆"),
                    A("draw.rectangle", "矩形"),
                    A("draw.polygon", "多边形"),
                    A("draw.regularpolygon", "正多边形"),
                    A("draw.spline", "样条"),
                    A("draw.centerline", "中心线")
                }),
                Tab("三维", new[]
                {
                    A("solid.box", "长方体"),
                    A("solid.cylinder", "圆柱"),
                    A("solid.cone", "圆锥"),
                    A("solid.frustum", "圆台"),
                    A("solid.sphere", "球"),
                    A("solid.ellipsoid", "椭球"),
                    A("solid.torus", "圆环"),
                    A("curve.helix", "螺旋线"),
                    A("feature.extrude", "拉伸"),
                    A("feature.revolve", "旋转"),
                    A("feature.sweep", "扫掠"),
                    A("feature.loft", "放样")
                }),
                Tab("修改", new[]
                {
                    A("modify.move", "移动"),
                    A("modify.copy", "复制"),
                    A("modify.rotate", "旋转"),
                    A("modify.scale", "缩放"),
                    A("modify.mirror", "镜像"),
                    A("modify.array", "阵列"),
                    A("modify.offset", "偏移"),
                    A("modify.trim", "修剪"),
                    A("modify.extend", "延伸"),
                    A("modify.fillet", "圆角"),
                    A("modify.chamfer", "倒角")
                }),
                Tab("标注", new[]
                {
                    A("annotate.text", "文字"),
                    A("annotate.length", "线性"),
                    A("annotate.angle", "角度"),
                    A("annotate.radius", "半径"),
                    A("annotate.diameter", "直径"),
                    A("measure.distance", "测距")
                }),
                Tab("视图", new[]
                {
                    A("view.fit", "适合"),
                    A("view.isometric", "轴测"),
                    A("view.top", "俯视"),
                    A("view.front", "前视"),
                    A("view.right", "右视"),
                    A("display.wireframe", "线框"),
                    A("display.shaded", "着色"),
                    A("display.hiddenline", "隐藏线"),
                    A("view.hide", "隐藏"),
                    A("view.isolate", "隔离"),
                    A("view.showall", "全部显示")
                })
            }
        };

        _workspace.Events.Changed += (_, _) => RefreshState();
        _workspace.Actions.ActionFinished += (_, _) => RefreshState();
        _workspace.Actions.ActionFailed += (_, args) => _feedback(args.Exception.Message);
        RefreshState();
    }

    private TabItem Tab(string title, IReadOnlyList<ActionItem> actions)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 3),
            ItemHeight = 30
        };

        foreach (var item in actions)
        {
            if (_workspace.Actions.Find(item.Id) is null)
                continue;

            var button = new Button
            {
                Content = item.Text,
                MinWidth = 54,
                Height = 28,
                Padding = new Thickness(9, 3),
                Margin = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            ToolTip.SetTip(button, item.Text);
            button.Click += (_, _) => Execute(item.Id, item.Text);
            panel.Children.Add(button);
            _buttons[item.Id] = button;
        }

        return new TabItem
        {
            Header = title,
            Content = panel
        };
    }

    private void Execute(string id, string label)
    {
        if (!_workspace.Actions.Execute(id))
        {
            if (!_workspace.Actions.CanExecute(id))
                _feedback($"当前不能执行：{label}");
            return;
        }

        _feedback(_workspace.Tools.ActiveTool is { } tool
            ? tool.Prompt
            : label);
        RefreshState();
    }

    private void RefreshState()
    {
        foreach (var (id, button) in _buttons)
            button.IsEnabled = _workspace.Actions.CanExecute(id);
    }

    private static ActionItem A(string id, string text) => new(id, text);
    private sealed record ActionItem(string Id, string Text);
}
