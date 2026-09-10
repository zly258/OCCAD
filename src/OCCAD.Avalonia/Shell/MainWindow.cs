using System.Drawing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OCCAD;
using OcctNet;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

/// <summary>
/// Minimal industrial CAD shell aligned with OCCTBIM-Source: ribbon, model dock,
/// viewport, property/layer inspector, command line and status bar. No duplicate
/// toolbars, decorative brand bars, floating tool panels or fake document tabs.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly CadWorkspace _workspace = new();
    private readonly CadCommandManager _commands;
    private readonly OcctAvaloniaViewport _viewport = new();
    private readonly TextBlock _prompt = new();
    private readonly TextBox _commandInput = new();
    private readonly TextBlock _selectionStatus = new();
    private readonly TextBlock _coordinateStatus = new();
    private readonly TextBlock _layerStatus = new();
    private readonly Button _snapStatus = new();
    private readonly Button _orthoStatus = new();
    private readonly Button _polarStatus = new();
    private readonly Button _planeStatus = new();
    private readonly CadViewportController _viewportController;
    private readonly CadInspectorPanel _inspector;
    private int _commandHistoryIndex = -1;
    private bool _disposed;

    public MainWindow()
    {
        _commands = CadCommandManager.ForWorkspace(_workspace);
        _inspector = new CadInspectorPanel(_workspace, ShowFeedback);

        Title = "OCCAD";
        Width = 1280;
        Height = 820;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowState = WindowState.Maximized;
        Background = new SolidColorBrush(MediaColor.Parse("#E8EBEF"));

        ConfigureViewport();
        _viewportController = new CadViewportController(
            _workspace,
            _viewport,
            Dispatcher.UIThread);

        Content = BuildShell();
        WireEvents();
        RefreshStatus();
        RefreshPrompt();

        Opened += (_, _) =>
        {
            _viewport.Focus();
            Dispatcher.UIThread.Post(
                () => _workspace.Engine?.Redraw(),
                DispatcherPriority.Loaded);
        };
        Closed += (_, _) => DisposeWorkspace();
    }

    private Control BuildShell()
    {
        var root = new DockPanel
        {
            LastChildFill = true,
            Background = new SolidColorBrush(MediaColor.Parse("#E8EBEF"))
        };

        var ribbon = new CadCompactRibbon(_workspace, ShowFeedback);
        DockPanel.SetDock(ribbon, Dock.Top);
        root.Children.Add(ribbon);

        var status = BuildStatusBar();
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        var command = BuildCommandBar();
        DockPanel.SetDock(command, Dock.Bottom);
        root.Children.Add(command);

        root.Children.Add(BuildWorkspace());
        return root;
    }

    private Control BuildWorkspace()
    {
        var grid = new Grid
        {
            Background = new SolidColorBrush(MediaColor.Parse("#252A30"))
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(4)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(4)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(280)));

        var model = new CadModelPanel(_workspace);
        grid.Children.Add(model);

        var leftSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            Background = new SolidColorBrush(MediaColor.Parse("#D3D8DE"))
        };
        Grid.SetColumn(leftSplitter, 1);
        grid.Children.Add(leftSplitter);

        Grid.SetColumn(_viewport, 2);
        grid.Children.Add(_viewport);

        var rightSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            Background = new SolidColorBrush(MediaColor.Parse("#D3D8DE"))
        };
        Grid.SetColumn(rightSplitter, 3);
        grid.Children.Add(rightSplitter);

        Grid.SetColumn(_inspector, 4);
        grid.Children.Add(_inspector);

        return grid;
    }

    private Control BuildCommandBar()
    {
        _prompt.Text = "就绪";
        _prompt.VerticalAlignment = VerticalAlignment.Center;
        _prompt.Foreground = new SolidColorBrush(MediaColor.Parse("#343A40"));
        _prompt.FontSize = 12;
        _prompt.TextTrimming = TextTrimming.CharacterEllipsis;

        _commandInput.MinHeight = 28;
        _commandInput.VerticalContentAlignment = VerticalAlignment.Center;
        _commandInput.PlaceholderText = "输入命令、坐标或精确值";
        _commandInput.KeyDown += CommandInputKeyDown;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,300"),
            Margin = new Thickness(6, 3)
        };
        grid.Children.Add(new TextBlock
        {
            Text = "命令:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            FontWeight = FontWeight.SemiBold
        });
        Grid.SetColumn(_commandInput, 1);
        grid.Children.Add(_commandInput);
        Grid.SetColumn(_prompt, 2);
        _prompt.Margin = new Thickness(8, 0, 0, 0);
        grid.Children.Add(_prompt);

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(MediaColor.Parse("#CCD2D8")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            MinHeight = 36,
            Child = grid
        };
    }

    private Control BuildStatusBar()
    {
        StatusText(_selectionStatus);
        StatusText(_coordinateStatus);
        StatusText(_layerStatus);
        StatusButton(_snapStatus, () =>
        {
            _workspace.Snap.Enabled = !_workspace.Snap.Enabled;
            if (!_workspace.Snap.Enabled) _workspace.Snap.Clear();
            RefreshStatus();
        });
        StatusButton(_orthoStatus, () =>
        {
            _workspace.Drafting.OrthogonalTrackingEnabled =
                !_workspace.Drafting.OrthogonalTrackingEnabled;
            _workspace.Tracking.Clear();
            RefreshStatus();
        });
        StatusButton(_polarStatus, () =>
        {
            _workspace.Drafting.PolarTrackingEnabled =
                !_workspace.Drafting.PolarTrackingEnabled;
            _workspace.Tracking.Clear();
            RefreshStatus();
        });
        StatusButton(_planeStatus, CyclePlane);

        var panel = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(6, 1)
        };

        DockPanel.SetDock(_coordinateStatus, Dock.Right);
        panel.Children.Add(_coordinateStatus);
        DockPanel.SetDock(_planeStatus, Dock.Right);
        panel.Children.Add(_planeStatus);
        DockPanel.SetDock(_polarStatus, Dock.Right);
        panel.Children.Add(_polarStatus);
        DockPanel.SetDock(_orthoStatus, Dock.Right);
        panel.Children.Add(_orthoStatus);
        DockPanel.SetDock(_snapStatus, Dock.Right);
        panel.Children.Add(_snapStatus);
        DockPanel.SetDock(_layerStatus, Dock.Right);
        panel.Children.Add(_layerStatus);
        panel.Children.Add(_selectionStatus);

        return new Border
        {
            Background = new SolidColorBrush(MediaColor.Parse("#F4F6F8")),
            BorderBrush = new SolidColorBrush(MediaColor.Parse("#CCD2D8")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            MinHeight = 26,
            Child = panel
        };
    }

    private void ConfigureViewport()
    {
        _viewport.InteractionFeatures = OcctViewportInteractionFeatures.Default;
        _viewport.RectangleSelectionBehavior = OcctRectangleSelectionBehavior.Directional;
        _viewport.RectangleSelectionThreshold = 5;
        _viewport.SynchronizeRenderDpi = true;
        _viewport.InitialOptions = new OcctViewportInitializationOptions
        {
            BackgroundColor = Color.FromArgb(37, 42, 48),
            ViewOrientation = OcctViewOrientation.Isometric,
            Projection = OcctProjectionType.Orthographic,
            TriedronVisible = true,
            ViewCubeVisible = true
        };
    }

    private void WireEvents()
    {
        _viewport.EngineRecreated += (_, args) =>
        {
            try
            {
                _workspace.AttachEngine(args.Engine);
                _viewportController.AttachEngine(args.Engine);
                ShowFeedback($"就绪 - OCCT {OcctEngine.OcctVersion}");
                RefreshStatus();
            }
            catch (Exception exception)
            {
                ShowFeedback(exception.Message);
            }
        };

        _viewport.ErrorOccurred += (_, args) => ShowFeedback(args.Exception.Message);
        _viewport.PreviewKeyInput += ViewportShortcutKeyInput;
        _viewport.PreviewPointerInput += ViewportContextPointerInput;
        _viewportController.CoordinateChanged += (_, args) =>
        {
            var point = args.Value.Point;
            _coordinateStatus.Text = $"X {point.X:0.###}  Y {point.Y:0.###}  Z {point.Z:0.###}";
        };
        _viewportController.CoordinateCleared += (_, _) => _coordinateStatus.Text = string.Empty;
        _viewportController.InteractionChanged += (_, _) => RefreshStatus();

        _workspace.Events.Changed += (_, args) =>
        {
            RefreshStatus();
            if (args.Kind is CadDomainEventKind.ActiveToolChanged or CadDomainEventKind.ToolStageChanged)
                RefreshPrompt();
            if (args.Kind == CadDomainEventKind.DocumentModifiedChanged)
                UpdateTitle();
        };
        _workspace.Actions.ActionFailed += (_, args) => ShowFeedback(args.Exception.Message);
    }

    private void ViewportShortcutKeyInput(object? sender, OcctKeyInputEventArgs input)
    {
        if (input.Handled ||
            input.Kind != OcctKeyInputKind.Pressed ||
            input.IsRepeat ||
            _workspace.Tools.ActiveTool is not null)
            return;

        string? actionId = null;
        var control = (input.Modifiers & OcctInputModifiers.Control) != 0;
        var shift = (input.Modifiers & OcctInputModifiers.Shift) != 0;
        var altOrMeta =
            (input.Modifiers & (OcctInputModifiers.Alt | OcctInputModifiers.Meta)) != 0;

        if (!altOrMeta && control)
        {
            actionId = input.Key switch
            {
                OcctKey.Z when shift => "edit.redo",
                OcctKey.Z => "edit.undo",
                OcctKey.Y => "edit.redo",
                OcctKey.A => "select.all",
                _ => null
            };
        }
        else if (!altOrMeta && !control && input.Key == OcctKey.Delete)
        {
            actionId = "edit.delete";
        }
        else if (!altOrMeta && !control && input.Key == OcctKey.Escape)
        {
            _workspace.Selection.Clear();
            _workspace.Subobjects.Clear();
            input.Handled = true;
            return;
        }

        if (actionId is null || _workspace.Actions.Find(actionId) is null)
            return;

        input.Handled = true;
        if (!_workspace.Actions.Execute(actionId))
            ShowFeedback("当前不能执行该命令");
    }

    private void ViewportContextPointerInput(object? sender, OcctPointerInputEventArgs input)
    {
        if (input.Kind != OcctPointerInputKind.Pressed ||
            input.Button != OcctPointerButton.Right ||
            _workspace.Tools.ActiveTool is not null)
            return;

        input.Handled = true;
        OpenViewportContextMenu();
    }

    private void OpenViewportContextMenu()
    {
        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                ContextAction("恢复全部", "view.showall"),
                ContextAction("隐藏", "view.hide"),
                ContextAction("隔离", "view.isolate"),
                new Separator(),
                ContextAction("全选", "select.all"),
                ContextAction("反选", "select.invert"),
                new Separator(),
                ContextAction("移动", "modify.move"),
                ContextAction("复制", "modify.copy"),
                new Separator(),
                ContextAction("删除", "edit.delete"),
                new Separator(),
                ContextUiAction("属性", () => _inspector.ShowProperties())
            }
        };
        menu.Closed += (_, _) => _viewport.Focus();
        menu.Open(_viewport);
    }

    private MenuItem ContextAction(string text, string actionId)
    {
        var item = new MenuItem
        {
            Header = text,
            IsEnabled = _workspace.Actions.CanExecute(actionId)
        };
        item.Click += (_, _) =>
        {
            if (!_workspace.Actions.Execute(actionId))
                ShowFeedback($"当前不能执行：{text}");
        };
        return item;
    }

    private static MenuItem ContextUiAction(string text, Action action)
    {
        var item = new MenuItem { Header = text };
        item.Click += (_, _) => action();
        return item;
    }

    private void CommandInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _workspace.Tools.CancelCurrent();
            _commandInput.Text = string.Empty;
            _commandHistoryIndex = -1;
            RefreshPrompt();
            _viewport.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            NavigateCommandHistory(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        var text = _commandInput.Text;
        var result = _commands.Execute(text);
        _commandInput.Text = string.Empty;
        _commandHistoryIndex = -1;
        ShowFeedback(result.Message ?? ResultText(result.Kind));
        RefreshPrompt();
        RefreshStatus();
        _viewport.Focus();
        e.Handled = true;
    }

    private void NavigateCommandHistory(int direction)
    {
        var history = _commands.History;
        if (history.Count == 0)
            return;

        if (_commandHistoryIndex < 0)
            _commandHistoryIndex = history.Count;

        _commandHistoryIndex = Math.Clamp(
            _commandHistoryIndex + direction,
            0,
            history.Count);
        _commandInput.Text = _commandHistoryIndex == history.Count
            ? string.Empty
            : history[_commandHistoryIndex];
        _commandInput.CaretIndex = _commandInput.Text?.Length ?? 0;
    }

    private void CyclePlane()
    {
        if (!_workspace.Tools.CanChangeDrawingPlane)
        {
            ShowFeedback("当前阶段不能切换绘图平面");
            return;
        }

        var next = _workspace.WorkPlane.Preset switch
        {
            CadWorkPlanePreset.XY => CadWorkPlanePreset.YZ,
            CadWorkPlanePreset.YZ => CadWorkPlanePreset.XZ,
            _ => CadWorkPlanePreset.XY
        };
        _workspace.Tools.TryChangeDrawingPlane(next);
        RefreshStatus();
    }

    private void RefreshPrompt()
    {
        _prompt.Text = _workspace.Tools.ActiveTool?.Prompt ?? "就绪";
    }

    private void RefreshStatus()
    {
        var count = _workspace.Selection.Selected.Count;
        _selectionStatus.Text = count == 0 ? "未选择" : $"已选择 {count}";
        _layerStatus.Text = $"图层: {_workspace.Layers.Current.Name}";
        _snapStatus.Content = _workspace.Snap.Enabled ? "捕捉 F3" : "捕捉关";
        _orthoStatus.Content = _workspace.Drafting.OrthogonalTrackingEnabled ? "正交 F8" : "正交关";
        _polarStatus.Content = _workspace.Drafting.PolarTrackingEnabled ? "极轴 F10" : "极轴关";
        _planeStatus.Content = $"平面 {_workspace.WorkPlane.Preset}";
        UpdateTitle();
    }

    private void UpdateTitle() =>
        Title = _workspace.IsModified ? "OCCAD *" : "OCCAD";

    private void ShowFeedback(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _prompt.Text = text;
    }

    private static string ResultText(CadCommandResultKind kind) => kind switch
    {
        CadCommandResultKind.Executed => "命令已执行",
        CadCommandResultKind.InputApplied => "输入已接受",
        CadCommandResultKind.Repeated => "重复上一命令",
        CadCommandResultKind.Canceled => "已取消",
        _ => "命令失败"
    };

    private static void StatusText(TextBlock text)
    {
        text.VerticalAlignment = VerticalAlignment.Center;
        text.FontSize = 11;
        text.Foreground = new SolidColorBrush(MediaColor.Parse("#495057"));
        text.Margin = new Thickness(5, 0);
    }

    private static void StatusButton(Button button, Action action)
    {
        button.MinHeight = 22;
        button.Padding = new Thickness(6, 1);
        button.Margin = new Thickness(1, 0);
        button.Click += (_, _) => action();
    }

    private void DisposeWorkspace()
    {
        if (_disposed) return;
        _disposed = true;
        _viewportController.Dispose();
        _workspace.Dispose();
    }
}
