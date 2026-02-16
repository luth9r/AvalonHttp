using System;
using System.Threading.Tasks;
using AvalonHttp.Models;

namespace AvalonHttp.Services.Interfaces;

public interface ISessionService
{
    Task SaveLastRequestAsync(Guid requestId);
    Task SaveLanguageAsync(string languageCode);
    Task<AppState> LoadStateAsync();

    AppState LoadState();
    
    Task ClearStateAsync();
}