using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace OCCAD.Avalonia;

internal sealed class LayerNameDialog : Window
{
    private readonly TextBox _input;

    public LayerNameDialog(
        string initialName,
        bool creating)
    {
        Title = CadLanguageManager.Text(
            creating
                ? "Cad.Text.NewLayerTitle"
                : "Cad.Text.RenameLayerTitle",
            creating
                ? "New Layer"
                : "Rename Layer");
        Width = 360;
        Height = 150;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadTheme.WindowBrush;

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
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6
        };
        var ok = Button(
            CadLanguageManager.Text("Cad.Text.Accept", "OK"));
        ok.Click += (_, _) => Accept();
        var cancel = Button(
            CadLanguageManager.Text("Cad.Text.Cancel", "Cancel"));
        cancel.Click += (_, _) => Close((string?)null);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var content = new StackPanel
        {
            Margin = new Thickness(14),
            Spacing = 8
        };
        content.Children.Add(new TextBlock
        {
            Text = CadLanguageManager.Text(
                "Cad.Text.LayerName",
                "Layer name")
        });
        content.Children.Add(_input);
        content.Children.Add(buttons);
        Content = content;

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

    private static Button Button(string text)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 76,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Classes.Add("cad-compact");
        return button;
    }
}
