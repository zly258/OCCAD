using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace OCCAD.Avalonia;

public sealed class CadApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme
        {
            DensityStyle = DensityStyle.Compact
        });
        Styles.Add(new StyleInclude(new Uri("avares://OCCAD/"))
        {
            Source = new Uri(
                "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml")
        });

        CadTheme.Apply(this);
        CadLanguageManager.Apply(
            System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith(
                "zh",
                StringComparison.OrdinalIgnoreCase)
                ? "zh-CN"
                : "en-US");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        if (!IsRecoverable(e.Exception))
            return;

        e.Handled = true;
        System.Diagnostics.Debug.WriteLine(e.Exception);
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
