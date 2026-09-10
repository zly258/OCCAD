using Avalonia.Platform.Storage;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Platform storage adapter for the UI-independent CadDocumentSession. It owns
/// only the current Avalonia storage handle; document state remains in Core.
/// </summary>
internal sealed class CadDocumentStorage(CadApplicationCore application) : IDisposable
{
    private readonly CadApplicationCore _application =
        application ?? throw new ArgumentNullException(nameof(application));
    private IStorageFile? _currentFile;

    public IStorageFile? CurrentFile => _currentFile;
    public string DisplayName => _application.Documents.DisplayName;

    public void New()
    {
        ReplaceCurrentFile(null);
        _application.Documents.New();
    }

    public async Task OpenAsync(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = await file.OpenReadAsync();
        _application.Documents.Open(stream, file.Name);
        ReplaceCurrentFile(file);
    }

    public Task SaveCurrentAsync()
    {
        if (_currentFile is null)
            throw new InvalidOperationException("The document has no storage file yet.");
        return SaveAsAsync(_currentFile);
    }

    public async Task SaveAsAsync(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            _application.Documents.Save(localPath);
            ReplaceCurrentFile(file);
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek)
        {
            stream.SetLength(0);
            stream.Position = 0;
        }

        _application.Documents.Save(stream, file.Name);
        await stream.FlushAsync();
        ReplaceCurrentFile(file);
    }

    public void Dispose() => ReplaceCurrentFile(null);

    private void ReplaceCurrentFile(IStorageFile? file)
    {
        if (ReferenceEquals(_currentFile, file))
            return;

        _currentFile?.Dispose();
        _currentFile = file;
    }
}
