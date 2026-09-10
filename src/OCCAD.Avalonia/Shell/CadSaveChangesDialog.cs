using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace OCCAD.Avalonia;

internal enum CadSaveChangesDecision
{
    Cancel = 0,
    Save = 1,
    Discard = 2
}

/// <summary>
/// Minimal modal used only to protect document data. It intentionally avoids a
/// general message-box framework and contains no CAD state.
/// </summary>
internal sealed class CadSaveChangesDialog : Window
{
    public CadSaveChangesDialog(string documentName)
    {
        Title = "OCCAD";
        Width = 420;
        Height = 150;
        MinWidth = 420;
        MinHeight = 150;
        MaxWidth = 420;
        MaxHeight = 150;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CadUi.Panel;

        var message = new TextBlock
        {
            Text = $"“{documentName}” 已修改，是否保存？",
            FontSize = 12,
            Foreground = CadUi.Text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18)
        };

        var save = Button("保存", CadSaveChangesDecision.Save);
        var discard = Button("不保存", CadSaveChangesDecision.Discard);
        var cancel = Button("取消", CadSaveChangesDecision.Cancel);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6,
            Children = { save, discard, cancel }
        };

        Content = new Grid
        {
            Margin = new Thickness(18),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                message,
                buttons
            }
        };
        Grid.SetRow(buttons, 1);

        Opened += (_, _) => save.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key != global::Avalonia.Input.Key.Escape)
                return;
            Close(CadSaveChangesDecision.Cancel);
            e.Handled = true;
        };
    }

    private Button Button(string text, CadSaveChangesDecision decision)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 72,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Foreground = CadUi.Text
        };
        CadUi.ConfigureCompactButton(button);
        button.Click += (_, _) => Close(decision);
        return button;
    }
}
