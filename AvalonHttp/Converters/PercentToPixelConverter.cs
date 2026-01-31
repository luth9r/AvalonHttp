using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvalonHttp.Converters;

public class PercentToPixelConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && 
            values[0] is double percent && 
            values[1] is double containerWidth)
        {
            return Math.Max(2, (percent / 100.0) * containerWidth);
        }
        return 2.0;
    }
}