using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace OCCAD.Avalonia;

internal enum CadDialogResult
{
    Cancel,
    No,
    Yes,
    Ok
}

internal enum CadMessageDialogKind
{
    Information,
    Question,
    Warning,
    Error
}

/// <summary>
/// Compact Fluent message dialog shared by OCCAD confirmation and notice flows.
/// </summary>
internal sealed class CadMessageDialog : Window
{
    private CadDialogResult _result = CadDialogResult.Cancel;

    private CadMessageDialog(
        string title,
        string message,
        bool yesNoCancel,
        CadMessageDialogKind kind)
    {
        _ = kind;
        Title = title;
        Width = 420;
        MinWidth = 360;
        MaxWidth = 620;
        MinHeight = 128;
        MaxHeight = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadTheme.Surface;

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = CadTheme.Text,
            LineHeight = 18,
            FontSize = CadTheme.FontSize
        };

        var messageHost = new ScrollViewer
        {
            Content = messageText,
            MaxHeight = 260,
            Margin = new Thickness(CadTheme.DialogPadding),
            HorizontalScrollBarVisibility =
                global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var footer = new Border
        {
            MinHeight = 38,
            Background = CadTheme.Panel,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(CadTheme.DialogPadding, 6),
            Child = BuildButtons(yesNoCancel)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.Children.Add(messageHost);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        Content = root;

        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                CloseWith(CadDialogResult.Cancel);
            }
            else if (args.Key == Key.Enter)
            {
                args.Handled = true;
                CloseWith(yesNoCancel ? CadDialogResult.Yes : CadDialogResult.Ok);
            }
        };
    }

    public static Task<CadDialogResult> ShowAsync(
        Window? owner,
        string title,
        string message,
        bool yesNoCancel = false,
        CadMessageDialogKind kind = CadMessageDialogKind.Information)
    {
        var dialog = new CadMessageDialog(title, message, yesNoCancel, kind);

        if (owner is { IsVisible: true })
            return dialog.ShowDialog<CadDialogResult>(owner);

        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var tcs = new TaskCompletionSource<CadDialogResult>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog._result);
        dialog.Show();
        return tcs.Task;
    }

    private Control BuildButtons(bool yesNoCancel)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 4
        };

        if (yesNoCancel)
        {
            var yes = DialogButton(CadLanguageManager.Text("Cad.Text.Yes", "Yes"));
            yes.Click += (_, _) => CloseWith(CadDialogResult.Yes);
            buttons.Children.Add(yes);

            var no = DialogButton(CadLanguageManager.Text("Cad.Text.No", "No"));
            no.Click += (_, _) => CloseWith(CadDialogResult.No);
            buttons.Children.Add(no);

            var cancel = DialogButton(CadLanguageManager.Text("Cad.Text.Cancel", "Cancel"));
            cancel.Click += (_, _) => CloseWith(CadDialogResult.Cancel);
            buttons.Children.Add(cancel);
        }
        else
        {
            var ok = DialogButton(CadLanguageManager.Text("Cad.Text.OK", "OK"));
            ok.Click += (_, _) => CloseWith(CadDialogResult.Ok);
            buttons.Children.Add(ok);
        }

        return buttons;
    }

    private void CloseWith(CadDialogResult result)
    {
        _result = result;
        Close(_result);
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
