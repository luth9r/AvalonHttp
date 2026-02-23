using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class GenericFileStorage<T> : IFileStorage<T>, IDisposable where T : class, new()
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    
    private T? _cachedState;
    private DateTime _lastCacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime;
    
    public GenericFileStorage(string fileName, string? basePath = null, TimeSpan? cacheLifetime = null)
    {
        var appFolder = basePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvalonHttp");
        Directory.CreateDirectory(appFolder);
        
        _filePath = Path.Combine(appFolder, fileName);
        _cacheLifetime = cacheLifetime ?? TimeSpan.FromSeconds(5);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
    
    public async Task<T> LoadAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (_cachedState != null && DateTime.UtcNow - _lastCacheTime < _cacheLifetime)
                return _cachedState;

            var state = await LoadInternalAsync();
            UpdateCache(state);
            return state;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public T Load()
    {
        _fileLock.Wait();
        try
        {
            if (_cachedState != null && DateTime.UtcNow - _lastCacheTime < _cacheLifetime)
                return _cachedState;

            if (!File.Exists(_filePath)) return new T();
        
            var json = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        
            UpdateCache(state);
            return state;
        }
        catch
        {
            return new T();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task UpdateAsync(Action<T> updateAction)
    {
        await _fileLock.WaitAsync();
        try
        {
            var state = await LoadInternalAsync();
            
            updateAction(state);
            
            await SaveInternalAsync(state);
            UpdateCache(state);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            _cachedState = null;
            _lastCacheTime = DateTime.MinValue;
        }
        finally
        {
            _fileLock.Release();
        }
    }
    
    private async Task<T> LoadInternalAsync()
    {
        if (!File.Exists(_filePath)) return new T();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        }
        catch (Exception)
        {
            var backupPath = _filePath + ".backup";
            if (File.Exists(backupPath))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(backupPath);
                    return JsonSerializer.Deserialize<T>(backupJson, _jsonOptions) ?? new T();
                }
                catch { /* Backup corrupted too */ }
            }
            return new T();
        }
    }

    private async Task SaveInternalAsync(T state)
    {
        var tempPath = _filePath + ".tmp";
        var backupPath = _filePath + ".backup";

        try
        {
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(tempPath, json);

            if (File.Exists(_filePath)) File.Copy(_filePath, backupPath, overwrite: true);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception)
        {
            if (File.Exists(tempPath)) { try { File.Delete(tempPath); } catch { } }
            throw;
        }
    }

    private void UpdateCache(T state)
    {
        _cachedState = state;
        _lastCacheTime = DateTime.UtcNow;
    }

    public void Dispose()
    {
        _fileLock?.Dispose();
    }
}