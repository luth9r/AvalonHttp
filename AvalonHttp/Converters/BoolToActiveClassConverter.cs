using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvalonHttp.Converters;

public class BoolToActiveClassConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            bool shouldBeActive = parameter?.ToString() == "Invert" ? !isActive : isActive;
            
            return shouldBeActive ? "ActionButton Active" : "ActionButton";
        }
        return "ActionButton";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}