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
            System.Text.Json.JsonException)
        {
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
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                using var stream = File.Create(_settingsPath);
                core.SaveSettings(stream);
            }
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
}
