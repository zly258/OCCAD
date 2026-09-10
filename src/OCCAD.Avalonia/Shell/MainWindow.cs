using System.Drawing;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OCCAD;
using OcctNet;

namespace OCCAD.Avalonia;

/// <summary>
/// Thin Avalonia adapter around CadApplicationCore. The shell owns layout and
/// focus only; CAD state, commands, tools, selection and transactions stay in Core.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly CadWorkspace _workspace;
    private readonly CadSettingsStore _settings;
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

    public MainWindow(CadApplicationCore application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _workspace = application.Workspace;
        _settings = application.Settings;
        _commands = CadCommandManager.ForWorkspace(_workspace);
        _inspector = new CadInspectorPanel(_workspace, ShowFeedback);

        Title = "OCCAD";
        Width = 1280;
        Height = 820;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowState = WindowState.Maximized;
        Background = CadUi.Window;

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
        Closed += (_, _) => DisposeShell();
    }

    private Control BuildShell()
    {
        var root = new DockPanel
        {
            LastChildFill = true,
            Background = CadUi.Window
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
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CadUi.ModelPanelWidth)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CadUi.SplitterWidth)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CadUi.SplitterWidth)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CadUi.InspectorPanelWidth)));

        var model = new CadModelPanel(_workspace);
        grid.Children.Add(model);

        var leftSplitter = CreateSplitter();
        Grid.SetColumn(leftSplitter, 1);
        grid.Children.Add(leftSplitter);

        Grid.SetColumn(_viewport, 2);
        grid.Children.Add(_viewport);

        var rightSplitter = CreateSplitter();
        Grid.SetColumn(rightSplitter, 3);
        grid.Children.Add(rightSplitter);

        Grid.SetColumn(_inspector, 4);
        grid.Children.Add(_inspector);
        return grid;
    }

    private static GridSplitter CreateSplitter() =>
        new()
        {
            ResizeDirection = GridResizeDirection.Columns,
            Background = CadUi.Border
        };

    private Control BuildCommandBar()
    {
        _prompt.Text = "就绪";
        _prompt.VerticalAlignment = VerticalAlignment.Center;
        _prompt.Foreground = CadUi.Muted;
        _prompt.FontSize = CadUi.UiFontSize;
        _prompt.TextTrimming = TextTrimming.CharacterEllipsis;

        _commandInput.MinHeight = CadUi.CompactControlHeight;
        _commandInput.FontSize = CadUi.UiFontSize;
        _commandInput.VerticalContentAlignment = VerticalAlignment.Center;
        _commandInput.PlaceholderText = "命令 / 坐标 / 精确值";
        _commandInput.KeyDown += CommandInputKeyDown;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,260"),
            Margin = new Thickness(6, 2)
        };
        grid.Children.Add(new TextBlock
        {
            Text = ">",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            FontWeight = FontWeight.SemiBold,
            Foreground = CadUi.Accent
        });
        Grid.SetColumn(_commandInput, 1);
        grid.Children.Add(_commandInput);
        Grid.SetColumn(_prompt, 2);
        _prompt.Margin = new Thickness(8, 0, 0, 0);
        grid.Children.Add(_prompt);

        return new Border
        {
            Background = CadUi.Panel,
            BorderBrush = CadUi.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            MinHeight = 32,
            Child = grid
        };
    }

    private Control BuildStatusBar()
    {
        ConfigureStatusText(_selectionStatus);
        ConfigureStatusText(_coordinateStatus);
        ConfigureStatusText(_layerStatus);

        ConfigureStatusButton(_snapStatus, ToggleSnap);
        ConfigureStatusButton(_orthoStatus, ToggleOrtho);
        ConfigureStatusButton(_polarStatus, TogglePolar);
        ConfigureStatusButton(_planeStatus, CyclePlane);

        var panel = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(5, 1)
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
            Background = CadUi.Surface,
            BorderBrush = CadUi.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            MinHeight = 24,
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
            BackgroundColor = ReadSceneBackground(),
            ViewOrientation = OcctViewOrientation.Isometric,
            Projection = OcctProjectionType.Orthographic,
            TriedronVisible = true,
            ViewCubeVisible = true
        };
    }

    private Color ReadSceneBackground()
    {
        var text = _settings.Get(CadSettingKeys.SceneBackground, "#252A30");
        var hex = text.Trim().TrimStart('#');
        if (hex.Length == 6 &&
            uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return Color.FromArgb(
                (int)((value >> 16) & 0xFF),
                (int)((value >> 8) & 0xFF),
                (int)(value & 0xFF));
        }

        return Color.FromArgb(37, 42, 48);
    }

    private void WireEvents()
    {
        _viewport.EngineRecreated += (_, args) =>
        {
            try
            {
                _workspace.AttachEngine(args.Engine);
                _viewportController.AttachEngine(args.Engine);
                ShowFeedback($"就绪 · OCCT {OcctEngine.OcctVersion}");
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
        };
        _workspace.Actions.ActionFailed += (_, args) => ShowFeedback(args.Exception.Message);
    }

    private void ViewportShortcutKeyInput(object? sender, OcctKeyInputEventArgs input)
    {
        if (input.Handled || input.Kind != OcctKeyInputKind.Pressed || input.IsRepeat)
            return;

        if (_workspace.Tools.ActiveTool is null)
        {
            if (input.Modifiers == OcctInputModifiers.None && input.Key == OcctKey.Escape)
            {
                _workspace.Selection.Clear();
                _workspace.Subobjects.Clear();
                input.Handled = true;
                return;
            }

            if (input.Modifiers == OcctInputModifiers.None && input.Key == OcctKey.Space)
            {
                input.Handled = true;
                ExecuteCommand(string.Empty);
                return;
            }

            var shortcut = ShortcutText(input);
            if (shortcut is not null && _workspace.Actions.FindByShortcut(shortcut) is not null)
            {
                input.Handled = true;
                if (!_workspace.Actions.ExecuteShortcut(shortcut))
                    ShowFeedback("当前不能执行该命令");
                return;
            }

            if (input.Modifiers == OcctInputModifiers.None && TryLetter(input.Key, out var letter))
            {
                input.Handled = true;
                BeginCommandEntry(letter);
            }
            return;
        }

        if (input.Modifiers == OcctInputModifiers.None && TryDigit(input.Key, out var digit))
        {
            input.Handled = true;
            BeginCommandEntry(digit);
        }
    }

    private void ViewportContextPointerInput(object? sender, OcctPointerInputEventArgs input)
    {
        if (input.Handled ||
            input.Kind != OcctPointerInputKind.Pressed ||
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
                ContextAction("删除", "edit.delete"),
                new Separator(),
                ContextUiAction("属性", _inspector.ShowProperties)
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

    private void BeginCommandEntry(string seed)
    {
        _commandInput.Text = seed;
        _commandInput.CaretIndex = seed.Length;
        _commandInput.Focus();
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

        if (e.Key is Key.Up or Key.Down)
        {
            NavigateCommandHistory(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        ExecuteCommand(_commandInput.Text);
        e.Handled = true;
    }

    private void ExecuteCommand(string? text)
    {
        var result = _commands.Execute(text);
        _commandInput.Text = string.Empty;
        _commandHistoryIndex = -1;
        ShowFeedback(result.Message ?? ResultText(result.Kind));
        RefreshPrompt();
        RefreshStatus();
        _viewport.Focus();
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

    private void ToggleSnap()
    {
        _workspace.Snap.Enabled = !_workspace.Snap.Enabled;
        if (!_workspace.Snap.Enabled)
            _workspace.Snap.Clear();
        RefreshStatus();
        _viewport.Focus();
    }

    private void ToggleOrtho()
    {
        _workspace.Drafting.OrthogonalTrackingEnabled =
            !_workspace.Drafting.OrthogonalTrackingEnabled;
        _workspace.Tracking.Clear();
        RefreshStatus();
        _viewport.Focus();
    }

    private void TogglePolar()
    {
        _workspace.Drafting.PolarTrackingEnabled =
            !_workspace.Drafting.PolarTrackingEnabled;
        _workspace.Tracking.Clear();
        RefreshStatus();
        _viewport.Focus();
    }

    private void CyclePlane()
    {
        if (_workspace.Tools.ActiveTool is null || !_workspace.Tools.CanChangeDrawingPlane)
        {
            ShowFeedback("绘图过程中可切换工作平面：T=XY / F=XZ / S=YZ");
            _viewport.Focus();
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
        _viewport.Focus();
    }

    private void RefreshPrompt() =>
        _prompt.Text = _workspace.Tools.ActiveTool?.Prompt ?? "就绪";

    private void RefreshStatus()
    {
        var count = _workspace.Selection.Selected.Count;
        _selectionStatus.Text = count == 0 ? "未选择" : $"选择 {count}";
        _layerStatus.Text = $"层 {_workspace.Layers.Current.Name}";
        _snapStatus.Content = _workspace.Snap.Enabled ? "SNAP F3" : "SNAP —";
        _orthoStatus.Content = _workspace.Drafting.OrthogonalTrackingEnabled ? "ORTHO F8" : "ORTHO —";
        _polarStatus.Content = _workspace.Drafting.PolarTrackingEnabled ? "POLAR F10" : "POLAR —";
        _planeStatus.Content = $"{_workspace.WorkPlane.Preset}";
        Title = _workspace.IsModified ? "OCCAD *" : "OCCAD";
    }

    private void ShowFeedback(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
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

    private static void ConfigureStatusText(TextBlock text)
    {
        text.VerticalAlignment = VerticalAlignment.Center;
        text.FontSize = CadUi.UiFontSize;
        text.Foreground = CadUi.Muted;
        text.Margin = new Thickness(5, 0);
    }

    private static void ConfigureStatusButton(Button button, Action action)
    {
        CadUi.ConfigureStatusButton(button);
        button.Click += (_, _) => action();
    }

    private static string? ShortcutText(OcctKeyInputEventArgs input)
    {
        var key = ShortcutKeyName(input.Key);
        if (key is null)
            return null;

        List<string> parts = [];
        if ((input.Modifiers & OcctInputModifiers.Control) != 0)
            parts.Add("Ctrl");
        if ((input.Modifiers & OcctInputModifiers.Shift) != 0)
            parts.Add("Shift");
        if ((input.Modifiers & OcctInputModifiers.Alt) != 0)
            parts.Add("Alt");
        if ((input.Modifiers & OcctInputModifiers.Meta) != 0)
            parts.Add("Meta");
        parts.Add(key);
        return string.Join('+', parts);
    }

    private static string? ShortcutKeyName(OcctKey key)
    {
        var raw = (int)key;
        if (raw >= (int)OcctKey.A && raw <= (int)OcctKey.Z)
            return key.ToString();
        if (raw >= (int)OcctKey.F1 && raw <= (int)OcctKey.F12)
            return key.ToString();
        return key switch
        {
            OcctKey.Delete => "Delete",
            OcctKey.Insert => "Insert",
            OcctKey.Home => "Home",
            OcctKey.End => "End",
            _ => null
        };
    }

    private static bool TryLetter(OcctKey key, out string text)
    {
        var raw = (int)key;
        if (raw >= (int)OcctKey.A && raw <= (int)OcctKey.Z)
        {
            text = key.ToString().ToLowerInvariant();
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static bool TryDigit(OcctKey key, out string text)
    {
        var raw = (int)key;
        if (raw >= (int)OcctKey.D0 && raw <= (int)OcctKey.D9)
        {
            text = (raw - (int)OcctKey.D0).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        text = string.Empty;
        return false;
    }

    private void DisposeShell()
    {
        if (_disposed)
            return;
        _disposed = true;
        _viewportController.Dispose();
    }
}
