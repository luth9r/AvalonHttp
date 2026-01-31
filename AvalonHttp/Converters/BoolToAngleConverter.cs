using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvalonHttp.Converters;

public class BoolToAngleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? 0.0 : -90.0; // Expanded = 0°, Collapsed = -90°
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}