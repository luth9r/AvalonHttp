using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Services.Interfaces;
using Environment = System.Environment;

namespace AvalonHttp.Services;

public class SessionService : ISessionService
{
    private readonly IFileStorage<AppState> _storage;
    private CancellationTokenSource? _debounceCts;

    public SessionService(IFileStorage<AppState> storage)
    {
        _storage = storage;
    }

    public async Task SaveLastRequestAsync(Guid requestId)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;

        try
        {
            await Task.Delay(500, ct);

            await _storage.UpdateAsync(state => state.LastSelectedRequestId = requestId);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public Task SaveLanguageAsync(string languageCode)
    {
        return _storage.UpdateAsync(state => state.Language = languageCode);
    }

    public Task SaveThemeAsync(string theme)
    {
        return _storage.UpdateAsync(state => state.Theme = theme);
    }

    public Task<AppState> LoadStateAsync() => _storage.LoadAsync();
    
    public AppState LoadState() => _storage.Load();

    public Task ClearStateAsync() => _storage.ClearAsync();
}
