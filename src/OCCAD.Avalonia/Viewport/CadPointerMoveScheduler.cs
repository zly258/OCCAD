using Avalonia.Threading;
using OcctNet;

namespace OCCAD.Avalonia;

internal sealed class CadPointerMoveScheduler : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Action<OcctPointerInputEventArgs> _process;
    private OcctPointerInputEventArgs? _pending;

    public CadPointerMoveScheduler(
        Dispatcher dispatcher,
        Action<OcctPointerInputEventArgs> process)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            dispatcher);
        _timer.Tick += Tick;
    }

    public void Post(OcctPointerInputEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _pending = input;
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    public void Flush()
    {
        var input = _pending;
        _pending = null;
        _timer.Stop();
        if (input is not null)
            _process(input);
    }

    public void Clear()
    {
        _pending = null;
        _timer.Stop();
    }

    public void Dispose()
    {
        Clear();
        _timer.Tick -= Tick;
    }

    private void Tick(object? sender, EventArgs e)
    {
        var input = _pending;
        _pending = null;
        _timer.Stop();
        if (input is not null)
            _process(input);
    }
}
