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

internal sealed class CadMessageDialog : Window
{
    private CadDialogResult _result =
        CadDialogResult.Cancel;

    private CadMessageDialog(
        string title,
        string message,
        bool yesNoCancel,
        CadMessageDialogKind kind)
    {
        Title = title;
        Width = 460;
        MinWidth = 420;
        MaxWidth = 680;
        MinHeight = 170;
        MaxHeight = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;
        Background = CadTheme.WindowBrush;

        var statusBrush =
            StatusBrush(kind);
        var statusText =
            StatusText(kind);

        var headerText = new TextBlock
        {
            Text = statusText,
            FontWeight = FontWeight.SemiBold,
            Foreground = CadTheme.Text,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        var header = new Border
        {
            MinHeight = 32,
            Background = CadTheme.Header,
            BorderBrush = CadTheme.BorderStrong,
            BorderThickness =
                new Thickness(0, 0, 0, 1),
            Padding = new Thickness(
                CadTheme.DialogPadding,
                0),
            Child = headerText
        };

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = CadTheme.Text,
            LineHeight = 19,
            VerticalAlignment =
                VerticalAlignment.Top
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
            Width = 4,
            Background = statusBrush
        };

        var body = new Grid
        {
            ColumnSpacing = 10,
            Margin = new Thickness(
                CadTheme.DialogPadding,
                12)
        };
        body.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(4)));
        body.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(
                    1,
                    GridUnitType.Star)));
        body.Children.Add(accent);
        Grid.SetColumn(messageHost, 1);
        body.Children.Add(messageHost);

        var buttons =
            BuildButtons(yesNoCancel);
        var footer = new Border
        {
            MinHeight = 44,
            Background = CadTheme.PanelAlt,
            BorderBrush = CadTheme.Border,
            BorderThickness =
                new Thickness(0, 1, 0, 0),
            Padding = new Thickness(
                CadTheme.DialogPadding,
                8),
            Child = buttons
        };

        var root = new Grid();
        root.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));
        root.RowDefinitions.Add(
            new RowDefinition(
                new GridLength(
                    1,
                    GridUnitType.Star)));
        root.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        root.Children.Add(header);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;

        KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape)
                return;

            args.Handled = true;
            CloseWith(
                CadDialogResult.Cancel);
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
            kind ==
            CadMessageDialogKind.Information
                ? CadMessageDialogKind.Question
                : kind)
            .ShowDialog<CadDialogResult>(
                owner);

    private Control BuildButtons(
        bool yesNoCancel)
    {
        var buttons = new StackPanel
        {
            Orientation =
                Orientation.Horizontal,
            HorizontalAlignment =
                HorizontalAlignment.Right,
            Spacing = 6
        };

        if (yesNoCancel)
        {
            var yes = Button(
                CadLanguageManager.Text(
                    "Cad.Text.Yes",
                    "Yes"),
                primary: true);
            yes.Click += (_, _) =>
                CloseWith(
                    CadDialogResult.Yes);
            buttons.Children.Add(yes);

            var no = Button(
                CadLanguageManager.Text(
                    "Cad.Text.No",
                    "No"));
            no.Click += (_, _) =>
                CloseWith(
                    CadDialogResult.No);
            buttons.Children.Add(no);

            var cancel = Button(
                CadLanguageManager.Text(
                    "Cad.Text.Cancel",
                    "Cancel"));
            cancel.Click += (_, _) =>
                CloseWith(
                    CadDialogResult.Cancel);
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
                CloseWith(
                    CadDialogResult.Ok);
            buttons.Children.Add(ok);
        }

        return buttons;
    }

    private void CloseWith(
        CadDialogResult result)
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
            MinWidth =
                CadTheme.DialogButtonWidth,
            MinHeight =
                CadTheme.ControlHeight,
            HorizontalContentAlignment =
                HorizontalAlignment.Center,
            VerticalContentAlignment =
                VerticalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        if (primary)
            button.Classes.Add("cad-primary");
        return button;
    }

    private static IBrush StatusBrush(
        CadMessageDialogKind kind) =>
        kind switch
        {
            CadMessageDialogKind.Error =>
                new SolidColorBrush(
                    Color.Parse("#C74646")),
            CadMessageDialogKind.Warning =>
                new SolidColorBrush(
                    Color.Parse("#C28A27")),
            CadMessageDialogKind.Question =>
                CadTheme.Accent,
            _ =>
                CadTheme.BorderStrong
        };

    private static string StatusText(
        CadMessageDialogKind kind) =>
        kind switch
        {
            CadMessageDialogKind.Error =>
                CadLanguageManager.Text(
                    "Cad.Text.Error",
                    "Error"),
            CadMessageDialogKind.Warning =>
                CadLanguageManager.Text(
                    "Cad.Text.Warning",
                    "Warning"),
            CadMessageDialogKind.Question =>
                CadLanguageManager.Text(
                    "Cad.Text.Question",
                    "Question"),
            _ =>
                CadLanguageManager.Text(
                    "Cad.Text.Information",
                    "Information")
        };
}
