using System.Globalization;
using System.Threading.Tasks;

namespace AvalonHttp.Services.Interfaces;

public interface ILanguageService
{
    Task ChangeLanguageAsync(string cultureCode);
    Task InitAsync();
    CultureInfo CurrentCulture { get; }
}
