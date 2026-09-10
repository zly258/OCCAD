using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

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

internal sealed class CadMessageDialog : Window
{
    private CadDialogResult _result = CadDialogResult.Cancel;

    private CadMessageDialog(
        string title,
        string message,
        bool yesNoCancel,
        CadMessageDialogKind kind)
    {
        Title = title;
        Width = 440;
        MinWidth = 380;
        MaxWidth = 640;
        MinHeight = 180;
        MaxHeight = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;
        Background = CadTheme.Surface;

        var heading = new TextBlock
        {
            Text = title,
            FontWeight =
                global::Avalonia.Media.FontWeight.SemiBold,
            FontSize = CadTheme.FontSize,
            Foreground = CadTheme.Text,
            TextWrapping =
                global::Avalonia.Media.TextWrapping.Wrap
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping =
                global::Avalonia.Media.TextWrapping.Wrap,
            Foreground = CadTheme.Text,
            LineHeight = 18
        };

        var messageHost = new ScrollViewer
        {
            Content = text,
            MaxHeight = 300,
            HorizontalScrollBarVisibility =
                global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility =
                global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var accent = new Border
        {
            Width = 3,
            Background = kind switch
            {
                CadMessageDialogKind.Error =>
                    CadTheme.Accent,
                CadMessageDialogKind.Warning =>
                    CadTheme.BorderStrong,
                CadMessageDialogKind.Question =>
                    CadTheme.Accent,
                _ => CadTheme.Border
            }
        };

        var body = new Grid
        {
            ColumnSpacing = 8,
            Margin = new Thickness(CadTheme.DialogPadding)
        };
        body.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(3)));
        body.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(1, GridUnitType.Star)));
        body.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));
        body.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));

        Grid.SetRowSpan(accent, 2);
        body.Children.Add(accent);
        Grid.SetColumn(heading, 1);
        body.Children.Add(heading);
        Grid.SetColumn(messageHost, 1);
        Grid.SetRow(messageHost, 1);
        body.Children.Add(messageHost);

        var buttons = BuildButtons(yesNoCancel);
        var footer = new Border
        {
            Background = CadTheme.Toolbar,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(CadTheme.DialogPadding, 6),
            Child = buttons
        };

        var root = new Grid();
        root.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(1, GridUnitType.Star)));
        root.RowDefinitions.Add(
            new RowDefinition(GridLength.Auto));
        root.Children.Add(body);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        Content = root;

        KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape)
                return;

            args.Handled = true;
            CloseWith(CadDialogResult.Cancel);
        };
    }

    public static Task<CadDialogResult> ShowAsync(
        Window owner,
        string title,
        string message,
        bool yesNoCancel = false,
        CadMessageDialogKind kind =
            CadMessageDialogKind.Information) =>
        new CadMessageDialog(
            title,
            message,
            yesNoCancel,
            yesNoCancel &&
            kind == CadMessageDialogKind.Information
                ? CadMessageDialogKind.Question
                : kind)
            .ShowDialog<CadDialogResult>(owner);

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
            var yes = Button(
                CadLanguageManager.Text(
                    "Cad.Text.Yes",
                    "Yes"),
                primary: true);
            yes.Click += (_, _) =>
                CloseWith(CadDialogResult.Yes);
            buttons.Children.Add(yes);

            var no = Button(
                CadLanguageManager.Text(
                    "Cad.Text.No",
                    "No"));
            no.Click += (_, _) =>
                CloseWith(CadDialogResult.No);
            buttons.Children.Add(no);

            var cancel = Button(
                CadLanguageManager.Text(
                    "Cad.Text.Cancel",
                    "Cancel"));
            cancel.Click += (_, _) =>
                CloseWith(CadDialogResult.Cancel);
            buttons.Children.Add(cancel);
        }
        else
        {
            var ok = Button(
                CadLanguageManager.Text(
                    "Cad.Text.Ok",
                    "OK"),
                primary: true);
            ok.Click += (_, _) =>
                CloseWith(CadDialogResult.Ok);
            buttons.Children.Add(ok);
        }

        return buttons;
    }

    private void CloseWith(CadDialogResult result)
    {
        _result = result;
        Close(_result);
    }

    private static Button Button(
        string text,
        bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = CadTheme.DialogButtonWidth,
            HorizontalContentAlignment =
                HorizontalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        if (primary)
            button.Classes.Add("cad-primary");
        return button;
    }
}
