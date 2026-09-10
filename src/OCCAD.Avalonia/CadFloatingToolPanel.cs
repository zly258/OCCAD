using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Persistent non-modal parameter panel for the active CAD tool. Its lifetime
/// follows the active tool rather than the current input kind, so selection,
/// point, precision and confirmation stages keep one continuous surface.
/// </summary>
internal sealed class CadFloatingToolPanel : Border, IDisposable
{
    private const string ExactPointId = "$point.coordinate";
    private const string PrecisionLengthId = "$precision.length";
    private const string PrecisionAngleId = "$precision.angle";
    private const string PrecisionFactorId = "$precision.factor";

    private readonly CadWorkspace _workspace;
    private readonly Border _header;
    private readonly TextBlock _title;
    private readonly StackPanel _content;
    private readonly Button _back;
    private readonly Button _accept;
    private readonly Button _cancel;
    private CadTool? _tool;
    private bool _hiddenByUser;
    private bool _refreshing;
    private bool _dragging;
    private Point _dragPointerStart;
    private Point _dragPanelStart;
    private bool _disposed;

    public CadFloatingToolPanel(CadWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        Width = CadTheme.ToolPanelWidth;
        MaxHeight = 540;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(8);
        Background = CadTheme.Surface;
        BorderBrush = CadTheme.BorderStrong;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(0);
        IsVisible = false;

        _title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            Foreground = CadTheme.Text,
            Margin = new Thickness(7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var close = new Button
        {
            Content = "×",
            Width = CadTheme.PanelHeaderHeight,
            Height = CadTheme.PanelHeaderHeight,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = CadTheme.Muted
        };
        close.Click += (_, _) =>
        {
            HidePanel();
            UserVisibilityRequested?.Invoke(false);
        };

        var headerPanel = new DockPanel
        {
            Height = CadTheme.PanelHeaderHeight
        };
        DockPanel.SetDock(close, Dock.Right);
        headerPanel.Children.Add(close);
        headerPanel.Children.Add(_title);

        _header = new Border
        {
            Background = CadTheme.Header,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = headerPanel
        };
        _header.PointerPressed += HeaderPointerPressed;
        _header.PointerMoved += HeaderPointerMoved;
        _header.PointerReleased += HeaderPointerReleased;
        _header.PointerCaptureLost += (_, _) => _dragging = false;

        _content = new StackPanel
        {
            Spacing = 0,
            Margin = new Thickness(5)
        };

        _back = CompactButton();
        _back.Click += (_, _) => _workspace.Tools.StepBackCurrent();
        _accept = CompactButton();
        _accept.Classes.Add("cad-primary");
        _accept.Click += (_, _) => SubmitCurrent();
        _cancel = CompactButton();
        _cancel.Click += (_, _) => _workspace.Tools.CancelCurrent();

        var scroll = new ScrollViewer
        {
            Content = _content,
            HorizontalScrollBarVisibility =
                global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(
            new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.Children.Add(_header);
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Child = root;

        _workspace.Tools.ToolChanged += ToolChanged;
        _workspace.Tools.ToolUpdated += ToolChanged;
        CadLanguageManager.Changed += LanguageChanged;
        SetTool(_workspace.Tools.ActiveTool);
    }

    public bool CanDisplayCurrentTool => _tool is not null;
    public bool IsPanelVisible => _tool is not null && IsVisible;

    public event EventHandler? PanelVisibilityChanged;
    public event Action<bool>? UserVisibilityRequested;

    public void ShowPanel()
    {
        if (_tool is null)
            return;
        _hiddenByUser = false;
        SetPanelVisible(true);
    }

    public void HidePanel()
    {
        if (_tool is null)
            return;
        _hiddenByUser = true;
        SetPanelVisible(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _workspace.Tools.ToolChanged -= ToolChanged;
        _workspace.Tools.ToolUpdated -= ToolChanged;
        CadLanguageManager.Changed -= LanguageChanged;
        _header.PointerPressed -= HeaderPointerPressed;
        _header.PointerMoved -= HeaderPointerMoved;
        _header.PointerReleased -= HeaderPointerReleased;
    }

    private void ToolChanged(object? sender, CadToolChangedEventArgs e) =>
        SetTool(e.Tool);

    private void LanguageChanged(object? sender, EventArgs e) =>
        SetTool(_workspace.Tools.ActiveTool, forceRebuild: true);

    private void SetTool(CadTool? tool, bool forceRebuild = false)
    {
        if (_disposed)
            return;

        var changed = !ReferenceEquals(_tool, tool);
        _tool = tool;

        if (tool is null)
        {
            _hiddenByUser = false;
            _content.Children.Clear();
            SetPanelVisible(false);
            return;
        }

        if (changed)
            _hiddenByUser = false;

        if (changed || forceRebuild || IsVisible)
            Rebuild(tool);

        SetPanelVisible(!_hiddenByUser);
    }

    private void SetPanelVisible(bool visible)
    {
        if (IsVisible == visible)
            return;
        IsVisible = visible;
        PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild(CadTool tool)
    {
        if (_refreshing || !tool.IsActive)
            return;

        _refreshing = true;
        try
        {
            _title.Text = CadLanguageManager.Text(
                $"Cad.ToolPanel.{tool.Id}.Title",
                CadLanguageManager.Text(tool.LocalizationKey, tool.DisplayName));
            _content.Children.Clear();

            AddPrompt(tool);
            if (tool.CurrentStep.InputKind == CadToolInputKind.Point)
                AddExactPointEditor(tool);
            AddPrecisionEditors(tool);
            AddParameterEditors(tool);
            AddCommandButtons(tool);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void AddPrompt(CadTool tool)
    {
        var prompt = tool.Prompt is { } activePrompt
            ? CadLanguageManager.ToolPrompt(activePrompt)
            : CadLanguageManager.Text(tool.LocalizationKey, tool.DisplayName);
        var step =
            $"{CadLanguageManager.Text("Cad.Text.Step", "Step")} {tool.Stage + 1}";

        var panel = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(0, 0, 0, 5)
        };
        panel.Children.Add(new TextBlock
        {
            Text = step,
            FontSize = CadTheme.CaptionFontSize,
            Foreground = CadTheme.Muted
        });
        panel.Children.Add(new TextBlock
        {
            Text = prompt,
            TextWrapping = TextWrapping.Wrap,
            Foreground = CadTheme.Text,
            Margin = new Thickness(0, 0, 2, 0)
        });

        _content.Children.Add(new Border
        {
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4),
            Child = panel
        });
    }

    private void AddExactPointEditor(CadTool tool)
    {
        var editor = new TextBox
        {
            Tag = ExactPointId,
            PlaceholderText = CadLanguageManager.Text(
                "Cad.Text.ExactPointHint",
                "100,200 | @500,0 | @1000<30")
        };
        editor.Classes.Add("cad-input");
        editor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            CommitExactPoint(tool, editor);
            e.Handled = true;
        };

        var apply = CompactButton();
        apply.Content = CadLanguageManager.Text("Cad.Text.SetPoint", "Set");
        apply.MinWidth = 50;
        apply.Click += (_, _) => CommitExactPoint(tool, editor);

        var value = new Grid { ColumnSpacing = 3 };
        value.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        value.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        value.Children.Add(editor);
        Grid.SetColumn(apply, 1);
        value.Children.Add(apply);

        AddRow(CadLanguageManager.Text("Cad.Text.Coordinate", "Coordinate"), value);
    }

    private void CommitExactPoint(CadTool tool, TextBox editor)
    {
        if (_refreshing ||
            !ReferenceEquals(_workspace.Tools.ActiveTool, tool) ||
            tool.CurrentStep.InputKind != CadToolInputKind.Point)
            return;

        var reference =
            tool.PrecisionReferencePoint ??
            _workspace.LastResolvedPoint?.Point ??
            _workspace.WorkPlane.Origin;

        if (!CadCoordinateInputParser.TryParse(
                editor.Text,
                reference,
                _workspace.WorkPlane,
                out var input) ||
            !_workspace.Tools.CommitPoint(input.Point))
        {
            editor.SelectAll();
            return;
        }

        SetTool(_workspace.Tools.ActiveTool, forceRebuild: true);
    }

    private void AddPrecisionEditors(CadTool tool)
    {
        var inputs = tool.PrecisionInputs;
        if ((inputs & CadPrecisionInputKind.Length) != 0)
        {
            AddPrecisionRow(
                tool,
                PrecisionLengthId,
                CadPrecisionInputKind.Length,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Length",
                    tool.PrecisionLengthLabel),
                _workspace.Drafting.LengthLockEnabled
                    ? _workspace.Drafting.LockedLength
                    : null);
        }

        if ((inputs & CadPrecisionInputKind.Angle) != 0)
        {
            AddPrecisionRow(
                tool,
                PrecisionAngleId,
                CadPrecisionInputKind.Angle,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Angle",
                    tool.PrecisionAngleLabel),
                _workspace.Drafting.AngleLockEnabled
                    ? _workspace.Drafting.LockedAngleDegrees
                    : null);
        }

        if ((inputs & CadPrecisionInputKind.Factor) != 0)
        {
            AddPrecisionRow(
                tool,
                PrecisionFactorId,
                CadPrecisionInputKind.Factor,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Factor",
                    tool.PrecisionFactorLabel),
                _workspace.Precision.Factor);
        }
    }

    private void AddPrecisionRow(
        CadTool tool,
        string id,
        CadPrecisionInputKind kind,
        string label,
        double? value)
    {
        var editor = new TextBox
        {
            Tag = id,
            Text = value?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty
        };
        editor.Classes.Add("cad-input");
        editor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            ApplyPrecisionText(tool, kind, editor.Text);
            e.Handled = true;
        };

        var locked = new CheckBox
        {
            Content = CadLanguageManager.Text("Cad.Text.Lock", "Lock"),
            IsChecked = value.HasValue,
            VerticalAlignment = VerticalAlignment.Center
        };
        locked.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing)
                return;

            if (locked.IsChecked == true)
                ApplyPrecisionText(tool, kind, editor.Text);
            else
                ApplyPrecisionText(tool, kind, string.Empty);
        };

        var valuePanel = new Grid { ColumnSpacing = 3 };
        valuePanel.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        valuePanel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        valuePanel.Children.Add(editor);
        Grid.SetColumn(locked, 1);
        valuePanel.Children.Add(locked);
        AddRow(label, valuePanel);
    }

    private void ApplyPrecisionText(
        CadTool tool,
        CadPrecisionInputKind kind,
        string? text)
    {
        if (_refreshing ||
            !ReferenceEquals(_workspace.Tools.ActiveTool, tool))
            return;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (!_workspace.Precision.ClearLock(tool, kind))
                SetTool(tool, forceRebuild: true);
            return;
        }

        if (!CadValueTextConverter.TryParseFiniteDouble(text, out var number) ||
            (kind != CadPrecisionInputKind.Angle && number <= 0))
        {
            SetTool(tool, forceRebuild: true);
            return;
        }

        var input = kind switch
        {
            CadPrecisionInputKind.Length =>
                new CadPrecisionInput(Length: number),
            CadPrecisionInputKind.Angle =>
                new CadPrecisionInput(AngleDegrees: number),
            CadPrecisionInputKind.Factor =>
                new CadPrecisionInput(Factor: number),
            _ => default
        };

        if (!_workspace.Precision.Apply(tool, input))
            SetTool(tool, forceRebuild: true);
    }

    private void AddParameterEditors(CadTool tool)
    {
        if (tool.ParameterPanel is not { } panel)
            return;

        foreach (var parameter in panel.Parameters)
            AddParameterRow(tool, parameter);
    }

    private void AddParameterRow(
        CadTool tool,
        CadToolParameterDescriptor parameter)
    {
        var label = CadLanguageManager.Text(
            $"Cad.Parameter.{tool.Id}.{parameter.Id}",
            parameter.Label);
        AddRow(label, CreateParameterEditor(tool, parameter));
    }

    private Control CreateParameterEditor(
        CadTool tool,
        CadToolParameterDescriptor parameter)
    {
        switch (parameter)
        {
            case CadBooleanToolParameterDescriptor value:
            {
                var editor = new CheckBox
                {
                    IsChecked = value.Value,
                    VerticalAlignment = VerticalAlignment.Center
                };
                editor.IsCheckedChanged += (_, _) =>
                {
                    if (!_refreshing)
                        ApplyParameter(
                            tool,
                            parameter.Id,
                            editor.IsChecked == true ? "true" : "false");
                };
                return editor;
            }

            case CadChoiceToolParameterDescriptor value:
            {
                var choices = value.Choices
                    .Select(choice => new ChoiceItem(
                        choice,
                        CadLanguageManager.Text(
                            $"Cad.Parameter.{tool.Id}.{parameter.Id}.{choice}",
                            choice)))
                    .ToArray();
                var editor = new ComboBox
                {
                    ItemsSource = choices,
                    SelectedItem = choices.FirstOrDefault(item =>
                        string.Equals(
                            item.Value,
                            value.Value,
                            StringComparison.OrdinalIgnoreCase))
                };
                editor.Classes.Add("cad-input");
                editor.SelectionChanged += (_, _) =>
                {
                    if (!_refreshing && editor.SelectedItem is ChoiceItem selected)
                        ApplyParameter(tool, parameter.Id, selected.Value);
                };
                return editor;
            }

            case CadIntegerToolParameterDescriptor value:
                return ParameterTextBox(
                    tool,
                    parameter.Id,
                    value.Value.ToString(CultureInfo.CurrentCulture));

            case CadDoubleToolParameterDescriptor value:
                return ParameterTextBox(
                    tool,
                    parameter.Id,
                    value.Value.ToString("0.###", CultureInfo.CurrentCulture));

            case CadOptionalDoubleToolParameterDescriptor value:
                return ParameterTextBox(
                    tool,
                    parameter.Id,
                    value.Value?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty);

            case CadStringToolParameterDescriptor value:
                return ParameterTextBox(tool, parameter.Id, value.Value);

            default:
                return new TextBlock
                {
                    Text = parameter.GetType().Name,
                    Foreground = CadTheme.Muted,
                    VerticalAlignment = VerticalAlignment.Center
                };
        }
    }

    private TextBox ParameterTextBox(
        CadTool tool,
        string id,
        string value)
    {
        var editor = new TextBox
        {
            Text = value,
            Tag = id
        };
        editor.Classes.Add("cad-input");
        editor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            ApplyParameter(tool, id, editor.Text ?? string.Empty);
            e.Handled = true;
        };
        editor.LostFocus += (_, _) =>
            ApplyParameter(tool, id, editor.Text ?? string.Empty);
        return editor;
    }

    private void ApplyParameter(
        CadTool tool,
        string id,
        string value)
    {
        if (_refreshing ||
            !ReferenceEquals(_workspace.Tools.ActiveTool, tool))
            return;

        tool.TrySetParameter(id, value);
        SetTool(tool, forceRebuild: true);
    }

    private void AddCommandButtons(CadTool tool)
    {
        var buttons = new Grid
        {
            Margin = new Thickness(0, 5, 0, 0),
            ColumnSpacing = 3
        };
        buttons.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        buttons.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        buttons.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        _back.Content = CadLanguageManager.Text("Cad.Text.StepBack", "Back");
        _back.IsEnabled = tool.CanStepBack;
        _accept.Content = tool.CanCommitCurrentStage
            ? CadLanguageManager.Text("Cad.Text.Accept", "Accept")
            : CadLanguageManager.Text("Cad.Text.Finish", "Finish");
        _accept.IsEnabled = tool.CanCommitCurrentStage || tool.CanFinish;
        _cancel.Content = CadLanguageManager.Text("Cad.Text.Cancel", "Cancel");
        _cancel.IsEnabled = tool.CanCancel;

        Detach(_back);
        Detach(_accept);
        Detach(_cancel);
        buttons.Children.Add(_back);
        Grid.SetColumn(_accept, 1);
        buttons.Children.Add(_accept);
        Grid.SetColumn(_cancel, 2);
        buttons.Children.Add(_cancel);
        _content.Children.Add(buttons);
    }

    private void SubmitCurrent()
    {
        if (_workspace.Tools.ActiveTool is null)
            return;
        _workspace.Tools.SubmitCurrent();
    }

    private void AddRow(string labelText, Control editor)
    {
        var row = new Grid
        {
            MinHeight = CadTheme.PropertyRowHeight,
            ColumnSpacing = 0
        };
        row.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(CadTheme.ToolLabelWidth)));
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1)));
        row.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        row.Children.Add(new TextBlock
        {
            Text = labelText,
            Foreground = CadTheme.Text,
            Margin = new Thickness(5, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var separator = new Border
        {
            Width = 1,
            Background = CadTheme.Border
        };
        Grid.SetColumn(separator, 1);
        row.Children.Add(separator);

        editor.Margin = new Thickness(3, 0, 0, 0);
        Grid.SetColumn(editor, 2);
        row.Children.Add(editor);

        _content.Children.Add(new Border
        {
            Background = CadTheme.Surface,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row
        });
    }

    private void HeaderPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (e.Source is Button ||
            Parent is not Control parent ||
            !e.GetCurrentPoint(_header).Properties.IsLeftButtonPressed)
            return;

        _dragging = true;
        _dragPointerStart = e.GetPosition(parent);
        _dragPanelStart = new Point(Margin.Left, Margin.Top);
        e.Pointer.Capture(_header);
        e.Handled = true;
    }

    private void HeaderPointerMoved(
        object? sender,
        PointerEventArgs e)
    {
        if (!_dragging || Parent is not Control parent)
            return;

        var current = e.GetPosition(parent);
        var width = Bounds.Width > 0 ? Bounds.Width : Width;
        var height = Bounds.Height > 0 ? Bounds.Height : 120;
        var maxX = Math.Max(0, parent.Bounds.Width - width);
        var maxY = Math.Max(0, parent.Bounds.Height - height);
        var x = Math.Clamp(
            _dragPanelStart.X + current.X - _dragPointerStart.X,
            0,
            maxX);
        var y = Math.Clamp(
            _dragPanelStart.Y + current.Y - _dragPointerStart.Y,
            0,
            maxY);
        Margin = new Thickness(x, y, 0, 0);
        e.Handled = true;
    }

    private void HeaderPointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private static void Detach(Control control)
    {
        if (control.Parent is Panel parent)
            parent.Children.Remove(control);
    }

    private static Button CompactButton()
    {
        var button = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        return button;
    }

    private sealed record ChoiceItem(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
