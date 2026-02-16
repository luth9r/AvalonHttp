using System.Threading.Tasks;

namespace AvalonHttp.Services.Interfaces;

public interface IThemeService
{
    string CurrentTheme { get; }
    void Init();
    Task ChangeThemeAsync(string theme);
}