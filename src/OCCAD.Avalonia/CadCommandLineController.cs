using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

internal sealed class CadCommandLineController : IDisposable
{
    private readonly CadWorkspace _workspace;
    private readonly CadCommandManager _commands;
    private readonly TextBlock _output;
    private readonly TextBox _input;
    private int _historyIndex;

    public CadCommandLineController(
        CadWorkspace workspace,
        Panel host)
    {
        _workspace = workspace ??
            throw new ArgumentNullException(nameof(workspace));
        ArgumentNullException.ThrowIfNull(host);

        _commands = new CadCommandManager(workspace);
        var shell = BuildHost(out _output, out _input);
        host.Children.Add(shell);

        _input.KeyDown += InputKeyDown;
        _workspace.Tools.ToolChanged += ToolUpdated;
        _workspace.Tools.ToolUpdated += ToolUpdated;
        _workspace.Actions.ActionFailed += ActionFailed;
        RefreshPrompt();
    }

    public void FocusInput() => _input.Focus();

    public void RefreshLanguage() => RefreshPrompt();

    public void Dispose()
    {
        _input.KeyDown -= InputKeyDown;
        _workspace.Tools.ToolChanged -= ToolUpdated;
        _workspace.Tools.ToolUpdated -= ToolUpdated;
        _workspace.Actions.ActionFailed -= ActionFailed;
    }

    private void ToolUpdated(
        object? sender,
        CadToolChangedEventArgs e) =>
        RefreshPrompt();

    private void ActionFailed(
        object? sender,
        CadActionFailedEventArgs e) =>
        SetOutput(e.Exception.Message);

    private static Border BuildHost(
        out TextBlock output,
        out TextBox input)
    {
        output = new TextBlock
        {
            Margin = new Thickness(8, 3, 8, 2),
            FontSize = CadTheme.FontSize,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = CadTheme.Muted
        };

        input = new TextBox
        {
            Margin = new Thickness(7, 0, 7, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            PlaceholderText = ">"
        };
        input.Classes.Add("cad-input");

        var grid = new Grid();
        grid.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));
        grid.Children.Add(output);
        Grid.SetRow(input, 1);
        grid.Children.Add(input);

        return new Border
        {
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = CadTheme.Panel,
            Child = grid
        };
    }

    private void InputKeyDown(
        object? sender,
        KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ExecuteInput();
                e.Handled = true;
                break;

            case Key.Up:
                Recall(-1);
                e.Handled = true;
                break;

            case Key.Down:
                Recall(1);
                e.Handled = true;
                break;

            case Key.Escape:
                if (!string.IsNullOrEmpty(_input.Text))
                    _input.Clear();
                else if (_workspace.Tools.ActiveTool is not null)
                    _workspace.Tools.CancelCurrent();

                RefreshPrompt();
                e.Handled = true;
                break;
        }
    }

    private void ExecuteInput()
    {
        var text = _input.Text;
        var result = _commands.Execute(text);
        _input.Clear();
        _historyIndex = _commands.History.Count;

        if (result.Success &&
            _workspace.Tools.ActiveTool is not null)
        {
            RefreshPrompt();
        }
        else if (!string.IsNullOrWhiteSpace(result.Message))
        {
            SetOutput(
                CadLanguageManager.CommandMessage(result));
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            SetOutput($"> {text.Trim()}");
        }
        else
        {
            RefreshPrompt();
        }
    }

    private void Recall(int direction)
    {
        var history = _commands.History;
        if (history.Count == 0)
            return;

        _historyIndex = Math.Clamp(
            _historyIndex + direction,
            0,
            history.Count);
        _input.Text = _historyIndex == history.Count
            ? string.Empty
            : history[_historyIndex];
        _input.CaretIndex = _input.Text?.Length ?? 0;
    }

    private void RefreshPrompt()
    {
        var tool = _workspace.Tools.ActiveTool;
        SetOutput(
            tool?.Prompt is { } prompt
                ? CadLanguageManager.ToolPrompt(prompt)
                : CadLanguageManager.Text(
                    "Cad.Text.Ready",
                    "Ready"));
        _historyIndex = _commands.History.Count;
    }

    private void SetOutput(string text)
    {
        _output.Text = text;
        ToolTip.SetTip(_output, text);
    }
}
