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

    public async Task InitAsync()
    {
        var state = await _sessionService.LoadStateAsync();
        var lang = state.Language;
        
        if (string.IsNullOrWhiteSpace(lang) || !_availableLanguages.Contains(lang))
        {
            lang = "en";
        }
        
        SwitchLanguageInternal(lang);
    }

    public async Task ChangeLanguageAsync(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return;
        if (!_availableLanguages.Contains(cultureCode)) return;

        if (CurrentCulture.Name.Equals(cultureCode, StringComparison.OrdinalIgnoreCase)) return;
        
        SwitchLanguageInternal(cultureCode);

        await _sessionService.SaveLanguageAsync(cultureCode);
    }
    
    private void SwitchLanguageInternal(string cultureCode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                var newUri = new Uri($"avares://AvalonHttp/Assets/Lang/{cultureCode}.axaml");
                var dictionaries = app.Resources.MergedDictionaries;

                var existingDictionary = dictionaries.OfType<ResourceInclude>()
                    .FirstOrDefault(d => d.Source?.AbsolutePath?.Contains("/Assets/Lang/") == true);

                if (existingDictionary != null)
                {
                    if (existingDictionary.Source == newUri) return;
                    dictionaries.Remove(existingDictionary);
                }
                
                var newResourceInclude = new ResourceInclude(newUri) { Source = newUri };
                dictionaries.Add(newResourceInclude);
                
                CurrentCulture = new CultureInfo(cultureCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching language: {ex.Message}");
            }
        });
    }
}