using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Persistent CAD command surface. It owns the active command/stage prompt,
/// command input, completion/history navigation and command feedback. Parsing
/// and stage commits remain in CadCommandManager/CadToolManager so the UI does
/// not duplicate interaction rules.
/// </summary>
internal sealed class CadCommandLineController : IDisposable
{
    private readonly CadWorkspace _workspace;
    private readonly CadCommandManager _commands;
    private readonly Panel _host;
    private readonly TextBlock _prompt = new();
    private readonly TextBlock _label = new();
    private readonly TextBox _input = new();
    private readonly TextBlock _feedback = new();
    private int _historyIndex = -1;
    private string _historyDraft = string.Empty;

    public CadCommandLineController(
        CadWorkspace workspace,
        Panel host)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _commands = CadCommandManager.ForWorkspace(_workspace);
        _host = host ?? throw new ArgumentNullException(nameof(host));

        BuildUi();
        _workspace.Actions.ActionFailed += ActionFailed;
        _workspace.Tools.ToolChanged += ToolChanged;
        _workspace.Tools.ToolUpdated += ToolChanged;
        RefreshToolPrompt();
    }

    public void FocusInput()
    {
        _input.Focus();
        _input.SelectAll();
    }

    public void ShowFeedback(string? message)
    {
        _feedback.Text = message?.Trim() ?? string.Empty;
        ToolTip.SetTip(
            _feedback,
            string.IsNullOrWhiteSpace(_feedback.Text)
                ? null
                : _feedback.Text);
    }

    public void RefreshLanguage()
    {
        _label.Text = CadLanguageManager.Text(
            "Cad.Text.Command",
            "Command");
        _input.PlaceholderText = CadLanguageManager.Text(
            "Cad.Text.CommandPrompt",
            "Type command, coordinate or parameter; Enter executes, Tab completes");
        RefreshToolPrompt();
    }

    public void Dispose()
    {
        _workspace.Actions.ActionFailed -= ActionFailed;
        _workspace.Tools.ToolChanged -= ToolChanged;
        _workspace.Tools.ToolUpdated -= ToolChanged;
        _input.KeyDown -= InputKeyDown;
        _host.Children.Clear();
    }

    private void BuildUi()
    {
        _host.Children.Clear();
        _host.IsVisible = true;

        _prompt.FontSize = CadTheme.SmallFontSize;
        _prompt.FontWeight = FontWeight.SemiBold;
        _prompt.Foreground = CadTheme.Text;
        _prompt.VerticalAlignment = VerticalAlignment.Center;
        _prompt.TextTrimming = TextTrimming.CharacterEllipsis;

        var promptHost = new Border
        {
            Height = 20,
            Padding = new Thickness(7, 0),
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = _prompt
        };

        var inputGrid = new Grid
        {
            Height = 28,
            Background = CadTheme.Surface,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        _label.FontSize = CadTheme.SmallFontSize;
        _label.FontWeight = FontWeight.SemiBold;
        _label.Foreground = CadTheme.Text;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.Margin = new Thickness(7, 0, 6, 0);
        Grid.SetColumn(_label, 0);
        inputGrid.Children.Add(_label);

        _input.Classes.Add("cad-input");
        _input.MinHeight = 22;
        _input.Height = 22;
        _input.Margin = new Thickness(0, 2);
        _input.Padding = new Thickness(5, 0);
        _input.BorderThickness = new Thickness(1);
        _input.KeyDown += InputKeyDown;
        Grid.SetColumn(_input, 1);
        inputGrid.Children.Add(_input);

        _feedback.FontSize = CadTheme.SmallFontSize;
        _feedback.Foreground = CadTheme.Muted;
        _feedback.VerticalAlignment = VerticalAlignment.Center;
        _feedback.Margin = new Thickness(8, 0, 8, 0);
        _feedback.MaxWidth = 360;
        _feedback.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(_feedback, 2);
        inputGrid.Children.Add(_feedback);

        var root = new Grid
        {
            Background = CadTheme.Surface
        };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.Children.Add(promptHost);
        Grid.SetRow(inputGrid, 1);
        root.Children.Add(inputGrid);

        _host.Children.Add(new Border
        {
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = root
        });
        RefreshLanguage();
    }

    private void ToolChanged(object? sender, CadToolChangedEventArgs e) =>
        RefreshToolPrompt();

    private void RefreshToolPrompt()
    {
        var commandLabel = CadLanguageManager.Text("Cad.Text.Command", "Command");
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null)
        {
            _prompt.Text = $"{commandLabel}: {CadLanguageManager.Text("Cad.Text.Ready", "Ready")}";
            ToolTip.SetTip(_prompt, _prompt.Text);
            return;
        }

        var toolName = CadLanguageManager.Text(tool.LocalizationKey, tool.DisplayName);
        var stepLabel = CadLanguageManager.Text("Cad.Text.Step", "Step");
        var prompt = tool.Prompt is { } activePrompt
            ? CadLanguageManager.ToolPrompt(activePrompt)
            : toolName;

        _prompt.Text = $"{commandLabel}: {toolName}  ·  {stepLabel} {tool.Stage + 1}  —  {prompt}";
        ToolTip.SetTip(_prompt, _prompt.Text);
    }

    private void InputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                if (_workspace.Tools.ActiveTool is not null)
                    ShowResult(_commands.Execute("ESC"));
                _input.Text = string.Empty;
                ResetHistoryNavigation();
                e.Handled = true;
                return;

            case Key.Up:
                NavigateHistory(-1);
                e.Handled = true;
                return;

            case Key.Down:
                NavigateHistory(1);
                e.Handled = true;
                return;

            case Key.Tab:
                CompleteCommand();
                e.Handled = true;
                return;

            case Key.Enter:
            {
                var input = _input.Text;
                _input.Text = string.Empty;
                ResetHistoryNavigation();
                ShowResult(_commands.Execute(input));
                e.Handled = true;
                return;
            }
        }
    }

    private void NavigateHistory(int direction)
    {
        var history = _commands.History;
        if (history.Count == 0)
            return;

        if (_historyIndex < 0)
        {
            if (direction > 0)
                return;

            _historyDraft = _input.Text ?? string.Empty;
            _historyIndex = history.Count - 1;
        }
        else if (direction < 0)
        {
            if (_historyIndex > 0)
                _historyIndex--;
        }
        else if (_historyIndex < history.Count - 1)
        {
            _historyIndex++;
        }
        else
        {
            _historyIndex = -1;
        }

        _input.Text = _historyIndex >= 0
            ? history[_historyIndex]
            : _historyDraft;
        _input.CaretIndex = _input.Text?.Length ?? 0;
    }

    private void CompleteCommand()
    {
        var text = _input.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return;

        var matches = _commands.Complete(text)
            .Take(12)
            .ToArray();
        if (matches.Length == 0)
        {
            ShowFeedback(
                CadLanguageManager.Text(
                    "Cad.Text.NoMatchingCommand",
                    "No matching command"));
            return;
        }

        if (matches.Length == 1)
        {
            _input.Text = matches[0];
            _input.CaretIndex = matches[0].Length;
            ShowFeedback(null);
            return;
        }

        var common = LongestCommonPrefix(matches);
        if (common.Length > text.Length)
        {
            _input.Text = common;
            _input.CaretIndex = common.Length;
        }

        ShowFeedback(string.Join("  ", matches.Take(6)));
    }

    private void ResetHistoryNavigation()
    {
        _historyIndex = -1;
        _historyDraft = string.Empty;
    }

    private static string LongestCommonPrefix(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return string.Empty;

        var prefix = values[0];
        for (var index = 1; index < values.Count && prefix.Length > 0; index++)
        {
            var other = values[index];
            var length = Math.Min(prefix.Length, other.Length);
            var shared = 0;
            while (shared < length &&
                   char.ToUpperInvariant(prefix[shared]) == char.ToUpperInvariant(other[shared]))
            {
                shared++;
            }

            prefix = prefix[..shared];
        }

        return prefix;
    }

    private void ShowResult(CadCommandResult result)
    {
        var message = CadLanguageManager.CommandMessage(result);
        if (!string.IsNullOrWhiteSpace(message))
        {
            ShowFeedback(message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ActionId) &&
            _workspace.Actions.Find(result.ActionId) is { } action)
        {
            ShowFeedback(
                CadLanguageManager.Text(
                    $"Cad.Action.{action.Id}",
                    action.DisplayName));
            return;
        }

        ShowFeedback(
            result.Kind switch
            {
                CadCommandResultKind.Repeated => CadLanguageManager.Text(
                    "Cad.Text.CommandRepeated",
                    "Repeated last command"),
                CadCommandResultKind.Canceled => CadLanguageManager.Text(
                    "Cad.Text.CommandCanceled",
                    "Canceled"),
                _ => string.Empty
            });
    }

    private void ActionFailed(object? sender, CadActionFailedEventArgs e)
    {
        ShowFeedback(
            string.Format(
                CadLanguageManager.Text(
                    "Cad.Text.CommandFailed",
                    "{0} failed"),
                CadLanguageManager.Text(
                    $"Cad.Action.{e.Action.Id}",
                    e.Action.DisplayName)));
    }
}
