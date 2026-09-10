using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using OcctNet;

namespace OCCAD.Avalonia;

public sealed class CadApplication : Application
{
    public override void Initialize()
    {
        CadDiagnostics.Trace("CadApplication.Initialize entered.");

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

        // OCCAD uses English as the product default. Once the user changes
        // language, the persisted application setting wins on the next start.
        var settings = CadApplicationSettings.Load();
        CadLanguageManager.Apply(settings.Language);

        CadDiagnostics.Trace("CadApplication.Initialize completed.");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        CadDiagnostics.AttachUiDispatcher();
        CadDiagnostics.Trace(
            $"Framework initialization entered. Lifetime={ApplicationLifetime?.GetType().FullName ?? "<null>"}.");

        CadDiagnostics.Trace("Configuring OCCT runtime.");
        OcctRuntime.Configure();
        CadDiagnostics.Trace(
            "OCCT runtime configured." + Environment.NewLine +
            OcctRuntime.GetDiagnosticReport());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            CadDiagnostics.Trace("Constructing MainWindow.");

            var window = new MainWindow();
            // The shell is Ribbon-first. Refinement passes attach status,
            // floating-tool and property surfaces to the same workspace state;
            // no legacy Menu/toolbar is constructed before the window is shown.
            window.ApplyRibbon();
            window.ApplyUiRefinement();
            window.ApplyFloatingToolPanel();
            window.ApplyRibbonSynchronization();
            window.ApplyPropertyGridRefinement();
            window.ApplyCommandStatusRefinement();

            CadDiagnostics.Trace("MainWindow constructed.");

            desktop.MainWindow = window;
            desktop.Exit += (_, args) =>
                CadDiagnostics.Trace(
                    $"Desktop lifetime exit requested. ExitCode={args.ApplicationExitCode}.");

            CadDiagnostics.Trace("MainWindow assigned to desktop lifetime.");
        }
        else
        {
            CadDiagnostics.Trace("Classic desktop lifetime was not available.");
        }

        base.OnFrameworkInitializationCompleted();
        CadDiagnostics.Trace("Framework initialization completed.");
    }
}
