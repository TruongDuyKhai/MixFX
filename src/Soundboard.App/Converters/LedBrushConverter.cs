using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Soundboard.App.Converters;

/// <summary>Mic LED: green while voice reaches the game, grey otherwise (§5.2).</summary>
public sealed class LedBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.FindResource(value is true ? "SuccessBrush" : "LedOffBrush");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
