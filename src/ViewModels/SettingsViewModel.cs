using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ILanguageService _languageService;
    private readonly IThemeService _themeService;

    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new();
    public ObservableCollection<ThemeOption> AvailableThemes { get; } = new();

    [ObservableProperty]
    private LanguageItem? _selectedLanguage;

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    public SettingsViewModel(ILanguageService languageService, IThemeService themeService)
    {
        _languageService = languageService;
        _themeService = themeService;

        // Populate languages
        AvailableLanguages.Add(new LanguageItem("English", "en"));
        AvailableLanguages.Add(new LanguageItem("Українська", "ua"));

        AvailableThemes.Add(new ThemeOption("Light"));
        AvailableThemes.Add(new ThemeOption("Dark"));
        
        // Set initial selection
        SelectedLanguage = AvailableLanguages.FirstOrDefault(x => x.Code == _languageService.CurrentCulture.Name) 
                           ?? AvailableLanguages.FirstOrDefault();
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Name == _themeService.CurrentTheme);
        
    }

    async partial void OnSelectedLanguageChanged(LanguageItem? value)
    {
        if (value != null)
        {
            await _languageService.ChangeLanguageAsync(value.Code);
        }
    }
    
    async partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (value != null)
        {
            await _themeService.ChangeThemeAsync(value.Name);
        }
    }
}

public record LanguageItem(string Name, string Code);
public record ThemeOption(string Name);
