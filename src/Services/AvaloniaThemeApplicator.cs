using AvalonHttp.Services.Interfaces;
using Avalonia;
using Avalonia.Styling;

namespace AvalonHttp.Services;

public class AvaloniaThemeApplicator : IThemeApplicator
{
    public void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (app == null) return;

        app.RequestedThemeVariant = theme == "Dark" 
            ? ThemeVariant.Dark 
            : ThemeVariant.Light;
    }
}