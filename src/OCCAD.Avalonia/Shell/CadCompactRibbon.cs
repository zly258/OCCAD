using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using OCCAD;

namespace OCCAD.Avalonia;

internal sealed record CadShellCommand(
    string Text,
    Func<Task> ExecuteAsync,
    string? Description = null);

/// <summary>
/// Minimal command surface. CAD commands resolve to registered Core Action IDs;
/// only platform document lifecycle commands are injected from the shell.
/// </summary>
internal sealed class CadCompactRibbon : Border
{
    private readonly CadWorkspace _workspace;
    private readonly Action<string> _feedback;
    private readonly IReadOnlyList<CadShellCommand> _shellCommands;

    public CadCompactRibbon(
        CadWorkspace workspace,
        Action<string> feedback,
        IReadOnlyList<CadShellCommand>? shellCommands = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        _shellCommands = shellCommands ?? Array.Empty<CadShellCommand>();

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
                StartTab(),

                Tab("绘图",
                    Action("点", "draw.point"),
                    Action("直线", "draw.line"),
                    Action("中心线", "draw.centerline"),
                    Action("多段线", "draw.polyline"),
                    Action("圆", "draw.circle"),
                    Action("圆弧", "draw.arc"),
                    Action("矩形", "draw.rectangle"),
                    Action("多边形", "draw.polygon"),
                    Action("正多边形", "draw.regularpolygon"),
                    Action("椭圆", "draw.ellipse"),
                    Action("样条", "draw.spline")),

                Tab("建模",
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

    private TabItem StartTab()
    {
        var buttons = CreateButtonPanel();

        foreach (var command in _shellCommands)
        {
            var button = CreateButton(command.Text);
            if (!string.IsNullOrWhiteSpace(command.Description))
                ToolTip.SetTip(button, command.Description);
            button.Click += async (_, _) => await ExecuteShellAsync(command);
            buttons.Children.Add(button);
        }

        AddActionButton(buttons, Action("撤销", "edit.undo"));
        AddActionButton(buttons, Action("重做", "edit.redo"));
        AddActionButton(buttons, Action("删除", "edit.delete"));
        AddActionButton(buttons, Action("全选", "select.all"));
        AddActionButton(buttons, Action("反选", "select.invert"));
        AddActionButton(buttons, Action("测距", "measure.distance"));

        return CreateTab("开始", buttons);
    }

    private TabItem Tab(string title, params RibbonAction[] actions)
    {
        var buttons = CreateButtonPanel();
        foreach (var item in actions)
            AddActionButton(buttons, item);
        return CreateTab(title, buttons);
    }

    private void AddActionButton(Panel buttons, RibbonAction item)
    {
        var action = _workspace.Actions.Find(item.Id);
        if (action is null)
            return;

        var button = CreateButton(item.Text);
        var shortcut = string.IsNullOrWhiteSpace(action.Shortcut)
            ? string.Empty
            : $"  {action.Shortcut}";
        ToolTip.SetTip(button, $"{action.Description}{shortcut}");
        button.Click += (_, _) => Execute(item.Id, item.Text);
        buttons.Children.Add(button);
    }

    private static StackPanel CreateButtonPanel() =>
        new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 3)
        };

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Content = text,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Foreground = CadUi.Text
        };
        CadUi.ConfigureCompactButton(button);
        return button;
    }

    private static TabItem CreateTab(string title, Control content) =>
        new()
        {
            Header = title,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = content
            }
        };

    private async Task ExecuteShellAsync(CadShellCommand command)
    {
        try
        {
            await command.ExecuteAsync();
        }
        catch (Exception exception)
        {
            _feedback(exception.Message);
        }
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
