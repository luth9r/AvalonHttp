using System;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;
using Avalonia;
using Avalonia.Styling;

namespace AvalonHttp.Services;

public class ThemeService : IThemeService
{
    private readonly ISessionService _sessionService;
    private readonly IThemeApplicator _themeApplicator;
    private readonly string[] _availableThemes = { "Dark", "Light" };
    
    public string CurrentTheme { get; private set; } = "Dark";
    
    public ThemeService(ISessionService sessionService, IThemeApplicator themeApplicator)
    {
        _sessionService = sessionService;
        _themeApplicator = themeApplicator;
    }
    
    public void Init()
    {
        var state = _sessionService.LoadState();
        var theme = state.Theme;

        if (string.IsNullOrWhiteSpace(theme) || !_availableThemes.Contains(theme))
        {
            theme = "Dark";
        }
        
        SwitchThemeInternal(theme);
    }

    public async Task ChangeThemeAsync(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme)) return;
        if (!_availableThemes.Contains(theme)) return;
        if (CurrentTheme.Equals(theme, StringComparison.OrdinalIgnoreCase)) return;
        
        SwitchThemeInternal(theme);
        await _sessionService.SaveThemeAsync(theme);
    }
    
    private void SwitchThemeInternal(string theme)
    {
        CurrentTheme = theme;
        
        try
        {
            _themeApplicator.ApplyTheme(theme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error switching theme: {ex.Message}");
        }
    }
}