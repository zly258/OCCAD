using System.Globalization;
using System.Windows.Data;

namespace OCCAD.Wpf;

internal sealed class CadLineStyleDisplayConverter : IValueConverter
{
    public static CadLineStyleDisplayConverter Instance { get; } = new();

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        if (!string.Equals(
                CadLanguageManager.CurrentLanguage,
                "zh-CN",
                StringComparison.OrdinalIgnoreCase))
            return text;

        return text switch
        {
            "Solid" => "实线",
            "Dash" or "Dashed" => "虚线",
            "Dot" or "Dotted" => "点线",
            "DashDot" => "点划线",
            _ => text
        };
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
