using Avalonia.Controls;
using OCCAD;

namespace OCCAD.Avalonia;

/// <summary>
/// Compatibility shell for the former command-line surface. OCCAD now keeps
/// interaction prompts in the status bar and exposes common operations through
/// menus, shortcuts and tool input rather than a persistent command textbox.
/// </summary>
internal sealed class CadCommandLineController : IDisposable
{
    private readonly Panel _host;

    public CadCommandLineController(
        CadWorkspace workspace,
        Panel host)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _host = host ?? throw new ArgumentNullException(nameof(host));

        _host.Children.Clear();
        _host.IsVisible = false;
    }

    public void FocusInput()
    {
    }

    public void RefreshLanguage()
    {
    }

    public void Dispose()
    {
        _host.Children.Clear();
    }
}
