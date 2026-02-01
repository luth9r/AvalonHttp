using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class SessionService : ISessionService, IDisposable
{
    private const string FileName = "app-state.json";
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    
    // Cache to avoid excessive file reads
    private AppState? _cachedState;
    private DateTime _lastCacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(5);

    public SessionService()
    {
        // Use proper AppData folder
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataFolder, "AvalonHttp");
        
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, FileName);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task SaveLastRequestAsync(Guid requestId)
    {
        await _fileLock.WaitAsync();
        
        try
        {
            // Load current state to preserve other properties
            var state = await LoadStateInternalAsync();
            state.LastSelectedRequestId = requestId;
            state.LastUpdated = DateTime.UtcNow;
            
            await SaveStateInternalAsync(state);
            
            // Update cache
            _cachedState = state;
            _lastCacheTime = DateTime.UtcNow;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<AppState> LoadStateAsync()
    {
        await _fileLock.WaitAsync();
        
        try
        {
            // Return cached state if fresh
            if (_cachedState != null && 
                DateTime.UtcNow - _lastCacheTime < _cacheLifetime)
            {
                return _cachedState;
            }

            var state = await LoadStateInternalAsync();
            
            // Update cache
            _cachedState = state;
            _lastCacheTime = DateTime.UtcNow;
            
            return state;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ClearStateAsync()
    {
        await _fileLock.WaitAsync();
        
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            
            _cachedState = null;
            _lastCacheTime = DateTime.MinValue;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<AppState> LoadStateInternalAsync()
    {
        if (!File.Exists(_filePath))
            return new AppState();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load app state: {ex.Message}");
            
            // Try to restore from backup
            var backupPath = _filePath + ".backup";
            if (File.Exists(backupPath))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(backupPath);
                    return JsonSerializer.Deserialize<AppState>(backupJson, _jsonOptions) ?? new AppState();
                }
                catch
                {
                    // Backup also corrupted
                }
            }
            
            return new AppState();
        }
    }

    private async Task SaveStateInternalAsync(AppState state)
    {
        var tempPath = _filePath + ".tmp";
        var backupPath = _filePath + ".backup";

        try
        {
            // 1. Write to temporary file
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(tempPath, json);

            // 2. Backup existing file
            if (File.Exists(_filePath))
            {
                File.Copy(_filePath, backupPath, overwrite: true);
            }

            // 3. Replace original with temporary (atomic on most systems)
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save app state: {ex.Message}");
            
            // Cleanup temporary file if exists
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            
            throw;
        }
    }

    public void Dispose()
    {
        _fileLock?.Dispose();
    }
}
