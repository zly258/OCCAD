using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OCCAD;
using OcctNet;
using DrawingColor = System.Drawing.Color;

namespace OCCAD.Avalonia;

/// <summary>
/// Thin Avalonia adapter around <see cref="CadApplicationCore"/>. The shell owns
/// layout, focus and platform storage only. CAD model state, commands, tools,
/// selection, history and scene semantics stay in Core.
/// </summary>
internal sealed class MainWindow : Window
{
    private static readonly FilePickerFileType OccadDocumentType =
        new("OCCAD Document") { Patterns = ["*.occad"] };

    private readonly CadApplicationCore _application;
    private readonly CadWorkspace _workspace;
    private readonly CadSettingsStore _settings;
    private readonly CadDocumentStorage _documents;
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
    private bool _closePromptOpen;
    private bool _allowClose;
    private bool _disposed;

    public MainWindow(CadApplicationCore application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _workspace = application.Workspace;
        _settings = application.Settings;
        _documents = new CadDocumentStorage(application);
        _commands = application.Commands;
        _inspector = new CadInspectorPanel(_workspace, ShowFeedback);

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
        RefreshPrompt();
        RefreshStatus();

        Opened += WindowOpened;
        Closing += WindowClosing;
        Closed += WindowClosed;
    }

    private Control BuildShell()
    {
        var root = new DockPanel
        {
            LastChildFill = true,
            Background = CadUi.Window
        };

        var ribbon = new CadCompactRibbon(
            _workspace,
            ShowFeedback,
            CreateShellCommands());
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

    private IReadOnlyList<CadShellCommand> CreateShellCommands() =>
    [
        new("新建", NewDocumentAsync, "新建 OCCAD 文档  Ctrl+N"),
        new("打开", OpenDocumentAsync, "打开 OCCAD 文档  Ctrl+O"),
        new("保存", async () => _ = await SaveDocumentAsync(false), "保存  Ctrl+S"),
        new("另存", async () => _ = await SaveDocumentAsync(true), "另存为  Ctrl+Shift+S")
    ];

    private Control BuildWorkspace()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                $"{CadUi.ModelPanelWidth},{CadUi.SplitterWidth},*,{CadUi.SplitterWidth},{CadUi.InspectorPanelWidth}")
        };

        grid.Children.Add(new CadModelPanel(_workspace));

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
        _prompt.Margin = new Thickness(8, 0, 0, 0);

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

        AddRight(panel, _coordinateStatus);
        AddRight(panel, _planeStatus);
        AddRight(panel, _polarStatus);
        AddRight(panel, _orthoStatus);
        AddRight(panel, _snapStatus);
        AddRight(panel, _layerStatus);
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

    private static void AddRight(DockPanel panel, Control control)
    {
        DockPanel.SetDock(control, Dock.Right);
        panel.Children.Add(control);
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
            ViewCubeVisible = false
        };
    }

    private DrawingColor ReadSceneBackground()
    {
        var text = _settings.Get(CadSettingKeys.SceneBackground, "#252A30");
        var hex = text.Trim().TrimStart('#');
        if (hex.Length == 6 &&
            uint.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return DrawingColor.FromArgb(
                (int)((value >> 16) & 0xFF),
                (int)((value >> 8) & 0xFF),
                (int)(value & 0xFF));
        }

        return DrawingColor.FromArgb(37, 42, 48);
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
                ShowFeedback($"场景初始化失败：{exception.Message}");
            }
        };

        _viewport.ErrorOccurred += (_, args) => ShowFeedback(args.Exception.Message);
        _viewport.PreviewKeyInput += ViewportShortcutKeyInput;
        _viewport.PreviewPointerInput += ViewportContextPointerInput;
        _viewport.TextInput += ViewportTextInput;
        _viewportController.CoordinateChanged += (_, args) =>
        {
            var point = args.Value.Point;
            _coordinateStatus.Text =
                $"X {point.X:0.###}  Y {point.Y:0.###}  Z {point.Z:0.###}";
        };
        _viewportController.CoordinateCleared += (_, _) =>
            _coordinateStatus.Text = string.Empty;
        _viewportController.InteractionChanged += (_, _) => RefreshStatus();

        _workspace.Events.Changed += (_, args) =>
        {
            RefreshStatus();
            if (args.Kind is
                CadDomainEventKind.ActiveToolChanged or
                CadDomainEventKind.ToolStageChanged)
            {
                RefreshPrompt();
            }
        };
        _workspace.ModifiedChanged += (_, _) => RefreshStatus();
        _application.Documents.IdentityChanged += (_, _) => RefreshStatus();
        _workspace.Actions.ActionFailed += (_, args) =>
            ShowFeedback(args.Exception.Message);
    }

    private void WindowOpened(object? sender, EventArgs args)
    {
        _viewport.Focus();
        Dispatcher.UIThread.Post(
            () => _workspace.Engine?.Redraw(),
            DispatcherPriority.Loaded);
    }

    private async void WindowClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_allowClose || !_workspace.IsModified)
            return;

        args.Cancel = true;
        if (_closePromptOpen)
            return;

        _closePromptOpen = true;
        try
        {
            if (await ConfirmCanReplaceDocumentAsync())
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closePromptOpen = false;
        }
    }

    private void WindowClosed(object? sender, EventArgs args) => DisposeShell();

    private async void ViewportShortcutKeyInput(
        object? sender,
        OcctKeyInputEventArgs input)
    {
        if (input.Handled ||
            input.Kind != OcctKeyInputKind.Pressed ||
            input.IsRepeat)
            return;

        if (_workspace.Tools.ActiveTool is null)
        {
            var shortcut = ShortcutText(input);
            switch (shortcut)
            {
                case "Ctrl+N":
                    input.Handled = true;
                    await NewDocumentAsync();
                    return;
                case "Ctrl+O":
                    input.Handled = true;
                    await OpenDocumentAsync();
                    return;
                case "Ctrl+S":
                    input.Handled = true;
                    _ = await SaveDocumentAsync(false);
                    return;
                case "Ctrl+Shift+S":
                    input.Handled = true;
                    _ = await SaveDocumentAsync(true);
                    return;
            }

            if (input.Modifiers == OcctInputModifiers.None &&
                input.Key == OcctKey.Escape)
            {
                _workspace.Selection.Clear();
                _workspace.Subobjects.Clear();
                input.Handled = true;
                return;
            }

            if (input.Modifiers == OcctInputModifiers.None &&
                input.Key == OcctKey.Space)
            {
                input.Handled = true;
                ExecuteCoreCommand(string.Empty);
                return;
            }

            if (shortcut is not null &&
                _workspace.Actions.FindByShortcut(shortcut) is not null)
            {
                input.Handled = true;
                if (!_workspace.Actions.ExecuteShortcut(shortcut))
                    ShowFeedback("当前不能执行该命令");
                return;
            }

            if (input.Modifiers == OcctInputModifiers.None &&
                TryLetter(input.Key, out var letter))
            {
                input.Handled = true;
                BeginCommandEntry(letter);
            }
            return;
        }

        if (input.Modifiers == OcctInputModifiers.None &&
            TryDigit(input.Key, out var digit))
        {
            input.Handled = true;
            BeginCommandEntry(digit);
        }
    }

    private void ViewportTextInput(object? sender, TextInputEventArgs args)
    {
        if (_workspace.Tools.ActiveTool is null ||
            string.IsNullOrEmpty(args.Text) ||
            args.Text.Length != 1)
            return;

        // OcctKey intentionally models command/navigation keys only. Printable
        // punctuation needed by exact point syntax is routed from Avalonia text
        // input so @relative, #absolute, negative and decimal values can start
        // directly while the viewport owns focus.
        var character = args.Text[0];
        if (character is not ('@' or '#' or '-' or '+' or '.' or ',' or '<'))
            return;

        BeginCommandEntry(args.Text);
        args.Handled = true;
    }

    private void ViewportContextPointerInput(
        object? sender,
        OcctPointerInputEventArgs input)
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

    private async void CommandInputKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            _workspace.Tools.CancelCurrent();
            ResetCommandInput();
            RefreshPrompt();
            _viewport.Focus();
            args.Handled = true;
            return;
        }

        if (args.Key is Key.Up or Key.Down)
        {
            NavigateCommandHistory(args.Key == Key.Up ? -1 : 1);
            args.Handled = true;
            return;
        }

        if (args.Key != Key.Enter)
            return;

        await ExecuteCommandAsync(_commandInput.Text);
        args.Handled = true;
    }

    private async Task ExecuteCommandAsync(string? text)
    {
        var input = text?.Trim() ?? string.Empty;
        if (_workspace.Tools.ActiveTool is null)
        {
            switch (input.ToUpperInvariant())
            {
                case "NEW":
                    ResetCommandInput();
                    await NewDocumentAsync();
                    return;
                case "OPEN":
                    ResetCommandInput();
                    await OpenDocumentAsync();
                    return;
                case "SAVE":
                    ResetCommandInput();
                    _ = await SaveDocumentAsync(false);
                    return;
                case "SAVEAS":
                    ResetCommandInput();
                    _ = await SaveDocumentAsync(true);
                    return;
            }
        }

        ExecuteCoreCommand(input);
    }

    private void ExecuteCoreCommand(string? text)
    {
        var result = _commands.Execute(text);
        ResetCommandInput();
        ShowFeedback(result.Message ?? ResultText(result.Kind));
        RefreshPrompt();
        RefreshStatus();
        _viewport.Focus();
    }

    private void ResetCommandInput()
    {
        _commandInput.Text = string.Empty;
        _commandHistoryIndex = -1;
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
        _commandInput.Text =
            _commandHistoryIndex == history.Count
                ? string.Empty
                : history[_commandHistoryIndex];
        _commandInput.CaretIndex = _commandInput.Text?.Length ?? 0;
    }

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmCanReplaceDocumentAsync())
            return;

        _documents.New();
        _workspace.Engine?.Redraw();
        ShowFeedback("已新建文档");
        RefreshStatus();
        _viewport.Focus();
    }

    private async Task OpenDocumentAsync()
    {
        if (!await ConfirmCanReplaceDocumentAsync())
            return;
        if (!StorageProvider.CanOpen)
        {
            ShowFeedback("当前平台不支持打开文件");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "打开 OCCAD 文档",
                AllowMultiple = false,
                FileTypeFilter = [OccadDocumentType]
            });
        if (files.Count == 0)
            return;

        try
        {
            await _documents.OpenAsync(files[0]);
            _workspace.Engine?.FitAll();
            ShowFeedback($"已打开 {_documents.DisplayName}");
            RefreshStatus();
        }
        catch (Exception exception)
        {
            files[0].Dispose();
            ShowFeedback($"打开失败：{exception.Message}");
        }
        finally
        {
            _viewport.Focus();
        }
    }

    private async Task<bool> SaveDocumentAsync(bool saveAs)
    {
        IStorageFile? target = null;
        var pickedTarget = false;

        try
        {
            if (!saveAs && _documents.CurrentFile is not null)
            {
                await _documents.SaveCurrentAsync();
                ShowFeedback($"已保存 {_documents.DisplayName}");
                RefreshStatus();
                return true;
            }

            if (!StorageProvider.CanSave)
            {
                ShowFeedback("当前平台不支持保存文件");
                return false;
            }

            target = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = saveAs ? "另存 OCCAD 文档" : "保存 OCCAD 文档",
                    SuggestedFileName = _documents.DisplayName,
                    DefaultExtension = "occad",
                    ShowOverwritePrompt = true,
                    FileTypeChoices = [OccadDocumentType]
                });
            if (target is null)
                return false;

            pickedTarget = true;
            await _documents.SaveAsAsync(target);
            ShowFeedback($"已保存 {_documents.DisplayName}");
            RefreshStatus();
            return true;
        }
        catch (Exception exception)
        {
            if (pickedTarget && !ReferenceEquals(target, _documents.CurrentFile))
                target?.Dispose();
            ShowFeedback($"保存失败：{exception.Message}");
            return false;
        }
        finally
        {
            _viewport.Focus();
        }
    }

    private async Task<bool> ConfirmCanReplaceDocumentAsync()
    {
        if (!_workspace.IsModified)
            return true;

        var decision = await new CadSaveChangesDialog(_documents.DisplayName)
            .ShowDialog<CadSaveChangesDecision>(this);

        return decision switch
        {
            CadSaveChangesDecision.Save => await SaveDocumentAsync(false),
            CadSaveChangesDecision.Discard => true,
            _ => false
        };
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
        if (_workspace.Tools.ActiveTool is null ||
            !_workspace.Tools.CanChangeDrawingPlane)
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
        _prompt.Text = _workspace.Tools.ActiveTool?.Prompt?.Message ?? "就绪";

    private void RefreshStatus()
    {
        var count = _workspace.Selection.Selected.Count;
        _selectionStatus.Text = count == 0 ? "未选择" : $"选择 {count}";
        _layerStatus.Text = $"层 {_workspace.Layers.Current.Name}";
        _snapStatus.Content = _workspace.Snap.Enabled ? "SNAP F3" : "SNAP —";
        _orthoStatus.Content =
            _workspace.Drafting.OrthogonalTrackingEnabled ? "ORTHO F8" : "ORTHO —";
        _polarStatus.Content =
            _workspace.Drafting.PolarTrackingEnabled ? "POLAR F10" : "POLAR —";
        _planeStatus.Content = _workspace.WorkPlane.Preset.ToString();
        Title =
            $"{_documents.DisplayName}{(_workspace.IsModified ? " *" : string.Empty)} - OCCAD";
    }

    private void ShowFeedback(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            _prompt.Text = text;
    }

    private static string ResultText(CadCommandResultKind kind) =>
        kind switch
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
            text = (raw - (int)OcctKey.D0)
                .ToString(CultureInfo.InvariantCulture);
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
        _documents.Dispose();
        _viewportController.Dispose();
    }
}
