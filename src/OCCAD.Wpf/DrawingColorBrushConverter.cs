using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OCCAD.Wpf;

internal sealed class DrawingColorBrushConverter : IValueConverter
{
    public static DrawingColorBrushConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not System.Drawing.Color color)
            return System.Windows.Media.Brushes.Transparent;

        return new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
