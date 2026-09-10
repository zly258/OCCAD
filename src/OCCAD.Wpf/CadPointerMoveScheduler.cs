using System.Windows.Threading;
using OcctNet;

namespace OCCAD.Wpf;

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
            Tick,
            dispatcher);
        _timer.Stop();
    }

    public void Post(OcctPointerInputEventArgs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Kind != OcctPointerInputKind.Moved)
            throw new ArgumentException("Only pointer-move events can be coalesced.", nameof(input));

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
