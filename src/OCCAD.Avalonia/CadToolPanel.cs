using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

internal sealed class CadToolPanel : Border
{
    private const string PrecisionLengthId = "$precision.length";
    private const string PrecisionAngleId = "$precision.angle";
    private const string PrecisionFactorId = "$precision.factor";

    private readonly CadWorkspace _workspace;
    private readonly TextBlock _title;
    private readonly StackPanel _content;
    private readonly Button _close;
    private readonly Button _finish;
    private readonly Button _cancel;
    private readonly TextBlock _planeState;
    private readonly CheckBox _planeLock;
    private readonly Dictionary<string, Control> _editors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> _locks =
        new(StringComparer.OrdinalIgnoreCase);

    private CadTool? _tool;
    private string _schema = string.Empty;
    private bool _refreshing;
    private bool _hiddenByUser;

    public CadToolPanel(CadWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        Width = 326;
        MaxHeight = 610;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(12);
        Background = CadTheme.Panel;
        BorderBrush = CadTheme.Border;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        IsVisible = false;

        _title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0)
        };

        _close = new Button
        {
            Content = "×",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _close.Click += (_, _) => HidePanel();

        var header = new DockPanel
        {
            Height = 30,
            Background = CadTheme.Header
        };
        DockPanel.SetDock(_close, Dock.Right);
        header.Children.Add(_close);
        header.Children.Add(_title);

        _content = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(10)
        };

        _finish = CompactButton();
        _finish.Click += (_, _) => FinishTool();
        _cancel = CompactButton();
        _cancel.Click += (_, _) => _workspace.Tools.CancelCurrent();

        _planeState = new TextBlock
        {
            Foreground = CadTheme.Muted,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        _planeLock = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        _planeLock.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing) return;
            _workspace.WorkPlane.SetToolPlaneFixed(
                _planeLock.IsChecked == true);
            RefreshWorkPlaneState();
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _content
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.Children.Add(header);
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Child = root;
    }

    public bool CanDisplayCurrentTool => _tool is not null;
    public bool IsPanelVisible => _tool is not null && IsVisible;
    public event EventHandler? PanelVisibilityChanged;

    public void SetTool(CadTool? tool)
    {
        if (tool is null || !CanShow(tool))
        {
            _tool = null;
            _hiddenByUser = false;
            _schema = string.Empty;
            _editors.Clear();
            _locks.Clear();
            _content.Children.Clear();
            SetPanelVisible(false);
            return;
        }

        var changed = !ReferenceEquals(_tool, tool);
        _tool = tool;

        if (changed)
        {
            _hiddenByUser = false;
            _schema = BuildSchema(tool);
            Rebuild(tool);
        }
        else
        {
            var next = BuildSchema(tool);
            if (!string.Equals(_schema, next, StringComparison.Ordinal))
            {
                _schema = next;
                Rebuild(tool);
            }
            else
            {
                RefreshValues(tool);
            }
        }

        SetPanelVisible(!_hiddenByUser);
    }

    public void HidePanel()
    {
        if (_tool is null) return;
        _hiddenByUser = true;
        SetPanelVisible(false);
    }

    public void ShowPanel()
    {
        if (_tool is null) return;
        _hiddenByUser = false;
        SetPanelVisible(true);
    }

    private void SetPanelVisible(bool visible)
    {
        if (IsVisible == visible)
            return;

        IsVisible = visible;
        PanelVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshLanguage()
    {
        ToolTip.SetTip(
            _close,
            CadLanguageManager.Text(
                "Cad.Text.CloseToolPanel",
                "Hide tool parameters"));
        if (_tool is not null)
            Rebuild(_tool);
    }

    private static bool CanShow(CadTool tool) =>
        tool.ParameterPanel is not null ||
        tool.PrecisionInputs != CadPrecisionInputKind.None;

    private static string BuildSchema(CadTool tool)
    {
        var parameters = tool.ParameterPanel?.Parameters ?? [];
        return $"{tool.Stage}:{tool.PrecisionInputs}:{tool.PrecisionLengthLabel}:{tool.PrecisionAngleLabel}:{tool.PrecisionFactorLabel}|" +
               string.Join(
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
            _title.Text = CadLanguageManager.Text(
                $"Cad.ToolPanel.{tool.Id}.Title",
                fallbackTitle);
            ToolTip.SetTip(
                _close,
                CadLanguageManager.Text(
                    "Cad.Text.CloseToolPanel",
                    "Hide tool parameters"));

            _content.Children.Clear();
            _editors.Clear();
            _locks.Clear();

            AddWorkPlaneState();
            AddPrecisionEditors(tool);

            if (tool.ParameterPanel is { } panel)
            {
                foreach (var parameter in panel.Parameters)
                    AddParameterRow(tool, parameter);
            }

            var buttons = new Grid
            {
                Margin = new Thickness(0, 6, 0, 0),
                ColumnSpacing = 6
            };
            buttons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            buttons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            _finish.Content = tool.CanFinish
                ? CadLanguageManager.Text("Cad.Text.Finish", "Finish")
                : CadLanguageManager.Text("Cad.Text.Accept", "Accept");
            _finish.IsEnabled = tool.CanFinish || tool.CanCommitCurrentStage;
            _cancel.Content = CadLanguageManager.Text("Cad.Text.Cancel", "Cancel");
            _cancel.IsEnabled = tool.CanCancel;
            buttons.Children.Add(_finish);
            Grid.SetColumn(_cancel, 1);
            buttons.Children.Add(_cancel);
            _content.Children.Add(buttons);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void AddWorkPlaneState()
    {
        _planeLock.Content = CadLanguageManager.Text(
            "Cad.Text.FixToolPlane",
            "Fix tool plane");

        var row = new Grid
        {
            Margin = new Thickness(0, 0, 0, 5),
            ColumnSpacing = 8
        };
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.Children.Add(_planeState);
        Grid.SetColumn(_planeLock, 1);
        row.Children.Add(_planeLock);
        _content.Children.Add(row);
        RefreshWorkPlaneState();
    }

    private void RefreshWorkPlaneState()
    {
        _planeState.Text =
            _workspace.WorkPlane.ToolPlaneFixed
                ? CadLanguageManager.Text(
                    "Cad.Text.ToolPlaneFixed",
                    "Tool plane fixed")
                : _workspace.WorkPlane.UserPlaneLocked
                    ? CadLanguageManager.Text(
                        "Cad.Text.UserPlaneLocked",
                        "User work plane locked")
                    : CadLanguageManager.Text(
                        "Cad.Text.ToolPlaneActive",
                        "Tool plane active");
        _planeLock.IsChecked =
            _workspace.WorkPlane.ToolPlaneFixed;
        _planeLock.IsEnabled =
            _workspace.WorkPlane.ToolPlane is not null;
    }

    private void AddPrecisionEditors(CadTool tool)
    {
        var inputs = tool.PrecisionInputs;

        if ((inputs & CadPrecisionInputKind.Length) != 0)
        {
            AddLockedNumericRow(
                PrecisionLengthId,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Length",
                    tool.PrecisionLengthLabel),
                _workspace.WorkPlane.LengthLockEnabled
                    ? _workspace.WorkPlane.LockedLength
                    : null);
        }

        if ((inputs & CadPrecisionInputKind.Angle) != 0)
        {
            AddLockedNumericRow(
                PrecisionAngleId,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Angle",
                    tool.PrecisionAngleLabel),
                _workspace.WorkPlane.AngleLockEnabled
                    ? _workspace.WorkPlane.LockedAngleDegrees
                    : null);
        }

        if ((inputs & CadPrecisionInputKind.Factor) != 0)
        {
            AddLockedNumericRow(
                PrecisionFactorId,
                CadLanguageManager.Text(
                    $"Cad.Precision.{tool.Id}.{tool.Stage}.Factor",
                    tool.PrecisionFactorLabel),
                _workspace.Precision.Factor);
        }
    }

    private void AddLockedNumericRow(
        string id,
        string label,
        double? value)
    {
        var editor = CreateTextEditor(
            id,
            value?.ToString("0.###", CultureInfo.CurrentCulture) ??
            string.Empty);
        var toggle = CreateLockToggle(id, editor, value.HasValue);
        _editors[id] = editor;
        _locks[id] = toggle;

        var panel = new Grid { ColumnSpacing = 6 };
        panel.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        panel.Children.Add(editor);
        Grid.SetColumn(toggle, 1);
        panel.Children.Add(toggle);
        AddRow(label, panel);
    }

    private void AddParameterRow(
        CadTool tool,
        CadToolParameterDescriptor parameter)
    {
        var label = CadLanguageManager.Text(
            $"Cad.Parameter.{tool.Id}.{parameter.Id}",
            parameter.Label);

        if (parameter is CadOptionalDoubleToolParameterDescriptor optional)
        {
            var editor = CreateTextEditor(
                parameter.Id,
                optional.Value?.ToString(
                    "0.###",
                    CultureInfo.CurrentCulture) ?? string.Empty);
            var toggle = CreateLockToggle(
                parameter.Id,
                editor,
                optional.Value.HasValue);
            _editors[parameter.Id] = editor;
            _locks[parameter.Id] = toggle;

            var panel = new Grid { ColumnSpacing = 6 };
            panel.ColumnDefinitions.Add(
                new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            panel.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Auto));
            panel.Children.Add(editor);
            Grid.SetColumn(toggle, 1);
            panel.Children.Add(toggle);
            AddRow(label, panel);
            return;
        }

        var valueEditor = CreateEditor(tool, parameter);
        _editors[parameter.Id] = valueEditor;
        AddRow(label, valueEditor);
    }

    private Control CreateEditor(
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
                        Apply(
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
                    if (!_refreshing &&
                        editor.SelectedItem is ChoiceItem selected)
                        Apply(parameter.Id, selected.Value);
                };
                return editor;
            }

            case CadIntegerToolParameterDescriptor value:
                return CreateTextEditor(
                    parameter.Id,
                    value.Value.ToString(CultureInfo.CurrentCulture));

            case CadDoubleToolParameterDescriptor value:
                return CreateTextEditor(
                    parameter.Id,
                    value.Value.ToString(
                        "0.###",
                        CultureInfo.CurrentCulture));

            case CadStringToolParameterDescriptor value:
                return CreateTextEditor(parameter.Id, value.Value);

            default:
                throw new NotSupportedException(
                    $"Unsupported tool parameter descriptor: {parameter.GetType().Name}");
        }
    }

    private TextBox CreateTextEditor(
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
            if (e.Key != Key.Enter) return;
            Apply(id, editor.Text ?? string.Empty);
            e.Handled = true;
        };
        editor.LostFocus += (_, _) =>
            Apply(id, editor.Text ?? string.Empty);
        return editor;
    }

    private CheckBox CreateLockToggle(
        string id,
        TextBox editor,
        bool locked)
    {
        var toggle = new CheckBox
        {
            Content = CadLanguageManager.Text(
                "Cad.Text.Lock",
                "Lock"),
            IsChecked = locked,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = id
        };

        toggle.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing) return;

            if (toggle.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(editor.Text))
                {
                    RefreshValues(_tool!);
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

    private void AddRow(
        string labelText,
        Control editor)
    {
        var row = new Grid
        {
            ColumnSpacing = 7,
            Margin = new Thickness(0, 1)
        };
        row.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(112)));
        row.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var label = new TextBlock
        {
            Text = labelText,
            Foreground = CadTheme.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        row.Children.Add(label);
        Grid.SetColumn(editor, 1);
        row.Children.Add(editor);
        _content.Children.Add(row);
    }

    private void RefreshValues(CadTool tool)
    {
        _refreshing = true;
        try
        {
            RefreshWorkPlaneState();

            RefreshLockedEditor(
                PrecisionLengthId,
                _workspace.WorkPlane.LengthLockEnabled
                    ? _workspace.WorkPlane.LockedLength
                    : null);
            RefreshLockedEditor(
                PrecisionAngleId,
                _workspace.WorkPlane.AngleLockEnabled
                    ? _workspace.WorkPlane.LockedAngleDegrees
                    : null);
            RefreshLockedEditor(
                PrecisionFactorId,
                _workspace.Precision.Factor);

            if (tool.ParameterPanel is { } panel)
            {
                foreach (var parameter in panel.Parameters)
                {
                    if (!_editors.TryGetValue(parameter.Id, out var editor))
                        continue;

                    switch (parameter, editor)
                    {
                        case (CadBooleanToolParameterDescriptor value, CheckBox checkBox):
                            checkBox.IsChecked = value.Value;
                            break;

                        case (CadChoiceToolParameterDescriptor value, ComboBox comboBox):
                            if (!comboBox.IsDropDownOpen &&
                                comboBox.ItemsSource is IEnumerable<ChoiceItem> choices)
                            {
                                comboBox.SelectedItem =
                                    choices.FirstOrDefault(item =>
                                        string.Equals(
                                            item.Value,
                                            value.Value,
                                            StringComparison.OrdinalIgnoreCase));
                            }
                            break;

                        case (CadIntegerToolParameterDescriptor value, TextBox textBox):
                            if (!textBox.IsFocused)
                                textBox.Text =
                                    value.Value.ToString(
                                        CultureInfo.CurrentCulture);
                            break;

                        case (CadDoubleToolParameterDescriptor value, TextBox textBox):
                            if (!textBox.IsFocused)
                                textBox.Text =
                                    value.Value.ToString(
                                        "0.###",
                                        CultureInfo.CurrentCulture);
                            break;

                        case (CadOptionalDoubleToolParameterDescriptor value, TextBox textBox):
                            if (!textBox.IsFocused)
                            {
                                textBox.Text = value.Value?.ToString(
                                    "0.###",
                                    CultureInfo.CurrentCulture) ??
                                    string.Empty;
                            }
                            if (_locks.TryGetValue(
                                    parameter.Id,
                                    out var parameterLock))
                                parameterLock.IsChecked = value.Value.HasValue;
                            break;

                        case (CadStringToolParameterDescriptor value, TextBox textBox):
                            if (!textBox.IsFocused)
                                textBox.Text = value.Value;
                            break;
                    }
                }
            }

            _finish.Content = tool.CanFinish
                ? CadLanguageManager.Text(
                    "Cad.Text.Finish",
                    "Finish")
                : CadLanguageManager.Text(
                    "Cad.Text.Accept",
                    "Accept");
            _finish.IsEnabled =
                tool.CanFinish || tool.CanCommitCurrentStage;
            _cancel.Content = CadLanguageManager.Text(
                "Cad.Text.Cancel",
                "Cancel");
            _cancel.IsEnabled = tool.CanCancel;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshLockedEditor(
        string id,
        double? value)
    {
        if (_editors.TryGetValue(id, out var element) &&
            element is TextBox textBox &&
            !textBox.IsFocused)
        {
            textBox.Text =
                value?.ToString(
                    "0.###",
                    CultureInfo.CurrentCulture) ??
                string.Empty;
        }

        if (_locks.TryGetValue(id, out var toggle))
            toggle.IsChecked = value.HasValue;
    }

    private void Apply(
        string id,
        string value)
    {
        if (_refreshing || _tool is null) return;

        var success = id switch
        {
            PrecisionLengthId =>
                ApplyPrecision(
                    value,
                    CadPrecisionInputKind.Length),
            PrecisionAngleId =>
                ApplyPrecision(
                    value,
                    CadPrecisionInputKind.Angle),
            PrecisionFactorId =>
                ApplyPrecision(
                    value,
                    CadPrecisionInputKind.Factor),
            _ => _tool.TrySetParameter(id, value)
        };

        if (success)
        {
            SetTool(_tool);
            return;
        }

        RefreshValues(_tool);
    }

    private bool ApplyPrecision(
        string value,
        CadPrecisionInputKind kind)
    {
        if (_tool is null)
            return false;

        if (string.IsNullOrWhiteSpace(value))
        {
            switch (kind)
            {
                case CadPrecisionInputKind.Length:
                    _workspace.WorkPlane.LengthLockEnabled = false;
                    _workspace.WorkPlane.LockedLength = 0;
                    return true;

                case CadPrecisionInputKind.Angle:
                    _workspace.WorkPlane.AngleLockEnabled = false;
                    _workspace.WorkPlane.LockedAngleDegrees = 0;
                    return true;

                case CadPrecisionInputKind.Factor:
                    _workspace.Precision.ResetFactor();
                    return true;
            }
        }

        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var number) &&
            !double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number))
            return false;

        if (!double.IsFinite(number))
            return false;
        if (kind != CadPrecisionInputKind.Angle &&
            number <= 0)
            return false;

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

        return _workspace.Precision.Apply(_tool, input);
    }

    private void FinishTool()
    {
        if (_tool is null) return;

        if (_tool.CanFinish)
            _workspace.Tools.FinishCurrent();
        else if (_tool.CanCommitCurrentStage)
            _workspace.Tools.CommitCurrentStage();
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

    private sealed record ChoiceItem(
        string Value,
        string Label)
    {
        public override string ToString() => Label;
    }
}
