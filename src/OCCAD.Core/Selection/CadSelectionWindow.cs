using OcctNet;

namespace OCCAD;

/// <summary>
/// Core-owned Window/Crossing selection gesture presentation. Frontends provide
/// screen coordinates only; native transient lifetime remains inside Core.
/// </summary>
public sealed class CadSelectionWindow : IDisposable
{
    private readonly CadSelectionWindowPresenter _presenter = new();
    private readonly IDisposable _transientRegistration;
    private bool _disposed;

    public CadSelectionWindow(CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _transientRegistration = workspace.Transients.Register(
            CadTransientChannel.SelectionWindow,
            Clear,
            () => IsVisible);
    }

    public bool IsVisible => _presenter.IsVisible;

    public void AttachEngine(OcctEngine engine)
    {
        ThrowIfDisposed();
        _presenter.AttachEngine(engine);
    }

    public void Show(
        int startX,
        int startY,
        int endX,
        int endY,
        bool crossing)
    {
        ThrowIfDisposed();
        _presenter.Show(startX, startY, endX, endY, crossing);
    }

    public void Clear()
    {
        if (_disposed)
            return;
        _presenter.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _transientRegistration.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
