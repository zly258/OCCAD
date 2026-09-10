using Avalonia.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using OCCAD;
using OcctNet;
using DrawingColor = System.Drawing.Color;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow : Window
{
    private readonly CadWorkspace _workspace = new();

    private readonly Menu _mainMenu = new();
    private readonly ComboBox _layerCombo = new();
    private readonly ToggleButton _planeXy = new();
    private readonly ToggleButton _planeYz = new();
    private readonly ToggleButton _planeXz = new();
    private readonly ToggleButton _snapToggle = new();
    private readonly ToggleButton _orthoToggle = new();
    private readonly ToggleButton _polarToggle = new();
    private readonly Button _finishButton = new();
    private readonly Button _cancelButton = new();

    private readonly Border _modelPanel = new();
    private TextBlock? _modelHeaderText;
    private readonly TextBox _modelSearch = new();
    private readonly TreeView _modelTree = new();
    private readonly ColumnDefinition _modelColumn =
        new(new GridLength(218));
    private readonly ColumnDefinition _modelSplitterColumn =
        new(new GridLength(4));

    private readonly Border _rightPanel = new();
    private TextBlock? _rightHeaderText;
    private readonly TabControl _rightTabs = new();
    private readonly StackPanel _layerHost = new();
    private readonly StackPanel _propertyHost = new();
    private readonly ColumnDefinition _rightSplitterColumn =
        new(new GridLength(4));
    private readonly ColumnDefinition _rightColumn =
        new(new GridLength(352));

    private readonly Grid _viewportHost = new();
    private readonly OcctAvaloniaViewport _viewport = new();
    private readonly Ellipse _snapAperture = new();
    private readonly Border _dynamicHud = new();
    private readonly TextBlock _dynamicValue = new();

    private readonly TextBlock _toolStatus = new();
    private readonly TextBlock _selectionStatus = new();
    private readonly TextBlock _historyStatus = new();
    private readonly TextBlock _snapStatus = new();
    private readonly TextBlock _precisionStatus = new();
    private readonly TextBlock _workPlaneStatus = new();
    private readonly TextBlock _coordinateStatus = new();
    private readonly StackPanel _commandHost = new();

    private readonly Dictionary<string, List<MenuItem>> _actionItems =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<CadSnapType, MenuItem> _snapModeItems = [];
    private readonly Dictionary<double, MenuItem> _polarItems = [];
    private MenuItem? _chineseMenu;
    private MenuItem? _englishMenu;
    private MenuItem? _modelPanelMenu;
    private MenuItem? _layerPanelMenu;
    private MenuItem? _propertyPanelMenu;
    private MenuItem? _toolPanelMenu;

    private readonly CadToolPanel _toolPanel;
    private readonly CadViewportInteractionController _viewportInteraction;
    private readonly CadPropertyInspectorController _propertyInspector;
    private readonly CadLayerPanelController _layerPanel;
    private readonly CadCommandLineController _commandLine;

    private bool _refreshingUi;
    private bool _refreshingTree;
    private bool _closingConfirmed;
    private bool _disposed;
    private global::Avalonia.Platform.Storage.IStorageFile? _documentFile;

    public MainWindow()
    {
        Title = "OCCAD";
        Width = 1180;
        Height = 760;
        MinWidth = 800;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowState = WindowState.Maximized;
        Background = CadTheme.WindowBrush;

        _toolPanel = new CadToolPanel(_workspace);
        _toolPanel.PanelVisibilityChanged += (_, _) =>
            RefreshPanelMenuState();
        ConfigureViewport();
        Content = BuildShell();

        _viewportInteraction = new CadViewportInteractionController(
            _workspace,
            _viewport,
            Dispatcher);
        _propertyInspector =
            new CadPropertyInspectorController(
                this,
                _workspace,
                _propertyHost);
        _layerPanel =
            new CadLayerPanelController(
                this,
                _workspace,
                _layerHost,
                InspectLayer);
        _commandLine =
            new CadCommandLineController(
                _workspace,
                _commandHost);

        WireEvents();
        BuildMenu();
        RefreshAll();
        UpdateWindowTitle();

        Opened += (_, _) =>
        {
            _viewport.Focus();
            Dispatcher.Post(
                () =>
                {
                    if (_workspace.Engine is { IsInitialized: true } engine)
                        engine.Redraw();
                },
                DispatcherPriority.Loaded);
        };
    }

    private Control BuildShell()
    {
        var root = new DockPanel
        {
            LastChildFill = true,
            Background = CadTheme.WindowBrush
        };

        _mainMenu.Background = CadTheme.Panel;
        _mainMenu.BorderBrush = CadTheme.Border;
        _mainMenu.BorderThickness = new Thickness(0, 0, 0, 1);
        DockPanel.SetDock(_mainMenu, Dock.Top);
        root.Children.Add(_mainMenu);

        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var status = BuildStatusBar();
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        DockPanel.SetDock(_commandHost, Dock.Bottom);
        root.Children.Add(_commandHost);

        var workspace = BuildWorkspace();
        root.Children.Add(workspace);

        return root;
    }

    private Control BuildToolbar()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Margin = new Thickness(5, 3),
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = "WP",
            Foreground = CadTheme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 1, 0)
        });

        ConfigurePlaneButton(_planeXy, "XY", CadWorkPlanePreset.XY);
        ConfigurePlaneButton(_planeYz, "YZ", CadWorkPlanePreset.YZ);
        ConfigurePlaneButton(_planeXz, "XZ", CadWorkPlanePreset.XZ);
        panel.Children.Add(_planeXy);
        panel.Children.Add(_planeYz);
        panel.Children.Add(_planeXz);
        panel.Children.Add(Separator());

        _layerCombo.Width = 152;
        _layerCombo.Classes.Add("cad-input");
        _layerCombo.SelectionChanged += (_, _) =>
        {
            if (_refreshingUi ||
                _layerCombo.SelectedItem is not CadLayer layer)
                return;

            _workspace.SetCurrentLayer(layer);
        };
        panel.Children.Add(_layerCombo);
        panel.Children.Add(Separator());

        ConfigureToggle(_snapToggle);
        _snapToggle.IsCheckedChanged += (_, _) =>
        {
            if (_refreshingUi) return;
            _workspace.Snap.Enabled = _snapToggle.IsChecked == true;
            if (!_workspace.Snap.Enabled)
                _workspace.Snap.Clear();
            RefreshInteractionUi();
        };
        panel.Children.Add(_snapToggle);

        ConfigureToggle(_orthoToggle);
        _orthoToggle.IsCheckedChanged += (_, _) =>
        {
            if (_refreshingUi) return;
            _workspace.Drafting.OrthogonalTrackingEnabled =
                _orthoToggle.IsChecked == true;
            _workspace.Tracking.Clear();
            RefreshInteractionUi();
        };
        panel.Children.Add(_orthoToggle);

        ConfigureToggle(_polarToggle);
        _polarToggle.IsCheckedChanged += (_, _) =>
        {
            if (_refreshingUi) return;
            _workspace.Drafting.PolarTrackingEnabled =
                _polarToggle.IsChecked == true;
            _workspace.Tracking.Clear();
            RefreshInteractionUi();
        };
        panel.Children.Add(_polarToggle);
        panel.Children.Add(Separator());

        _finishButton.Classes.Add("cad-compact");
        _finishButton.Click += (_, _) => FinishCurrentTool();
        _cancelButton.Classes.Add("cad-compact");
        _cancelButton.Click += (_, _) => _workspace.Tools.CancelCurrent();
        panel.Children.Add(_finishButton);
        panel.Children.Add(_cancelButton);

        return new Border
        {
            Background = CadTheme.Panel,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = panel
        };
    }

    private Grid BuildWorkspace()
    {
        var grid = new Grid
        {
            Background = CadTheme.WindowBrush
        };
        grid.ColumnDefinitions.Add(_modelColumn);
        grid.ColumnDefinitions.Add(_modelSplitterColumn);
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(_rightSplitterColumn);
        grid.ColumnDefinitions.Add(_rightColumn);

        _modelPanel.Child = BuildModelPanel();
        _modelPanel.Background = CadTheme.Panel;
        _modelPanel.BorderBrush = CadTheme.Border;
        _modelPanel.BorderThickness = new Thickness(0, 0, 1, 0);
        grid.Children.Add(_modelPanel);

        var leftSplitter = new GridSplitter
        {
            Width = 4,
            Background = CadTheme.WindowBrush,
            ResizeDirection = GridResizeDirection.Columns
        };
        Grid.SetColumn(leftSplitter, 1);
        grid.Children.Add(leftSplitter);

        BuildViewportHost();
        Grid.SetColumn(_viewportHost, 2);
        grid.Children.Add(_viewportHost);

        var rightSplitter = new GridSplitter
        {
            Width = 4,
            Background = CadTheme.WindowBrush,
            ResizeDirection = GridResizeDirection.Columns
        };
        Grid.SetColumn(rightSplitter, 3);
        grid.Children.Add(rightSplitter);

        _rightPanel.Child = BuildRightPanel();
        _rightPanel.Background = CadTheme.Panel;
        _rightPanel.BorderBrush = CadTheme.Border;
        _rightPanel.BorderThickness = new Thickness(1, 0, 0, 0);
        Grid.SetColumn(_rightPanel, 4);
        grid.Children.Add(_rightPanel);

        return grid;
    }

    private Control BuildModelPanel()
    {
        var header = PanelHeader(
            CadLanguageManager.Text("Cad.Text.Model", "Model"),
            () => SetModelPanelVisible(false),
            out _modelHeaderText);

        _modelSearch.PlaceholderText = CadLanguageManager.Text(
            "Cad.Text.FilterModel",
            "Filter model tree");
        _modelSearch.Margin = new Thickness(6);
        _modelSearch.Classes.Add("cad-input");
        _modelSearch.TextChanged += (_, _) => RefreshTree();

        _modelTree.Margin = new Thickness(3, 0, 3, 3);
        _modelTree.SelectionChanged += ModelTreeSelectionChanged;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(1, GridUnitType.Star)));

        grid.Children.Add(header);
        Grid.SetRow(_modelSearch, 1);
        grid.Children.Add(_modelSearch);
        Grid.SetRow(_modelTree, 2);
        grid.Children.Add(_modelTree);
        return grid;
    }

    private Control BuildRightPanel()
    {
        var header = PanelHeader(
            CadLanguageManager.Text("Cad.Text.Properties", "Properties"),
            () => SetRightPanelVisible(false),
            out _rightHeaderText);

        var layerScroll = new ScrollViewer
        {
            Content = _layerHost,
            HorizontalScrollBarVisibility =
                ScrollBarVisibility.Disabled
        };
        var propertyScroll = new ScrollViewer
        {
            Content = _propertyHost,
            HorizontalScrollBarVisibility =
                ScrollBarVisibility.Disabled
        };

        _rightTabs.SelectionChanged += (_, _) =>
            RefreshPanelMenuState();

        _rightTabs.ItemsSource = new[]
        {
            new TabItem
            {
                Header = CadLanguageManager.Text(
                    "Cad.Text.Layers",
                    "Layers"),
                Content = layerScroll
            },
            new TabItem
            {
                Header = CadLanguageManager.Text(
                    "Cad.Text.Properties",
                    "Properties"),
                Content = propertyScroll
            }
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(1, GridUnitType.Star)));
        grid.Children.Add(header);
        Grid.SetRow(_rightTabs, 1);
        grid.Children.Add(_rightTabs);
        return grid;
    }

    private void BuildViewportHost()
    {
        _viewportHost.Background = Brushes.Black;
        _viewportHost.ClipToBounds = true;
        _viewportHost.Children.Add(_viewport);

        var overlay = new Canvas
        {
            IsHitTestVisible = false,
            ClipToBounds = true
        };

        _snapAperture.IsVisible = false;
        _snapAperture.Width = 18;
        _snapAperture.Height = 18;
        _snapAperture.Stroke = Brushes.White;
        _snapAperture.StrokeThickness = 1;
        _snapAperture.StrokeDashArray = new AvaloniaList<double> { 2, 2 };
        _snapAperture.Opacity = 0.55;
        overlay.Children.Add(_snapAperture);

        _dynamicValue.Foreground = CadTheme.Text;
        _dynamicValue.FontSize = 11;
        _dynamicHud.IsVisible = false;
        _dynamicHud.MinWidth = 112;
        _dynamicHud.MaxWidth = 300;
        _dynamicHud.Padding = new Thickness(7, 4);
        _dynamicHud.Background = CadTheme.Panel;
        _dynamicHud.BorderBrush = CadTheme.Border;
        _dynamicHud.BorderThickness = new Thickness(1);
        _dynamicHud.CornerRadius = new CornerRadius(2);
        _dynamicHud.Child = _dynamicValue;
        overlay.Children.Add(_dynamicHud);

        _viewportHost.Children.Add(overlay);
        _viewportHost.Children.Add(_toolPanel);
    }

    private Control BuildStatusBar()
    {
        ConfigureStatusText(_toolStatus, 250);
        ConfigureStatusText(_selectionStatus, 90);
        ConfigureStatusText(_historyStatus, 120);
        ConfigureStatusText(_snapStatus, 100);
        ConfigureStatusText(_precisionStatus, 120);
        ConfigureStatusText(_workPlaneStatus, 85);
        ConfigureStatusText(_coordinateStatus, 220);
        _coordinateStatus.TextAlignment = TextAlignment.Right;

        var panel = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(5, 2)
        };

        DockPanel.SetDock(_coordinateStatus, Dock.Right);
        panel.Children.Add(_coordinateStatus);
        DockPanel.SetDock(_workPlaneStatus, Dock.Right);
        panel.Children.Add(_workPlaneStatus);
        DockPanel.SetDock(_precisionStatus, Dock.Right);
        panel.Children.Add(_precisionStatus);
        DockPanel.SetDock(_snapStatus, Dock.Right);
        panel.Children.Add(_snapStatus);
        DockPanel.SetDock(_historyStatus, Dock.Right);
        panel.Children.Add(_historyStatus);
        DockPanel.SetDock(_selectionStatus, Dock.Right);
        panel.Children.Add(_selectionStatus);
        panel.Children.Add(_toolStatus);

        return new Border
        {
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            MinHeight = 27,
            Child = panel
        };
    }

    private void ConfigureViewport()
    {
        _viewport.InteractionFeatures =
            OcctViewportInteractionFeatures.Default;
        _viewport.RectangleSelectionBehavior =
            OcctRectangleSelectionBehavior.Directional;
        _viewport.RectangleSelectionThreshold = 5;
        _viewport.SynchronizeRenderDpi = true;
        _viewport.Cursor = Cursor.Default;
        _viewport.InitialOptions =
            new OcctViewportInitializationOptions
            {
                BackgroundColor = DrawingColor.Black,
                ViewOrientation = OcctViewOrientation.Isometric,
                Projection = OcctProjectionType.Orthographic,
                TriedronVisible = true,
                ViewCubeVisible = false
            };
    }

    private void WireEvents()
    {
        _viewport.EngineRecreated += (_, args) =>
            Ui(() =>
            {
                _workspace.AttachEngine(args.Engine);
                ConfigureEngine(args.Engine);
                _toolStatus.Text = UiFormat(
                    "Cad.Text.ReadyOcct",
                    "Ready - OCCT {0}",
                    OcctEngine.OcctVersion);
                RefreshTree();
                RefreshActionUi();
            });

        _viewport.ErrorOccurred += (_, args) =>
            Ui(() => _toolStatus.Text = args.Exception.Message);

        _viewportInteraction.CoordinateChanged += (_, args) =>
            Ui(() => UpdateCoordinateStatus(args.Value));
        _viewportInteraction.InteractionSettingsChanged += (_, _) =>
            Ui(RefreshInteractionUi);

        _workspace.Selection.Changed += (_, args) =>
            Ui(() => ApplySelection(args));
        _workspace.Subobjects.Changed += (_, args) =>
            Ui(() => ApplySubobjectSelection(args));
        _workspace.Preselection.Changed += (_, _) =>
            Ui(UpdateSelectionStatus);
        _workspace.Document.ChangeSetCommitted += (_, args) =>
            Ui(() => ApplyDocumentChangeSet(args));
        _workspace.Tools.ToolChanged += (_, _) =>
            Ui(() => UpdateToolUi(_workspace.Tools.ActiveTool));
        _workspace.Tools.ToolUpdated += (_, _) =>
            Ui(() => UpdateToolUi(_workspace.Tools.ActiveTool));
        _workspace.Actions.ActionFailed += (_, args) =>
            Ui(() => _toolStatus.Text = args.Exception.Message);
        _workspace.Snap.CurrentChanged += (_, _) =>
            Ui(RefreshSnapStatus);
        _workspace.WorkPlane.Changed += (_, _) =>
            Ui(WorkPlaneChanged);
        _workspace.Drafting.Changed += (_, _) =>
            Ui(DraftingChanged);
        _workspace.History.Changed += (_, _) =>
            Ui(UpdateHistoryUi);
        _workspace.Layers.Changed += (_, args) =>
            Ui(() => LayerManagerChanged(args));
        _workspace.ModifiedChanged += (_, _) =>
            Ui(UpdateWindowTitle);

        CadLanguageManager.Changed += LanguageChanged;
        KeyDown += MainWindowKeyDown;
    }

    private static void ConfigureEngine(OcctEngine engine)
    {
        using var batch = engine.BeginDisplayBatch();
        engine.SetGradientBackground(DrawingColor.Black, DrawingColor.Black);
        engine.SetSelectionTolerance(5);
        engine.SetAntialiasing(true);
        engine.SetFaceBoundariesVisible(true, applyExisting: true);
        engine.SetDefaultMaterial(OcctMaterial.Plastified);
        engine.SetViewCubeVisible(false);
    }

    private void Ui(Action action)
    {
        if (Dispatcher.CheckAccess())
            action();
        else
            Dispatcher.Post(action);
    }

    private void InspectLayer(CadLayer layer)
    {
        _propertyInspector.InspectLayer(layer);
        SetRightPanelVisible(true);
        _rightTabs.SelectedIndex = 1;
    }

    private void SetModelPanelVisible(bool visible)
    {
        _modelPanel.IsVisible = visible;
        _modelColumn.Width = visible
            ? new GridLength(218)
            : new GridLength(0);
        _modelSplitterColumn.Width = visible
            ? new GridLength(4)
            : new GridLength(0);
        RefreshPanelMenuState();
    }

    private void SetRightPanelVisible(bool visible)
    {
        _rightPanel.IsVisible = visible;
        _rightColumn.Width = visible
            ? new GridLength(352)
            : new GridLength(0);
        _rightSplitterColumn.Width = visible
            ? new GridLength(4)
            : new GridLength(0);
        RefreshPanelMenuState();
    }

    private static Border PanelHeader(
        string title,
        Action close,
        out TextBlock text)
    {
        text = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            Foreground = CadTheme.Text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        };
        var button = new Button
        {
            Content = "×",
            Width = 27,
            Height = 27,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        button.Click += (_, _) => close();

        var panel = new DockPanel
        {
            Height = 29,
            Background = CadTheme.PanelAlt
        };
        DockPanel.SetDock(button, Dock.Right);
        panel.Children.Add(button);
        panel.Children.Add(text);

        return new Border
        {
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = panel
        };
    }

    private static Control Separator() =>
        new Border
        {
            Width = 1,
            Height = 20,
            Background = CadTheme.Border,
            Margin = new Thickness(3, 2)
        };

    private static void ConfigureToggle(ToggleButton button)
    {
        button.MinHeight = 25;
        button.Padding = new Thickness(8, 2);
        button.Margin = new Thickness(1);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private void ConfigurePlaneButton(
        ToggleButton button,
        string text,
        CadWorkPlanePreset preset)
    {
        ConfigureToggle(button);
        button.Content = text;
        button.MinWidth = 34;
        button.Tag = preset;
        button.Click += (_, _) =>
        {
            if (_refreshingUi)
                return;

            if (!_workspace.Tools.TryChangeDrawingPlane(preset))
            {
                _toolStatus.Text = UiText(
                    "Cad.Text.WorkPlaneChangeBlocked",
                    "Finish the active tool before changing the work plane.");
            }

            RefreshWorkPlaneUi();
        };
    }

    private static void ConfigureStatusText(
        TextBlock text,
        double minWidth)
    {
        text.MinWidth = minWidth;
        text.Margin = new Thickness(5, 0);
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Foreground = CadTheme.Text;
    }

    private void LanguageChanged(
        object? sender,
        EventArgs e) =>
        Ui(RefreshLanguageUi);

    private void FinishCurrentTool()
    {
        if (_workspace.Tools.ActiveTool is null)
            return;

        _viewportInteraction.FlushPointerMoves();
        _workspace.Tools.SubmitCurrent();
    }

    private void DisposeWorkspace()
    {
        if (_disposed)
            return;

        _disposed = true;
        CadLanguageManager.Changed -= LanguageChanged;
        KeyDown -= MainWindowKeyDown;
        _commandLine.Dispose();
        _layerPanel.Dispose();
        _propertyInspector.Dispose();
        _viewportInteraction.Dispose();
        _workspace.Dispose();
    }
}
