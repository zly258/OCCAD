using Avalonia.Controls;
using Avalonia.Input;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Lightweight direct-entry surface for the initial-release CAD shell.
/// It keeps precision input available without restoring a permanent command
/// line or pointer-adjacent HUD. Parsing and stage semantics remain in Core.
/// </summary>
internal sealed class CadDirectInputController
{
    private const int MaxInputLength = 96;

    private readonly CadWorkspace _workspace;
    private readonly Control _viewport;
    private readonly Action _refreshStatus;
    private readonly Action<string?> _publishFeedback;
    private readonly CadCommandManager _commands;

    private string _buffer = string.Empty;
    private string? _message;
    private string? _toolId;
    private int _stage = -1;

    public CadDirectInputController(
        CadWorkspace workspace,
        Control viewport,
        Action refreshStatus,
        Action<string?> publishFeedback)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _refreshStatus = refreshStatus ?? throw new ArgumentNullException(nameof(refreshStatus));
        _publishFeedback = publishFeedback ?? throw new ArgumentNullException(nameof(publishFeedback));
        _commands = CadCommandManager.ForWorkspace(_workspace);

        _viewport.KeyDown += ViewportKeyDown;
        _viewport.TextInput += ViewportTextInput;
    }

    public bool HasInput => _buffer.Length > 0;
    public string Buffer => _buffer;
    public string? Message => _message;

    public void Synchronize()
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null)
        {
            ClearCore();
            return;
        }

        if (!string.Equals(_toolId, tool.Id, StringComparison.OrdinalIgnoreCase) ||
            _stage != tool.Stage)
        {
            ClearCore();
            _toolId = tool.Id;
            _stage = tool.Stage;
        }
    }

    public void Dispose()
    {
        _viewport.KeyDown -= ViewportKeyDown;
        _viewport.TextInput -= ViewportTextInput;
        ClearCore();
    }

    private void ViewportTextInput(object? sender, TextInputEventArgs e)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null || string.IsNullOrEmpty(e.Text))
            return;

        Synchronize();

        var text = new string(e.Text
            .Where(static character => !char.IsControl(character))
            .ToArray());
        if (text.Length == 0)
            return;

        var available = MaxInputLength - _buffer.Length;
        if (available <= 0)
            return;

        if (text.Length > available)
            text = text[..available];

        _buffer += text;
        _message = null;
        e.Handled = true;
        _refreshStatus();
    }

    private void ViewportKeyDown(object? sender, KeyEventArgs e)
    {
        if (_workspace.Tools.ActiveTool is null)
        {
            if (HasInput)
            {
                ClearCore();
                _refreshStatus();
            }
            return;
        }

        Synchronize();

        switch (e.Key)
        {
            case Key.Back when _buffer.Length > 0:
                _buffer = _buffer[..^1];
                _message = null;
                e.Handled = true;
                _refreshStatus();
                return;

            case Key.Escape when _buffer.Length > 0:
                ClearCore();
                e.Handled = true;
                _refreshStatus();
                return;

            case Key.Enter when _buffer.Length > 0:
                ExecuteBuffer();
                e.Handled = true;
                return;
        }
    }

    private void ExecuteBuffer()
    {
        var input = _buffer.Trim();
        if (input.Length == 0)
        {
            ClearCore();
            _refreshStatus();
            return;
        }

        var result = _commands.Execute(input);
        var message = CadLanguageManager.CommandMessage(result);
        if (result.Success)
        {
            ClearCore();
            if (!string.IsNullOrWhiteSpace(message))
                _publishFeedback(message);
            else
                _refreshStatus();
            return;
        }

        _message = string.IsNullOrWhiteSpace(message)
            ? CadLanguageManager.Text(
                "Cad.Command.InputRequired",
                "Input was rejected by the current tool stage.")
            : message;
        _refreshStatus();
    }

    private void ClearCore()
    {
        _buffer = string.Empty;
        _message = null;
        _toolId = null;
        _stage = -1;
    }
}
