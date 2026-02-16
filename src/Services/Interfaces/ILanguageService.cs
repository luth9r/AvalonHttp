using System.Globalization;
using System.Threading.Tasks;

namespace AvalonHttp.Services.Interfaces;

public interface ILanguageService
{
    Task ChangeLanguageAsync(string cultureCode);
    void Init();
    CultureInfo CurrentCulture { get; }
}
