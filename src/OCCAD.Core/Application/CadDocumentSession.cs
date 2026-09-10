namespace OCCAD;

/// <summary>
/// Application-level document session. It owns native document identity and
/// persistence lifecycle, while file pickers and platform storage handles stay
/// in the UI adapter.
/// </summary>
public sealed class CadDocumentSession
{
    public const string UntitledName = "Untitled.occad";

    private readonly CadWorkspace _workspace;
    private string _displayName = UntitledName;

    internal CadDocumentSession(CadWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public string DisplayName => _displayName;

    public event EventHandler? IdentityChanged;

    public void New()
    {
        _workspace.ResetDocument();
        SetDisplayName(UntitledName);
    }

    public void Open(Stream stream, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        CadDocumentSerializer.Load(_workspace, stream);
        SetDisplayName(NormalizeDisplayName(displayName));
    }

    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        CadDocumentSerializer.Load(_workspace, fullPath);
        SetDisplayName(Path.GetFileName(fullPath));
    }

    public void Save(Stream stream, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        CadDocumentSerializer.Save(_workspace, stream);
        stream.Flush();
        _workspace.MarkSaved();
        if (!string.IsNullOrWhiteSpace(displayName))
            SetDisplayName(displayName.Trim());
    }

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        CadDocumentSerializer.Save(_workspace, fullPath);
        _workspace.MarkSaved();
        SetDisplayName(Path.GetFileName(fullPath));
    }

    private void SetDisplayName(string value)
    {
        if (string.Equals(_displayName, value, StringComparison.Ordinal))
            return;

        _displayName = value;
        IdentityChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string NormalizeDisplayName(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? UntitledName
            : value.Trim();
}
