using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace OCCAD.Avalonia;

internal sealed class LayerNameDialog : Window
{
    private readonly TextBox _input;

    public LayerNameDialog(string initialName, bool creating)
    {
        Title = CadLanguageManager.Text(
            creating ? "Cad.Text.NewLayerTitle" : "Cad.Text.RenameLayerTitle",
            creating ? "New Layer" : "Rename Layer");
        Width = 370;
        Height = 154;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadTheme.Surface;

        _input = new TextBox
        {
            Text = initialName,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _input.Classes.Add("cad-input");
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Accept();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close((string?)null);
                e.Handled = true;
            }
        };

        var body = new StackPanel
        {
            Margin = new Thickness(CadTheme.DialogPadding),
            Spacing = 5
        };
        body.Children.Add(new TextBlock
        {
            Text = CadLanguageManager.Text("Cad.Text.LayerName", "Layer name"),
            Foreground = CadTheme.Muted
        });
        body.Children.Add(_input);

        var cancel = DialogButton(CadLanguageManager.Text("Cad.Text.Cancel", "Cancel"));
        cancel.Click += (_, _) => Close((string?)null);
        var ok = DialogButton(CadLanguageManager.Text("Cad.Text.Accept", "OK"), primary: true);
        ok.Click += (_, _) => Accept();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 4
        };
        actions.Children.Add(cancel);
        actions.Children.Add(ok);

        var footer = new Border
        {
            Background = CadTheme.Panel,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(CadTheme.DialogPadding, 6),
            Child = actions
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        root.Children.Add(body);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        Content = root;

        Opened += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };
    }

    private void Accept()
    {
        var value = _input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;
        Close(value);
    }

    private static Button DialogButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = CadTheme.DialogButtonWidth,
            MinHeight = CadTheme.ControlHeight
        };
        button.Classes.Add("cad-compact");
        if (primary)
            button.Classes.Add("cad-primary");
        return button;
    }
}
