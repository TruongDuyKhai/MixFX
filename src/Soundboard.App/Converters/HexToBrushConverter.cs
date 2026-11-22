using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Soundboard.App.Converters;

/// <summary>"#RRGGBB" from settings → brush. Bad values (hand-edited settings) fall back to grey instead of crashing the view.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Brush Fallback = Freeze(new SolidColorBrush(Colors.Gray));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex)
        {
            return Fallback;
        }

        try
        {
            return Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));
        }
        catch (FormatException)
        {
            return Fallback;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
