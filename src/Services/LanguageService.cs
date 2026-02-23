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
    private readonly ILanguageApplicator _languageApplicator;

    private readonly string[] _availableLanguages = { "en", "ua" };

    public CultureInfo CurrentCulture { get; private set; } = new("en");

    public LanguageService(ISessionService sessionService, ILanguageApplicator languageApplicator)
    {
        _sessionService = sessionService;
        _languageApplicator = languageApplicator;
    }

    public void Init()
    {
        var state = _sessionService.LoadState();
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
        CurrentCulture = new CultureInfo(cultureCode);

        _languageApplicator.ApplyLanguage(cultureCode);
    }
}