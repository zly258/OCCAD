using System.Windows;
using System.Windows.Input;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace OCCAD.Wpf;

internal sealed class LayerNameDialog : Window
{
    private readonly WpfTextBox _nameBox;

    public LayerNameDialog(Window owner, string currentName, bool creating = false)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);

        Owner = owner;
        Title = CadLanguageManager.Text(
            creating ? "Cad.Text.NewLayerTitle" : "Cad.Text.RenameLayerTitle",
            creating ? "New Layer" : "Rename Layer");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 340;

        _nameBox = new WpfTextBox
        {
            MinWidth = 220,
            Margin = new Thickness(0, 4, 0, 12),
            Text = currentName,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _nameBox.SelectAll();
        _nameBox.KeyDown += NameBoxKeyDown;

        var ok = new WpfButton
        {
            Content = CadLanguageManager.Text("Cad.Text.Accept", "OK"),
            Width = 76,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        ok.Click += (_, _) => Accept();

        var cancel = new WpfButton
        {
            Content = CadLanguageManager.Text("Cad.Text.Cancel", "Cancel"),
            Width = 76,
            IsCancel = true
        };

        var buttons = new WpfStackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new WpfStackPanel
        {
            Margin = new Thickness(16)
        };
        panel.Children.Add(new WpfTextBlock
        {
            Text = CadLanguageManager.Text("Cad.Text.LayerName", "Layer name")
        });
        panel.Children.Add(_nameBox);
        panel.Children.Add(buttons);

        Content = panel;
        Loaded += (_, _) => _nameBox.Focus();
    }

    public string LayerName => _nameBox.Text.Trim();

    private void NameBoxKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Accept();
        e.Handled = true;
    }

    private void Accept()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            System.Media.SystemSounds.Beep.Play();
            _nameBox.Focus();
            _nameBox.SelectAll();
            return;
        }

        DialogResult = true;
    }
}
