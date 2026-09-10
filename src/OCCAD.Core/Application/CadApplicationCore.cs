namespace OCCAD;

/// <summary>
/// UI-independent application composition root. It owns one CAD workspace,
/// one document session, one command session, and one application settings store.
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
        Commands = new CadCommandManager(Workspace);
        Documents = new CadDocumentSession(Workspace);
        _settingsBinding = Settings.Bind(Workspace);
    }

    public CadSettingsStore Settings { get; }
    public CadWorkspace Workspace { get; }
    public CadCommandManager Commands { get; }
    public CadDocumentSession Documents { get; }

    public void LoadSettings(Stream stream)
    {
        ThrowIfDisposed();
        // CadSettingsStore.Load is transactional and publishes each effective
        // replacement through the binding. A second Refresh pass would duplicate
        // application and could mask which setting caused validation failure.
        Settings.Load(stream);
    }

    public void SaveSettings(Stream stream)
    {
        ThrowIfDisposed();
        Settings.Save(stream);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _settingsBinding.Dispose();

        try
        {
            Workspace.Dispose();
        }
        catch (Exception exception)
            when (IsRecoverableShutdownFailure(exception))
        {
            // A desktop process must be able to exit even if the native viewer
            // is already partially destroyed. Operational cleanup remains
            // strict while the application is running; shutdown is best-effort.
            System.Diagnostics.Debug.WriteLine(
                $"OCCAD workspace shutdown cleanup failed: {exception}");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static bool IsRecoverableShutdownFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
