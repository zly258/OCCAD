using Avalonia;
namespace OCCAD.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppBuilder
            .Configure<CadApplication>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
