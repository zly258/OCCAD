using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OCCAD.Avalonia;

internal enum CadDialogResult
{
    Cancel,
    No,
    Yes,
    Ok
}

internal sealed class CadMessageDialog : Window
{
    private CadMessageDialog(
        string title,
        string message,
        bool yesNoCancel)
    {
        Title = title;
        Width = 430;
        MinHeight = 170;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadTheme.WindowBrush;

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Foreground = CadTheme.Text,
            MaxWidth = 390
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6
        };

        if (yesNoCancel)
        {
            var yes = Button(CadLanguageManager.Text("Cad.Text.Yes", "Yes"));
            yes.Click += (_, _) => Close(CadDialogResult.Yes);
            buttons.Children.Add(yes);

            var no = Button(CadLanguageManager.Text("Cad.Text.No", "No"));
            no.Click += (_, _) => Close(CadDialogResult.No);
            buttons.Children.Add(no);

            var cancel = Button(CadLanguageManager.Text("Cad.Text.Cancel", "Cancel"));
            cancel.Click += (_, _) => Close(CadDialogResult.Cancel);
            buttons.Children.Add(cancel);
        }
        else
        {
            var ok = Button(CadLanguageManager.Text("Cad.Text.Accept", "OK"));
            ok.Click += (_, _) => Close(CadDialogResult.Ok);
            buttons.Children.Add(ok);
        }

        var content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 18
        };
        content.Children.Add(text);
        content.Children.Add(buttons);
        Content = content;
    }

    public static Task<CadDialogResult> ShowAsync(
        Window owner,
        string title,
        string message,
        bool yesNoCancel = false) =>
        new CadMessageDialog(
            title,
            message,
            yesNoCancel)
            .ShowDialog<CadDialogResult>(owner);

    private static Button Button(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 78,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        return button;
    }
}
