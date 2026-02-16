using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class LanguageService : ILanguageService
{
    private readonly ISessionService _sessionService;

    private readonly string[] _availableLanguages = { "en", "ua" };

    public CultureInfo CurrentCulture { get; private set; } = new("en");

    public LanguageService(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public void Init()
    {
        var state = _sessionService.LoadState();
        var lang = state.Language;

        if (string.IsNullOrWhiteSpace(lang) || !_availableLanguages.Contains(lang))
        {
            lang = "en";
        }
        
        SwitchLanguageInternal(lang, immediate: true);
    }

    public async Task ChangeLanguageAsync(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return;
        if (!_availableLanguages.Contains(cultureCode)) return;

        if (CurrentCulture.Name.Equals(cultureCode, StringComparison.OrdinalIgnoreCase)) return;
        
        SwitchLanguageInternal(cultureCode);

        await _sessionService.SaveLanguageAsync(cultureCode);
    }
    
    private void SwitchLanguageInternal(string cultureCode, bool immediate = false)
    {
        Action switchAction = () =>
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var newUri = new Uri($"avares://AvalonHttp/Assets/Lang/{cultureCode}.axaml");
                var dictionaries = app.Resources.MergedDictionaries;

                var existingDictionary = dictionaries.OfType<ResourceInclude>()
                    .FirstOrDefault(d => d.Source?.AbsolutePath?.Contains("/Assets/Lang/") == true);

                if (existingDictionary?.Source?.ToString() == newUri.ToString())
                {
                    CurrentCulture = new CultureInfo(cultureCode);
                    return;
                }
                
                var newResourceInclude = new ResourceInclude(newUri) { Source = newUri };
                dictionaries.Insert(0, newResourceInclude);
                
                if (existingDictionary != null)
                {
                    dictionaries.Remove(existingDictionary);
                }

                CurrentCulture = new CultureInfo(cultureCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching language: {ex.Message}");
            }
        };
        
        if (Dispatcher.UIThread.CheckAccess())
        {
            switchAction(); // Уже в UI потоке
        }
        else
        {
            Dispatcher.UIThread.Invoke(switchAction);
        }
    }
}