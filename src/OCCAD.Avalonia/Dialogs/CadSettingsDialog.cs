using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DrawingColor = System.Drawing.Color;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

internal sealed class CadSettingsDialog : Window
{
    private readonly TextBox _gripSize = Input();
    private readonly TextBox _gripTolerance = Input();
    private readonly TextBox _snapMarkerSize = Input();
    private readonly TextBox _snapTolerance = Input();
    private readonly TextBox _zoomSensitivity = Input();
    private readonly TextBox _selectionTolerance = Input();
    private readonly TextBox _displayDeviation = Input();
    private readonly TextBox _displayAngle = Input();
    private readonly Button _backgroundButton = new();
    private DrawingColor _background;

    private CadSettingsDialog(CadApplicationSettings settings)
    {
        Title = Text("Cad.Text.Preferences", "Preferences");
        Width = 470;
        MinWidth = 440;
        Height = 450;
        MinHeight = 410;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        Background = CadTheme.Surface;

        ApplyValues(settings);

        _backgroundButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _backgroundButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _backgroundButton.VerticalContentAlignment = VerticalAlignment.Center;
        _backgroundButton.Click += BackgroundButtonClick;
        RefreshBackgroundButton();

        var content = new StackPanel
        {
            Spacing = 6
        };
        Grid.SetIsSharedSizeScope(content, true);

        content.Children.Add(
            GroupHeader(
                Text("Cad.Settings.Scene", "Scene"),
                topMargin: 0));
        content.Children.Add(
            Row(
                Text("Cad.Settings.SceneBackground", "Background"),
                _backgroundButton));
        content.Children.Add(
            Row(
                Text("Cad.Settings.DisplayDeviation", "Display precision"),
                _displayDeviation,
                Text("Cad.Settings.DisplayDeviationHint", "Smaller = finer")));
        content.Children.Add(
            Row(
                Text("Cad.Settings.DisplayAngle", "Angular precision"),
                _displayAngle,
                "°"));

        content.Children.Add(
            GroupHeader(Text("Cad.Settings.Interaction", "Interaction")));
        content.Children.Add(
            Row(
                Text("Cad.Settings.GripSize", "Grip size"),
                _gripSize,
                "px"));
        content.Children.Add(
            Row(
                Text("Cad.Settings.GripTolerance", "Grip tolerance"),
                _gripTolerance,
                "px"));
        content.Children.Add(
            Row(
                Text("Cad.Settings.SnapMarkerSize", "Snap marker size"),
                _snapMarkerSize,
                "px"));
        content.Children.Add(
            Row(
                Text("Cad.Settings.SnapTolerance", "Snap tolerance"),
                _snapTolerance,
                "px"));
        content.Children.Add(
            Row(
                Text("Cad.Settings.SelectionTolerance", "Selection tolerance"),
                _selectionTolerance,
                "px"));
        content.Children.Add(
            Row(
                Text("Cad.Settings.ZoomSensitivity", "Mouse zoom sensitivity"),
                _zoomSensitivity,
                "0.1–5"));

        var reset = DialogButton(
            Text("Cad.Text.ResetDefaults", "Reset Defaults"),
            minWidth: 96);
        reset.Click += (_, _) =>
            ApplyValues(CadApplicationSettings.CreateDefault());

        var cancel = DialogButton(Text("Cad.Text.Cancel", "Cancel"));
        cancel.Click += (_, _) => Close(null);

        var ok = DialogButton(Text("Cad.Text.OK", "OK"));
        ok.Click += async (_, _) => await AcceptAsync();

        var buttons = new DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(0)
        };
        DockPanel.SetDock(reset, Dock.Left);
        buttons.Children.Add(reset);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        right.Children.Add(cancel);
        right.Children.Add(ok);
        DockPanel.SetDock(right, Dock.Right);
        buttons.Children.Add(right);

        var footer = new Border
        {
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(CadTheme.DialogPadding, 8),
            Child = buttons
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        var scroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility =
                global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Margin = new Thickness(CadTheme.DialogPadding, 10)
        };
        grid.Children.Add(scroll);
        Grid.SetRow(footer, 1);
        grid.Children.Add(footer);

        Content = grid;
    }

    public static Task<CadApplicationSettings?> ShowAsync(
        Window owner,
        CadApplicationSettings current)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(current);

        var dialog = new CadSettingsDialog(current.Clone());
        return dialog.ShowDialog<CadApplicationSettings?>(owner);
    }

    private async Task AcceptAsync()
    {
        try
        {
            var value = ReadValues();
            value.Validate();
            Close(value);
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException and
                  not StackOverflowException and
                  not AccessViolationException)
        {
            await CadMessageDialog.ShowAsync(
                this,
                Text("Cad.Text.ErrorTitle", "OCCAD Error"),
                exception.GetBaseException().Message,
                kind: CadMessageDialogKind.Error);
        }
    }

    private async void BackgroundButtonClick(
        object? sender,
        global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var value = await CadColorDialog.ShowAsync(this, _background);
        if (value is not { } color)
            return;

        _background = color;
        RefreshBackgroundButton();
    }

    private void RefreshBackgroundButton()
    {
        _backgroundButton.Content =
            CadApplicationSettings.ColorToHex(_background);
        var media = MediaColor.FromArgb(
            _background.A,
            _background.R,
            _background.G,
            _background.B);
        _backgroundButton.Background = new SolidColorBrush(media);
        _backgroundButton.Foreground =
            RelativeLuminance(_background) > 0.52
                ? Brushes.Black
                : Brushes.White;
    }

    private CadApplicationSettings ReadValues() =>
        new()
        {
            SceneBackground =
                CadApplicationSettings.ColorToHex(_background),
            GripSize =
                ParseInt(
                    _gripSize,
                    Text("Cad.Settings.GripSize", "Grip size")),
            GripTolerance =
                ParseDouble(
                    _gripTolerance,
                    Text("Cad.Settings.GripTolerance", "Grip tolerance")),
            SnapMarkerSize =
                ParseInt(
                    _snapMarkerSize,
                    Text("Cad.Settings.SnapMarkerSize", "Snap marker size")),
            SnapTolerance =
                ParseDouble(
                    _snapTolerance,
                    Text("Cad.Settings.SnapTolerance", "Snap tolerance")),
            ZoomSensitivity =
                ParseDouble(
                    _zoomSensitivity,
                    Text("Cad.Settings.ZoomSensitivity", "Zoom sensitivity")),
            SelectionTolerance =
                ParseInt(
                    _selectionTolerance,
                    Text("Cad.Settings.SelectionTolerance", "Selection tolerance")),
            DisplayDeviationCoefficient =
                ParseDouble(
                    _displayDeviation,
                    Text("Cad.Settings.DisplayDeviation", "Display precision")),
            DisplayDeviationAngleDegrees =
                ParseDouble(
                    _displayAngle,
                    Text("Cad.Settings.DisplayAngle", "Display angle"))
        };

    private void ApplyValues(CadApplicationSettings value)
    {
        _background = value.SceneBackgroundColor;
        _gripSize.Text = value.GripSize.ToString(CultureInfo.CurrentCulture);
        _gripTolerance.Text = Format(value.GripTolerance);
        _snapMarkerSize.Text =
            value.SnapMarkerSize.ToString(CultureInfo.CurrentCulture);
        _snapTolerance.Text = Format(value.SnapTolerance);
        _zoomSensitivity.Text = Format(value.ZoomSensitivity);
        _selectionTolerance.Text =
            value.SelectionTolerance.ToString(CultureInfo.CurrentCulture);
        _displayDeviation.Text =
            value.DisplayDeviationCoefficient.ToString(
                "0.####",
                CultureInfo.CurrentCulture);
        _displayAngle.Text = Format(value.DisplayDeviationAngleDegrees);
        RefreshBackgroundButton();
    }

    private static Control GroupHeader(
        string text,
        double topMargin = 12) =>
        new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, topMargin, 0, 2)
        };

    private static Control Row(
        string label,
        Control editor,
        string? suffix = null)
    {
        editor.VerticalAlignment = VerticalAlignment.Center;
        editor.HorizontalAlignment = HorizontalAlignment.Stretch;

        var grid = new Grid
        {
            ColumnSpacing = 8
        };
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(GridLength.Auto)
            {
                SharedSizeGroup = "SettingsLabels"
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var labelText = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        grid.Children.Add(labelText);

        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            var suffixText = new TextBlock
            {
                Text = suffix,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(suffixText, 2);
            grid.Children.Add(suffixText);
        }

        return grid;
    }

    private static TextBox Input() =>
        new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

    private static Button DialogButton(
        string text,
        double? minWidth = null) =>
        new()
        {
            Content = text,
            MinWidth = minWidth ?? CadTheme.DialogButtonWidth,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

    private static int ParseInt(TextBox box, string name)
    {
        if (int.TryParse(
                box.Text,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var value))
            return value;

        throw new FormatException(
            string.Format(
                CultureInfo.CurrentCulture,
                Text(
                    "Cad.Settings.ParseIntError",
                    "{0} must be a valid integer."),
                name));
    }

    private static double ParseDouble(TextBox box, string name)
    {
        if (double.TryParse(
                box.Text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var value) ||
            double.TryParse(
                box.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
        {
            return value;
        }

        throw new FormatException(
            string.Format(
                CultureInfo.CurrentCulture,
                Text(
                    "Cad.Settings.ParseDoubleError",
                    "{0} must be a valid number."),
                name));
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.CurrentCulture);

    private static double RelativeLuminance(DrawingColor color) =>
        (0.2126 * color.R +
         0.7152 * color.G +
         0.0722 * color.B) /
        255.0;

    private static string Text(string key, string fallback) =>
        CadLanguageManager.Text(key, fallback);
}
