using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvalonHttp.Services;

public class AppState
{
    public Guid? LastSelectedRequestId { get; set; }
}

public class SessionService
{
    private const string FileName = "app-state.json";
    private readonly string _filePath;

    public SessionService()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, FileName);
    }

    public async Task SaveLastRequestAsync(Guid requestId)
    {
        var state = await LoadStateAsync();
        state.LastSelectedRequestId = requestId;
        
        var json = JsonSerializer.Serialize(state);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task<AppState> LoadStateAsync()
    {
        if (!File.Exists(_filePath))
            return new AppState();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }
}