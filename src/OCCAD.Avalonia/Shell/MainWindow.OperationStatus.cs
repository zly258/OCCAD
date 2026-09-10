using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using OCCAD;

namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private string? _statusFeedback;

    private void ShowStatusFeedback(string? message)
    {
        _statusFeedback = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();
        RefreshOperationStatus();
    }

    private void RefreshOperationStatus()
    {
        _directInput?.Synchronize();

        var prompt = _workspace.Tools.ActiveTool?.Prompt;
        if (prompt is null)
        {
            var feedback = _statusFeedback ?? string.Empty;
            _selectionStatus.Text = feedback;
            _selectionStatus.Foreground = CadTheme.Text;
            ToolTip.SetTip(
                _selectionStatus,
                feedback.Length == 0 ? null : feedback);
            return;
        }

        _statusFeedback = null;

        var text = prompt.Message;
        if (!string.IsNullOrWhiteSpace(prompt.ResourceKey))
        {
            var template = CadLanguageManager.Text(prompt.ResourceKey, prompt.Message);
            try
            {
                text = prompt.FormatArguments.Count == 0
                    ? template
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        template,
                        prompt.FormatArguments.ToArray());
            }
            catch (FormatException)
            {
                text = prompt.Message;
            }
        }

        if (_directInput is { HasInput: true } directInput)
            text += $"   ·   > {directInput.Buffer}";

        if (!string.IsNullOrWhiteSpace(_directInput?.Message))
            text += $"   ·   {_directInput.Message}";

        _selectionStatus.Text = text;
        _selectionStatus.Foreground = CadTheme.Text;
        ToolTip.SetTip(_selectionStatus, text);
    }
}
