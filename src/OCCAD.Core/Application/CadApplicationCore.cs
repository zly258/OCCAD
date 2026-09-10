namespace OCCAD;

/// <summary>
/// UI-independent application composition root. It owns one CAD workspace and
/// one application settings store, and binds settings to the workspace's
/// authoritative interaction services.
/// </summary>
public sealed class CadApplicationCore : IDisposable
{
    private readonly CadSettingsBinding _settingsBinding;
    private bool _disposed;

    public CadApplicationCore()
        : this(new CadSettingsStore())
    {
    }

    public CadApplicationCore(CadSettingsStore settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Workspace = new CadWorkspace();
        _settingsBinding = Settings.Bind(Workspace);
    }

    public CadSettingsStore Settings { get; }
    public CadWorkspace Workspace { get; }

    public void LoadSettings(Stream stream)
    {
        ThrowIfDisposed();
        Settings.Load(stream);
        _settingsBinding.Refresh();
    }

    public void SaveSettings(Stream stream)
    {
        ThrowIfDisposed();
        Settings.Save(stream);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settingsBinding.Dispose();
        Workspace.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
