using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Compact persistent CAD command line. It intentionally executes registered
/// actions instead of duplicating tool logic in the UI layer.
/// </summary>
internal sealed class CadCommandLineController : IDisposable
{
    private readonly CadWorkspace _workspace;
    private readonly Panel _host;
    private readonly TextBlock _label = new();
    private readonly TextBox _input = new();
    private readonly TextBlock _feedback = new();

    public CadCommandLineController(
        CadWorkspace workspace,
        Panel host)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        BuildUi();
        _workspace.Actions.ActionStarted += ActionsChanged;
        _workspace.Actions.ActionFinished += ActionsChanged;
        _workspace.Actions.ActionFailed += ActionFailed;
    }

    public void FocusInput()
    {
        _input.Focus();
        _input.SelectAll();
    }

    public void RefreshLanguage()
    {
        _label.Text = CadLanguageManager.Text("Cad.Text.Command", "Command");
        _input.Watermark = CadLanguageManager.Text(
            "Cad.Text.CommandWatermark",
            "Type a command and press Enter");
    }

    public void Dispose()
    {
        _workspace.Actions.ActionStarted -= ActionsChanged;
        _workspace.Actions.ActionFinished -= ActionsChanged;
        _workspace.Actions.ActionFailed -= ActionFailed;
        _input.KeyDown -= InputKeyDown;
        _host.Children.Clear();
    }

    private void BuildUi()
    {
        _host.Children.Clear();
        _host.IsVisible = true;

        var grid = new Grid
        {
            Height = 28,
            Background = CadTheme.Surface,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        var topBorder = new Border
        {
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid
        };

        _label.FontSize = CadTheme.SmallFontSize;
        _label.FontWeight = FontWeight.SemiBold;
        _label.Foreground = CadTheme.Text;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.Margin = new Thickness(7, 0, 6, 0);
        Grid.SetColumn(_label, 0);
        grid.Children.Add(_label);

        _input.Classes.Add("cad-input");
        _input.MinHeight = 22;
        _input.Height = 22;
        _input.Margin = new Thickness(0, 2);
        _input.Padding = new Thickness(5, 0);
        _input.BorderThickness = new Thickness(1);
        _input.KeyDown += InputKeyDown;
        Grid.SetColumn(_input, 1);
        grid.Children.Add(_input);

        _feedback.FontSize = CadTheme.SmallFontSize;
        _feedback.Foreground = CadTheme.Muted;
        _feedback.VerticalAlignment = VerticalAlignment.Center;
        _feedback.Margin = new Thickness(8, 0, 8, 0);
        _feedback.MaxWidth = 320;
        _feedback.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(_feedback, 2);
        grid.Children.Add(_feedback);

        _host.Children.Add(topBorder);
        RefreshLanguage();
    }

    private void InputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_workspace.Tools.ActiveTool is not null)
                _workspace.Tools.CancelCurrent();
            _input.Clear();
            _feedback.Text = string.Empty;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
            return;

        var command = _input.Text?.Trim();
        _input.Clear();
        e.Handled = true;

        if (string.IsNullOrWhiteSpace(command))
        {
            _feedback.Text = _workspace.Actions.ExecuteLast()
                ? CadLanguageManager.Text("Cad.Text.CommandRepeated", "Repeated last command")
                : CadLanguageManager.Text("Cad.Text.NoPreviousCommand", "No previous command");
            return;
        }

        var action = ResolveAction(command);
        if (action is null)
        {
            _feedback.Text = string.Format(
                CadLanguageManager.Text("Cad.Text.UnknownCommand", "Unknown command: {0}"),
                command);
            return;
        }

        if (!_workspace.Actions.CanExecute(action.Id))
        {
            _feedback.Text = string.Format(
                CadLanguageManager.Text("Cad.Text.CommandUnavailable", "Command unavailable: {0}"),
                action.DisplayName);
            return;
        }

        if (_workspace.Actions.Execute(action.Id))
            _feedback.Text = action.DisplayName;
    }

    private CadAction? ResolveAction(string command)
    {
        var direct = _workspace.Actions.Find(command);
        if (direct is not null)
            return direct;

        var normalized = Normalize(command);
        return _workspace.Actions.Actions.FirstOrDefault(action =>
            Normalize(action.DisplayName) == normalized ||
            Normalize(action.Id) == normalized);
    }

    private void ActionsChanged(object? sender, CadActionEventArgs e)
    {
        _feedback.Text = e.Action.DisplayName;
    }

    private void ActionFailed(object? sender, CadActionFailedEventArgs e)
    {
        _feedback.Text = string.Format(
            CadLanguageManager.Text("Cad.Text.CommandFailed", "{0} failed"),
            e.Action.DisplayName);
    }

    private static string Normalize(string value) =>
        new(value
            .Where(character => !char.IsWhiteSpace(character) && character is not '-' and not '_')
            .Select(char.ToUpperInvariant)
            .ToArray());
}
