using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace OCCAD.Avalonia;

internal static class CadDiagnostics
{
    private static readonly object SyncRoot = new();
    private static int _initialized;
    private static int _uiDispatcherAttached;

    public static string LogPath { get; } = CreateLogPath();

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
            return;

        AppDomain.CurrentDomain.UnhandledException +=
            OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException +=
            OnUnobservedTaskException;

        Trace(
            $"Process started. PID={Environment.ProcessId}; " +
            $"BaseDirectory={AppContext.BaseDirectory}; " +
            $"Framework={Environment.Version}; " +
            $"OS={Environment.OSVersion}.");
    }

    public static void AttachUiDispatcher()
    {
        if (Interlocked.Exchange(
                ref _uiDispatcherAttached,
                1) != 0)
            return;

        Dispatcher.UIThread.UnhandledException +=
            OnUiThreadUnhandledException;

        Trace(
            "UI dispatcher unhandled-exception handler attached.");
    }

    public static void Trace(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Write("Trace", message.Trim());
    }

    public static void Report(
        Exception exception,
        string context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        var builder = new StringBuilder();
        builder.Append(context.Trim());

        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            builder.AppendLine();
            builder.Append(current.GetType().FullName);
            builder.Append(": ");
            builder.Append(current.Message);

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                builder.AppendLine();
                builder.Append(current.StackTrace);
            }
        }

        Write("Error", builder.ToString());
    }

    public static void ReportStartupFailure(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Report(
            exception,
            "Startup");
        TryShowStartupFailure(exception);
    }

    public static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException or
        StackOverflowException or
        AccessViolationException;

    private static void OnCurrentDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        var exception =
            e.ExceptionObject as Exception ??
            new InvalidOperationException(
                $"Non-Exception throwable: {e.ExceptionObject}");

        Report(
            exception,
            e.IsTerminating
                ? "AppDomain fatal unhandled exception"
                : "AppDomain unhandled exception");
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        Report(
            e.Exception,
            "Unobserved task exception");
        e.SetObserved();

        TryPostErrorWindow(
            e.Exception,
            "Background task");
    }

    private static void OnUiThreadUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        Report(
            e.Exception,
            "UI dispatcher unhandled exception");

        if (IsFatal(e.Exception))
            return;

        e.Handled = true;
        TryPostErrorWindow(
            e.Exception,
            "UI");
    }

    private static void TryPostErrorWindow(
        Exception exception,
        string context)
    {
        try
        {
            Dispatcher.UIThread.Post(
                () => CadErrorWindow.ShowError(
                    exception,
                    context));
        }
        catch (Exception postFailure)
        {
            Report(
                postFailure,
                "Posting error window");
        }
    }

    private static void Write(
        string level,
        string message)
    {
        try
        {
            var line =
                $"[{DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)}] " +
                $"[{level}] {message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                var directory =
                    Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(
                    LogPath,
                    line,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never become the reason the application fails.
        }
    }

    private static void TryShowStartupFailure(
        Exception exception)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var message =
                exception.GetType().Name +
                ": " +
                exception.Message +
                Environment.NewLine +
                Environment.NewLine +
                "Log: " +
                LogPath;

            NativeMethods.MessageBox(
                0,
                message,
                "OCCAD Startup Error",
                0x00000010u);
        }
        catch
        {
        }
    }

    private static string CreateLogPath()
    {
        var root =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetTempPath();

        return Path.Combine(
            root,
            "OCCAD",
            "Logs",
            "OCCAD.App.log");
    }

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            EntryPoint = "MessageBoxW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        public static extern int MessageBox(
            nint hWnd,
            string text,
            string caption,
            uint type);
    }
}
