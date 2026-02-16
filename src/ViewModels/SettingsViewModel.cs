using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ILanguageService _languageService;

    public ObservableCollection<LanguageItem> AvailableLanguages { get; } = new();

    [ObservableProperty]
    private LanguageItem? _selectedLanguage;

    public SettingsViewModel(ILanguageService languageService)
    {
        _languageService = languageService;

        // Populate languages
        AvailableLanguages.Add(new LanguageItem("English", "en"));
        AvailableLanguages.Add(new LanguageItem("Українська", "ua"));

        // Set initial selection
        SelectedLanguage = AvailableLanguages.FirstOrDefault(x => x.Code == _languageService.CurrentCulture.Name) 
                           ?? AvailableLanguages.FirstOrDefault();
    }

    async partial void OnSelectedLanguageChanged(LanguageItem? value)
    {
        if (value != null)
        {
            await _languageService.ChangeLanguageAsync(value.Code);
        }
    }
}

public record LanguageItem(string Name, string Code);
