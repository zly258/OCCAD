using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace OCCAD.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        WinForms.Application.EnableVisualStyles();
        WinForms.Application.SetCompatibleTextRenderingDefault(false);

        DispatcherUnhandledException += HandleDispatcherUnhandledException;

        TypeDescriptor.AddAttributes(
            typeof(Color),
            new EditorAttribute(typeof(CadColorEditor), typeof(UITypeEditor)));

        CadLanguageManager.Apply(
            System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? "zh-CN"
                : "en-US");
        base.OnStartup(e);
    }

    private static void HandleDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        if (!IsRecoverable(e.Exception))
            return;

        e.Handled = true;
        var message = CadLanguageManager.Text(
            "Cad.Text.OperationFailed",
            "The operation could not be completed. The current command was kept in a safe state.");
        var detail = string.IsNullOrWhiteSpace(e.Exception.Message)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}";

        System.Windows.MessageBox.Show(
            detail,
            CadLanguageManager.Text("Cad.Text.Error", "Error"),
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
