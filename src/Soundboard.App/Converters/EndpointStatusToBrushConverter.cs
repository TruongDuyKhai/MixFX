using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Soundboard.Core.Models;

namespace Soundboard.App.Converters;

/// <summary>Colour of the small status dot next to each device picker.</summary>
public sealed class EndpointStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.FindResource(value switch
        {
            AudioEndpointStatus.Running => "SuccessBrush",
            AudioEndpointStatus.Failed or AudioEndpointStatus.Lost => "DangerBrush",
            _ => "LedOffBrush",
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
