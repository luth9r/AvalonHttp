using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AvalonHttp.Converters;

public class MethodToBrushConverter : IValueConverter
{
    private static readonly IBrush GetFg = Brush.Parse("#10B981");    // Green
    private static readonly IBrush PostFg = Brush.Parse("#3B82F6");   // Blue
    private static readonly IBrush PutFg = Brush.Parse("#F59E0B");    // Orange
    private static readonly IBrush DeleteFg = Brush.Parse("#EF4444"); // Red
    private static readonly IBrush PatchFg = Brush.Parse("#8B5CF6");  // Purple
    private static readonly IBrush DefaultFg = Brush.Parse("#6B7280"); // Gray
    
    private static readonly IBrush GetBg = Brush.Parse("#1A10B981");
    private static readonly IBrush PostBg = Brush.Parse("#1A3B82F6");
    private static readonly IBrush PutBg = Brush.Parse("#1AF59E0B");
    private static readonly IBrush DeleteBg = Brush.Parse("#1AEF4444");
    private static readonly IBrush PatchBg = Brush.Parse("#1A8B5CF6");
    private static readonly IBrush DefaultBg = Brush.Parse("#1A6B7280");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isBackground = parameter as string == "Bg";
        if (value is string method)
        {
            var m = method.ToUpper();

            return m switch
            {
                "GET" => isBackground ? GetBg : GetFg,
                "POST" => isBackground ? PostBg : PostFg,
                "PUT" => isBackground ? PutBg : PutFg,
                "DELETE" => isBackground ? DeleteBg : DeleteFg,
                "PATCH" => isBackground ? PatchBg : PatchFg,
                _ => isBackground ? DefaultBg : DefaultFg
            };
        }
        return isBackground ? DefaultBg : DefaultFg;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}