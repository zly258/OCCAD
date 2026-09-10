namespace OCCAD.Avalonia;

public sealed partial class MainWindow
{
    private CadDirectInputController? _directInput;

    internal void EnableDirectInput()
    {
        if (_directInput is not null)
            return;

        _directInput = new CadDirectInputController(
            _workspace,
            _viewport,
            RefreshOperationStatus,
            ShowStatusFeedback);

        Closed += (_, _) =>
        {
            _directInput?.Dispose();
            _directInput = null;
        };

        RefreshOperationStatus();
    }
}
