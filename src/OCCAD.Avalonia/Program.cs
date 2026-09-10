using Avalonia;

namespace OCCAD.Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        CadDiagnostics.Initialize();

        try
        {
            CadDiagnostics.Trace(
                "Starting Avalonia classic desktop lifetime.");

            var exitCode =
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);

            CadDiagnostics.Trace(
                $"Avalonia classic desktop lifetime exited. ExitCode={exitCode}.");

            return exitCode;
        }
        catch (Exception exception)
        {
            CadDiagnostics.ReportStartupFailure(
                exception);
            return 1;
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<CadApplication>()
            .UsePlatformDetect()
            .LogToTrace();
}
