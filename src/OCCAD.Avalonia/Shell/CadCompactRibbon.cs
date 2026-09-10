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
/// Thin action-driven Ribbon adapter. Layout follows ModelScript's compact
/// three-row industrial Ribbon; every CAD command resolves through Core's
/// authoritative <see cref="CadActionManager"/>.
///
/// The shell intentionally exposes only the current common 2D/3D and edit
/// surface. It must not advertise dormant experimental implementations.
/// </summary>
internal sealed class CadCompactRibbon : Border
{
    private readonly CadWorkspace _workspace;
    private readonly Action<string> _feedback;
    private readonly IReadOnlyList<CadShellCommand> _shellCommands;
    private readonly List<(Button Button, string ActionId)> _actionButtons = [];

    public CadCompactRibbon(
        CadWorkspace workspace,
        Action<string> feedback,
        IReadOnlyList<CadShellCommand>? shellCommands = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        _shellCommands = shellCommands ?? Array.Empty<CadShellCommand>();

        MinHeight = CadUi.RibbonHeight;
        MaxHeight = CadUi.RibbonHeight;
        Child = BuildRibbon();

        _workspace.Events.Changed += (_, _) => RefreshCanExecute();
        _workspace.Actions.ActionFinished += (_, _) => RefreshCanExecute();
        RefreshCanExecute();
    }

    private Control BuildRibbon()
    {
        var tabs = new TabControl
        {
            Classes = { "cad-ribbon-tabs" },
            FontFamily = CadUi.UiFontFamily,
            FontSize = CadUi.UiFontSize,
            ItemsSource = new[]
            {
                BuildHomeTab(),
                CadRibbonBar.CreateTab(
                    "二维",
                    ActionGroup(
                        "基本",
                        Item("点", "draw.point"),
                        Item("直线", "draw.line"),
                        Item("多段线", "draw.polyline"),
                        Item("矩形", "draw.rectangle")),
                    ActionGroup(
                        "曲线",
                        Item("圆", "draw.circle"),
                        Item("圆弧", "draw.arc"),
                        Item("椭圆", "draw.ellipse"),
                        Item("样条", "draw.spline")),
                    ActionGroup(
                        "多边形",
                        Item("多边形", "draw.polygon"),
                        Item("正多边形", "draw.regularpolygon")),
                    ActionGroup(
                        "中心",
                        Item("中心线", "draw.centerline"),
                        Item("中心标记", "draw.centermark"))),

                CadRibbonBar.CreateTab(
                    "编辑",
                    ActionGroup(
                        "变换",
                        Item("移动", "modify.move"),
                        Item("复制", "modify.copy"),
                        Item("旋转", "modify.rotate"),
                        Item("缩放", "modify.scale"),
                        Item("镜像", "modify.mirror"),
                        Item("阵列", "modify.array"))),

                CadRibbonBar.CreateTab(
                    "三维",
                    ActionGroup(
                        "基本实体",
                        Item("长方体", "solid.box"),
                        Item("圆柱", "solid.cylinder"),
                        Item("圆锥", "solid.cone"),
                        Item("球", "solid.sphere"))),

                CadRibbonBar.CreateTab(
                    "视图",
                    ActionGroup(
                        "视角",
                        Item("适合", "view.fit"),
                        Item("等轴测", "view.isometric"),
                        Item("顶", "view.top"),
                        Item("底", "view.bottom"),
                        Item("前", "view.front"),
                        Item("后", "view.back"),
                        Item("左", "view.left"),
                        Item("右", "view.right")),
                    ActionGroup(
                        "显示",
                        Item("线框", "display.wireframe"),
                        Item("着色", "display.shaded"),
                        Item("隐藏", "view.hide"),
                        Item("隔离", "view.isolate"),
                        Item("全部显示", "view.showall")))
            }
        };

        var quick = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1
        };
        AddActionButton(quick, Item("撤销", "edit.undo"));
        AddActionButton(quick, Item("重做", "edit.redo"));
        AddActionButton(quick, Item("适合", "view.fit"));
        if (quick.Children.Count > 0)
            tabs.Margin = new Thickness(0, 0, 150, 0);

        return CadRibbonBar.CreateShell(
            tabs,
            quick.Children.Count > 0 ? quick : null);
    }

    private TabItem BuildHomeTab()
    {
        var fileButtons = new List<Control>();
        foreach (var command in _shellCommands)
        {
            fileButtons.Add(
                CadRibbonBar.CreateButton(
                    command.Text,
                    () => _ = ExecuteShellAsync(command),
                    command.Description));
        }

        return CadRibbonBar.CreateTab(
            "开始",
            CadRibbonBar.CreateGroup("文件", fileButtons),
            ActionGroup(
                "编辑",
                Item("撤销", "edit.undo"),
                Item("重做", "edit.redo"),
                Item("删除", "edit.delete")),
            ActionGroup(
                "选择",
                Item("全选", "select.all"),
                Item("反选", "select.invert")),
            ActionGroup(
                "测量",
                Item("测距", "measure.distance")));
    }

    private Border ActionGroup(
        string title,
        params RibbonAction[] actions)
    {
        var controls = new List<Control>();
        foreach (var item in actions)
        {
            var button = CreateActionButton(item);
            if (button is not null)
                controls.Add(button);
        }

        return CadRibbonBar.CreateGroup(title, controls);
    }

    private Button? CreateActionButton(RibbonAction item)
    {
        var action = _workspace.Actions.Find(item.Id);
        if (action is null)
            return null;

        var shortcut = string.IsNullOrWhiteSpace(action.Shortcut)
            ? string.Empty
            : $"  {action.Shortcut}";
        var button = CadRibbonBar.CreateButton(
            item.Text,
            () => Execute(item.Id, item.Text),
            $"{action.Description}{shortcut}");
        _actionButtons.Add((button, item.Id));
        return button;
    }

    private void AddActionButton(Panel host, RibbonAction item)
    {
        var button = CreateActionButton(item);
        if (button is not null)
            host.Children.Add(button);
    }

    private void RefreshCanExecute()
    {
        foreach (var (button, actionId) in _actionButtons)
            button.IsEnabled = _workspace.Actions.CanExecute(actionId);
    }

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
        finally
        {
            RefreshCanExecute();
        }
    }

    private void Execute(string id, string text)
    {
        if (!_workspace.Actions.Execute(id))
            _feedback($"当前不能执行：{text}");
        RefreshCanExecute();
    }

    private static RibbonAction Item(string text, string id) =>
        new(text, id);

    private sealed record RibbonAction(string Text, string Id);
}
