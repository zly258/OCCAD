using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;

namespace OCCAD.Avalonia;

internal sealed class CadErrorWindow : Window
{
    private static bool _showing;

    private CadErrorWindow(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Title = CadLanguageManager.Text("Cad.Text.ErrorTitle", "OCCAD Error");
        Width = 640;
        Height = 390;
        MinWidth = 500;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadTheme.Surface;

        var message = new TextBlock
        {
            Text = CadLanguageManager.Text(
                "Cad.Text.ErrorMessage",
                "OCCAD encountered an error. Details were written to the application log."),
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Foreground = CadTheme.Text,
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold
        };

        var source = new TextBlock
        {
            Text = context,
            Foreground = CadTheme.Muted,
            TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis
        };

        var details = new TextBox
        {
            Text = exception.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        };

        var path = new TextBlock
        {
            Text = CadLanguageManager.Text("Cad.Text.ErrorLog", "Log:") + " " + CadDiagnostics.LogPath,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Foreground = CadTheme.Muted,
            FontSize = CadTheme.SmallFontSize
        };

        var close = DialogButton(CadLanguageManager.Text("Cad.Text.Continue", "Continue"));
        close.Click += (_, _) => Close();

        var exit = DialogButton(CadLanguageManager.Text("Cad.Text.Exit", "Exit"));
        exit.Click += (_, _) =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(1);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 4
        };
        buttons.Children.Add(close);
        buttons.Children.Add(exit);

        var content = new Grid
        {
            Margin = new Thickness(CadTheme.DialogPadding),
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto")
        };
        content.Children.Add(message);

        Grid.SetRow(source, 1);
        source.Margin = new Thickness(0, 5, 0, 7);
        content.Children.Add(source);

        Grid.SetRow(details, 2);
        content.Children.Add(details);

        Grid.SetRow(path, 3);
        path.Margin = new Thickness(0, 7, 0, 0);
        content.Children.Add(path);

        var footer = new Border
        {
            Background = CadTheme.Panel,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(CadTheme.DialogPadding, 6),
            Child = buttons
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        root.Children.Add(content);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        Content = root;
    }

    public static void ShowError(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_showing ||
            Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        _showing = true;
        try
        {
            var dialog = new CadErrorWindow(exception, context);
            dialog.Closed += (_, _) => _showing = false;

            if (desktop.MainWindow is { IsVisible: true } owner)
                dialog.Show(owner);
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.Show();
            }
        }
        catch (Exception showFailure)
        {
            _showing = false;
            CadDiagnostics.Report(showFailure, "Error window");
        }
    }

    private static Button DialogButton(string text) =>
        new()
        {
            Content = text,
            MinWidth = CadTheme.DialogButtonWidth,
            MinHeight = CadTheme.ControlHeight,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
}
