using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OCCAD;

namespace OCCAD.Wpf;

internal abstract class CadToolPanel : Border
{
    private readonly TextBlock _title = new();
    private readonly ContentControl _body = new();
    private readonly Button _close = new();
    private CadTool? _tool;

    protected CadToolPanel(CadWorkspace workspace)
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        MinWidth = 308;
        MaxWidth = 360;
        MaxHeight = 620;
        Background = System.Windows.SystemColors.WindowBrush;
        BorderBrush = System.Windows.SystemColors.ControlDarkBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(3);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(12);
        Visibility = Visibility.Collapsed;
        System.Windows.Controls.Panel.SetZIndex(this, 20);

        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.Margin = new Thickness(8, 0, 8, 0);
        _title.FontWeight = FontWeights.SemiBold;

        _close.Content = "×";
        _close.Width = 28;
        _close.MinHeight = 26;
        _close.Margin = new Thickness(0);
        _close.Padding = new Thickness(0);
        _close.Background = System.Windows.Media.Brushes.Transparent;
        _close.BorderThickness = new Thickness(0);
        _close.ToolTip = CadLanguageManager.Text(
            "Cad.Text.CloseToolPanel",
            "Close and cancel current tool");
        _close.Click += (_, _) => Workspace.Tools.CancelCurrent();

        var headerContent = new DockPanel();
        DockPanel.SetDock(_close, Dock.Right);
        headerContent.Children.Add(_close);
        headerContent.Children.Add(_title);

        var header = new Border
        {
            Background = System.Windows.SystemColors.ControlLightBrush,
            BorderBrush = System.Windows.SystemColors.ControlDarkBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            MinHeight = 30,
            Child = headerContent
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(header);
        Grid.SetRow(_body, 1);
        root.Children.Add(_body);

        Child = root;
    }

    protected CadWorkspace Workspace { get; }
    protected CadTool? Tool => _tool;

    protected UIElement? PanelContent
    {
        get => _body.Content as UIElement;
        set => _body.Content = value;
    }

    public void SetTool(CadTool? tool)
    {
        if (tool is null || !CanShow(tool))
        {
            _tool = null;
            OnToolCleared();
            Visibility = Visibility.Collapsed;
            return;
        }

        var changed = !ReferenceEquals(_tool, tool);
        _tool = tool;
        if (changed)
            OnToolChanged(tool);
        else
            OnToolUpdated(tool);

        Visibility = Visibility.Visible;
    }

    public void RefreshLanguage()
    {
        _close.ToolTip = CadLanguageManager.Text(
            "Cad.Text.CloseToolPanel",
            "Close and cancel current tool");
        if (_tool is not null)
            OnLanguageChanged(_tool);
    }

    protected void SetPanelTitle(string title)
    {
        _title.Text = title;
        ToolTip = title;
    }

    protected abstract bool CanShow(CadTool tool);
    protected abstract void OnToolChanged(CadTool tool);
    protected abstract void OnToolUpdated(CadTool tool);
    protected abstract void OnToolCleared();
    protected virtual void OnLanguageChanged(CadTool tool) => OnToolChanged(tool);
}

internal sealed class CadParameterToolPanel : CadToolPanel
{
    private const string PrecisionLengthId = "$precision.length";
    private const string PrecisionAngleId = "$precision.angle";
    private const string PrecisionFactorId = "$precision.factor";

    private readonly StackPanel _content = new();
    private readonly Dictionary<string, FrameworkElement> _editors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> _locks =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Button _finish = new();
    private readonly Button _cancel = new();
    private readonly UniformGrid _buttons = new() { Columns = 2 };
    private readonly DockPanel _workPlaneRow = new() { Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _workPlaneState = new();
    private readonly TextBlock _promptText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly CheckBox _workPlaneLock = new();
    private string _schemaKey = string.Empty;
    private bool _refreshing;

    public CadParameterToolPanel(CadWorkspace workspace)
        : base(workspace)
    {
        _finish.MinWidth = 78;
        _finish.Margin = new Thickness(0, 8, 4, 0);
        _finish.Click += (_, _) => FinishTool();
        _cancel.MinWidth = 78;
        _cancel.Margin = new Thickness(4, 8, 0, 0);
        _cancel.Click += (_, _) => Workspace.Tools.CancelCurrent();

        _workPlaneLock.VerticalAlignment = VerticalAlignment.Center;
        _workPlaneLock.Margin = new Thickness(8, 0, 0, 0);
        _workPlaneLock.Click += (_, _) =>
        {
            if (_refreshing) return;
            Workspace.WorkPlane.SetPlaneLocked(_workPlaneLock.IsChecked == true);
            RefreshWorkPlaneState();
        };

        PanelContent = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _content,
            Padding = new Thickness(10)
        };
    }

    protected override bool CanShow(CadTool tool) =>
        tool.IsActive;

    protected override void OnToolChanged(CadTool tool)
    {
        _schemaKey = BuildSchemaKey(tool);
        Rebuild(tool);
    }

    protected override void OnToolUpdated(CadTool tool)
    {
        var schemaKey = BuildSchemaKey(tool);
        if (!string.Equals(_schemaKey, schemaKey, StringComparison.Ordinal))
        {
            _schemaKey = schemaKey;
            Rebuild(tool);
            return;
        }
        RefreshValues(tool);
    }

    protected override void OnToolCleared()
    {
        _schemaKey = string.Empty;
        _editors.Clear();
        _locks.Clear();
        _content.Children.Clear();
    }

    protected override void OnLanguageChanged(CadTool tool) => Rebuild(tool);

    private static string BuildSchemaKey(CadTool tool)
    {
        var parameters = tool.ParameterPanel?.Parameters ?? [];
        return $"{tool.Stage}:{tool.PrecisionInputs}:{tool.PrecisionLengthLabel}:{tool.PrecisionAngleLabel}:{tool.PrecisionFactorLabel}|" + string.Join(
            "|",
            parameters.Select(parameter =>
                parameter is CadChoiceToolParameterDescriptor choice
                    ? $"{parameter.Id}:{parameter.GetType().Name}:{string.Join(',', choice.Choices)}"
                    : $"{parameter.Id}:{parameter.GetType().Name}"));
    }

    private void Rebuild(CadTool tool)
    {
        _refreshing = true;
        try
        {
            var fallbackTitle = tool.ParameterPanel?.Title ?? tool.DisplayName;
            SetPanelTitle(CadLanguageManager.Text($"Cad.ToolPanel.{tool.Id}.Title", fallbackTitle));
            _content.Children.Clear();
            _editors.Clear();
            _locks.Clear();

            AddWorkPlaneState();
            RefreshPrompt(tool);
            _content.Children.Add(_promptText);
            AddPrecisionEditors(tool);

            if (tool.ParameterPanel is { } panel)
            {
                foreach (var parameter in panel.Parameters)
                    AddParameterRow(tool, parameter);
            }

            _buttons.Children.Clear();
            _finish.Content = tool.CanFinish
                ? CadLanguageManager.Text("Cad.Text.Finish", "Finish")
                : CadLanguageManager.Text("Cad.Text.Accept", "Accept");
            _finish.IsEnabled = tool.CanFinish || tool.CanCommitCurrentStage;
            _cancel.Content = CadLanguageManager.Text("Cad.Text.Cancel", "Cancel");
            _cancel.IsEnabled = tool.CanCancel;
            _buttons.Children.Add(_finish);
            _buttons.Children.Add(_cancel);
            _content.Children.Add(_buttons);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void AddWorkPlaneState()
    {
        _workPlaneState.VerticalAlignment = VerticalAlignment.Center;
        _workPlaneState.Opacity = 0.72;

        _workPlaneLock.Content = CadLanguageManager.Text(
            "Cad.Text.LockWorkPlane",
            "Lock work plane");

        _workPlaneRow.Children.Clear();
        DockPanel.SetDock(_workPlaneLock, Dock.Right);
        _workPlaneRow.Children.Add(_workPlaneLock);
        _workPlaneRow.Children.Add(_workPlaneState);
        _content.Children.Add(_workPlaneRow);
        RefreshWorkPlaneState();
    }

    private void RefreshWorkPlaneState()
    {
        _workPlaneState.Text = !Workspace.WorkPlane.IsActive
            ? CadLanguageManager.Text("Cad.Text.ToolPlaneInactive", "Work plane inactive")
            : Workspace.WorkPlane.IsPlaneLocked
                ? CadLanguageManager.Text("Cad.Text.ToolPlaneLocked", "Work plane locked")
                : CadLanguageManager.Text("Cad.Text.ToolPlaneActive", "Work plane active");
        _workPlaneLock.IsEnabled = Workspace.WorkPlane.IsActive;
        _workPlaneLock.IsChecked = Workspace.WorkPlane.IsPlaneLocked;
    }

    private void AddPrecisionEditors(CadTool tool)
    {
        var inputs = tool.PrecisionInputs;
        if ((inputs & CadPrecisionInputKind.Length) != 0)
            AddLockedNumericRow(
                PrecisionLengthId,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Length",
                    tool.PrecisionLengthLabel),
                Workspace.WorkPlane.LengthLockEnabled
                    ? Workspace.WorkPlane.LockedLength
                    : null);
        if ((inputs & CadPrecisionInputKind.Angle) != 0)
            AddLockedNumericRow(
                PrecisionAngleId,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Angle",
                    tool.PrecisionAngleLabel),
                Workspace.WorkPlane.AngleLockEnabled
                    ? Workspace.WorkPlane.LockedAngleDegrees
                    : null);
        if ((inputs & CadPrecisionInputKind.Factor) != 0)
            AddLockedNumericRow(
                PrecisionFactorId,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Factor",
                    tool.PrecisionFactorLabel),
                Workspace.Precision.Factor);
    }

    private void AddLockedNumericRow(string id, string label, double? value)
    {
        var editor = CreateTextEditor(
            id,
            value?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty);
        var lockToggle = CreateLockToggle(id, editor, value.HasValue);
        _editors[id] = editor;
        _locks[id] = lockToggle;
        AddRow(label, CreateLockedEditor(editor, lockToggle));
    }

    private void AddParameterRow(CadTool tool, CadToolParameterDescriptor parameter)
    {
        var label = CadLanguageManager.Text(
            $"Cad.Parameter.{tool.Id}.{parameter.Id}",
            parameter.Label);

        if (parameter is CadOptionalDoubleToolParameterDescriptor optional)
        {
            var editor = CreateTextEditor(
                parameter.Id,
                optional.Value?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty);
            var lockToggle = CreateLockToggle(parameter.Id, editor, optional.Value.HasValue);
            _editors[parameter.Id] = editor;
            _locks[parameter.Id] = lockToggle;
            AddRow(label, CreateLockedEditor(editor, lockToggle));
            return;
        }

        var valueEditor = CreateEditor(tool, parameter);
        _editors[parameter.Id] = valueEditor;
        AddRow(label, valueEditor);
    }

    private CheckBox CreateLockToggle(string id, TextBox editor, bool locked)
    {
        var toggle = new CheckBox
        {
            Content = CadLanguageManager.Text("Cad.Text.Lock", "Lock"),
            IsChecked = locked,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = id
        };
        toggle.Click += (_, _) =>
        {
            if (_refreshing) return;
            if (toggle.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(editor.Text))
                {
                    System.Media.SystemSounds.Beep.Play();
                    RefreshValues(Tool!);
                    return;
                }
                Apply(id, editor.Text);
            }
            else
            {
                Apply(id, string.Empty);
            }
        };
        return toggle;
    }

    private static FrameworkElement CreateLockedEditor(TextBox editor, CheckBox lockToggle)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(editor, 0);
        panel.Children.Add(editor);
        Grid.SetColumn(lockToggle, 1);
        panel.Children.Add(lockToggle);
        return panel;
    }

    private void AddRow(string labelText, FrameworkElement editor)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(188) });

        var label = new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);
        editor.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(editor, 1);
        row.Children.Add(editor);
        _content.Children.Add(row);
    }

    private void RefreshPrompt(CadTool tool) =>
        _promptText.Text = tool.Prompt is { } prompt ? CadLanguageManager.ToolPrompt(prompt) : string.Empty;

    private void RefreshValues(CadTool tool)
    {
        _refreshing = true;
        try
        {
            RefreshWorkPlaneState();
            RefreshPrompt(tool);
            RefreshLockedEditor(
                PrecisionLengthId,
                Workspace.WorkPlane.LengthLockEnabled
                    ? Workspace.WorkPlane.LockedLength
                    : null);
            RefreshLockedEditor(
                PrecisionAngleId,
                Workspace.WorkPlane.AngleLockEnabled
                    ? Workspace.WorkPlane.LockedAngleDegrees
                    : null);
            RefreshLockedEditor(PrecisionFactorId, Workspace.Precision.Factor);

            if (tool.ParameterPanel is { } panel)
            {
                foreach (var parameter in panel.Parameters)
                {
                    if (!_editors.TryGetValue(parameter.Id, out var editor)) continue;
                    switch (parameter, editor)
                    {
                        case (CadBooleanToolParameterDescriptor value, CheckBox checkBox):
                            checkBox.IsChecked = value.Value;
                            break;
                        case (CadChoiceToolParameterDescriptor value, ComboBox comboBox):
                            if (!comboBox.IsDropDownOpen)
                                comboBox.SelectedValue = value.Value;
                            break;
                        case (CadIntegerToolParameterDescriptor value, TextBox textBox):
                            if (!textBox.IsKeyboardFocusWithin)
                                textBox.Text = value.Value.ToString(CultureInfo.CurrentCulture);
                            break;
                        case (CadDoubleToolParameterDescriptor value, TextBox textBox):
                            if (!textBox.IsKeyboardFocusWithin)
                                textBox.Text = value.Value.ToString("0.###", CultureInfo.CurrentCulture);
                            break;
                        case (CadOptionalDoubleToolParameterDescriptor value, TextBox textBox):
                            if (!textBox.IsKeyboardFocusWithin)
                                textBox.Text = value.Value?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty;
                            if (_locks.TryGetValue(parameter.Id, out var parameterLock))
                                parameterLock.IsChecked = value.Value.HasValue;
                            break;
                    }
                }
            }

            _finish.Content = tool.CanFinish
                ? CadLanguageManager.Text("Cad.Text.Finish", "Finish")
                : CadLanguageManager.Text("Cad.Text.Accept", "Accept");
            _finish.IsEnabled = tool.CanFinish || tool.CanCommitCurrentStage;
            _cancel.Content = CadLanguageManager.Text("Cad.Text.Cancel", "Cancel");
            _cancel.IsEnabled = tool.CanCancel;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshLockedEditor(string id, double? value)
    {
        if (_editors.TryGetValue(id, out var element) &&
            element is TextBox textBox &&
            !textBox.IsKeyboardFocusWithin)
            textBox.Text = value?.ToString("0.###", CultureInfo.CurrentCulture) ?? string.Empty;
        if (_locks.TryGetValue(id, out var toggle))
            toggle.IsChecked = value.HasValue;
    }

    private FrameworkElement CreateEditor(CadTool tool, CadToolParameterDescriptor parameter)
    {
        switch (parameter)
        {
            case CadStringToolParameterDescriptor value:
                return CreateTextEditor(parameter.Id, value.Value);
            case CadBooleanToolParameterDescriptor value:
                {
                    var editor = new CheckBox
                    {
                        IsChecked = value.Value,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    editor.Click += (_, _) => Apply(parameter.Id, editor.IsChecked == true ? "true" : "false");
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
                        DisplayMemberPath = nameof(ChoiceItem.Label),
                        SelectedValuePath = nameof(ChoiceItem.Value),
                        SelectedValue = value.Value,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    editor.SelectionChanged += (_, _) =>
                    {
                        if (!_refreshing && editor.SelectedValue is string selected)
                            Apply(parameter.Id, selected);
                    };
                    return editor;
                }
            case CadIntegerToolParameterDescriptor value:
                return CreateTextEditor(parameter.Id, value.Value.ToString(CultureInfo.CurrentCulture));
            case CadDoubleToolParameterDescriptor value:
                return CreateTextEditor(parameter.Id, value.Value.ToString("0.###", CultureInfo.CurrentCulture));
            default:
                throw new NotSupportedException(
                    $"Unsupported tool parameter descriptor: {parameter.GetType().Name}");
        }
    }

    private TextBox CreateTextEditor(string id, string value)
    {
        var editor = new TextBox
        {
            Text = value,
            Tag = id,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        editor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Apply(id, editor.Text);
            e.Handled = true;
        };
        editor.LostKeyboardFocus += (_, _) => Apply(id, editor.Text);
        return editor;
    }

    private void Apply(string id, string value)
    {
        if (_refreshing || Tool is null) return;

        var success = id switch
        {
            PrecisionLengthId => ApplyPrecision(value, CadPrecisionInputKind.Length),
            PrecisionAngleId => ApplyPrecision(value, CadPrecisionInputKind.Angle),
            PrecisionFactorId => ApplyPrecision(value, CadPrecisionInputKind.Factor),
            _ => Tool.TrySetParameter(id, value)
        };

        if (success)
        {
            OnToolUpdated(Tool);
            return;
        }

        System.Media.SystemSounds.Beep.Play();
        RefreshValues(Tool);
    }

    private bool ApplyPrecision(string value, CadPrecisionInputKind kind)
    {
        if (Tool is null) return false;
        if (string.IsNullOrWhiteSpace(value))
        {
            switch (kind)
            {
                case CadPrecisionInputKind.Length:
                    Workspace.WorkPlane.LengthLockEnabled = false;
                    Workspace.WorkPlane.LockedLength = 0.0;
                    return true;
                case CadPrecisionInputKind.Angle:
                    Workspace.WorkPlane.AngleLockEnabled = false;
                    Workspace.WorkPlane.LockedAngleDegrees = 0.0;
                    return true;
                case CadPrecisionInputKind.Factor:
                    Workspace.Precision.ResetFactor();
                    return true;
            }
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number) &&
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            return false;
        if (!double.IsFinite(number)) return false;
        if (kind != CadPrecisionInputKind.Angle && number <= 0.0) return false;

        var input = kind switch
        {
            CadPrecisionInputKind.Length => new CadPrecisionInput(Length: number),
            CadPrecisionInputKind.Angle => new CadPrecisionInput(AngleDegrees: number),
            CadPrecisionInputKind.Factor => new CadPrecisionInput(Factor: number),
            _ => default
        };
        return Workspace.Precision.Apply(Tool, input);
    }

    private void FinishTool()
    {
        if (Tool is null) return;
        if (Tool.CanFinish)
            Workspace.Tools.FinishCurrent();
        else if (Tool.CanCommitCurrentStage)
            Workspace.Tools.CommitCurrentStage();
    }

    private sealed record ChoiceItem(string Value, string Label);
}

