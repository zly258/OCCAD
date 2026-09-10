using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OCCAD;

namespace OCCAD.Wpf;

internal sealed class CadCommandLineController : IDisposable
{
    private readonly CadWorkspace _workspace;
    private readonly CadCommandManager _commands;
    private readonly TextBlock _output;
    private readonly TextBox _input;
    private int _historyIndex;

    public CadCommandLineController(Window owner, CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _commands = new CadCommandManager(workspace);

        if (owner.Content is not DockPanel root)
            throw new InvalidOperationException("The CAD main window root must be a DockPanel.");

        var host = BuildHost(out _output, out _input);
        DockPanel.SetDock(host, Dock.Bottom);
        root.Children.Insert(Math.Max(0, root.Children.Count - 1), host);

        _input.KeyDown += InputKeyDown;
        _workspace.Tools.ToolChanged += ToolUpdated;
        _workspace.Tools.ToolUpdated += ToolUpdated;
        _workspace.Actions.ActionFailed += ActionFailed;
        RefreshPrompt();
    }

    public void RefreshLanguage() => RefreshPrompt();

    public void Dispose()
    {
        _input.KeyDown -= InputKeyDown;
        _workspace.Tools.ToolChanged -= ToolUpdated;
        _workspace.Tools.ToolUpdated -= ToolUpdated;
        _workspace.Actions.ActionFailed -= ActionFailed;
    }

    private void ToolUpdated(object? sender, CadToolChangedEventArgs e) => RefreshPrompt();
    private void ActionFailed(object? sender, CadActionFailedEventArgs e) => SetOutput(e.Exception.Message);

    private static Border BuildHost(out TextBlock output, out TextBox input)
    {
        output = new TextBlock
        {
            Margin = new Thickness(5, 2, 5, 1),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        input = new TextBox
        {
            Margin = new Thickness(5, 0, 5, 3),
            MinHeight = 23,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(output);
        Grid.SetRow(input, 1);
        grid.Children.Add(input);

        return new Border
        {
            BorderBrush = System.Windows.SystemColors.ControlDarkBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = System.Windows.SystemColors.ControlBrush,
            Child = grid
        };
    }

    private void InputKeyDown(object sender, KeyEventArgs e)
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
                if (_input.Text.Length > 0)
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

        if (result.Success && _workspace.Tools.ActiveTool is not null)
            RefreshPrompt();
        else if (!string.IsNullOrWhiteSpace(result.Message))
            SetOutput(CadLanguageManager.CommandMessage(result));
        else if (!string.IsNullOrWhiteSpace(text))
            SetOutput($"> {text.Trim()}");
        else
            RefreshPrompt();
    }

    private void Recall(int direction)
    {
        var history = _commands.History;
        if (history.Count == 0) return;

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, history.Count);
        _input.Text = _historyIndex == history.Count ? string.Empty : history[_historyIndex];
        _input.CaretIndex = _input.Text.Length;
    }

    private void RefreshPrompt()
    {
        var tool = _workspace.Tools.ActiveTool;
        SetOutput(tool?.Prompt is { } prompt
            ? CadLanguageManager.ToolPrompt(prompt)
            : CadLanguageManager.Text("Cad.Text.Ready", "Ready"));
        _historyIndex = _commands.History.Count;
    }

    private void SetOutput(string text)
    {
        _output.Text = text;
        _output.ToolTip = text;
    }
}
