using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using OcctNet;
using OCCAD;

namespace OCCAD.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

internal sealed class App : Application
{
    private CadApplicationCore? _core;
    private string? _settingsPath;

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme
        {
            DensityStyle = DensityStyle.Compact
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        OcctRuntime.Configure();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _core = new CadApplicationCore();
            LoadSettings(_core);
            desktop.MainWindow = new MainWindow(_core);
            desktop.Exit += (_, _) => ShutdownCore();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void LoadSettings(CadApplicationCore core)
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OCCAD",
            "settings.json");

        if (!File.Exists(_settingsPath))
            return;

        try
        {
            using var stream = File.OpenRead(_settingsPath);
            core.LoadSettings(stream);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException or
            System.Text.Json.JsonException)
        {
            // CadSettingsStore.Load rolls back the store and bound runtime state
            // before validation failures escape. A malformed local preference
            // must not prevent the CAD application from starting.
            System.Diagnostics.Debug.WriteLine(
                $"OCCAD settings load skipped: {exception.Message}");
        }
    }

    private void ShutdownCore()
    {
        var core = Interlocked.Exchange(ref _core, null);
        if (core is null)
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(_settingsPath))
                SaveSettingsAtomically(core, _settingsPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"OCCAD settings save skipped: {exception.Message}");
        }
        finally
        {
            core.Dispose();
        }
    }

    private static void SaveSettingsAtomically(
        CadApplicationCore core,
        string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Settings path has no parent directory.");

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                core.SaveSettings(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
