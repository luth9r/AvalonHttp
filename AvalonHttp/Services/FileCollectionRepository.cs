using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class FileCollectionRepository : ICollectionRepository
{
    private readonly string _collectionsFolder;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IFileNameSanitizer _fileNameSanitizer;
    
    private readonly ConcurrentDictionary<Guid, string> _filePathCache = new();
    
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public FileCollectionRepository(IFileNameSanitizer fileNameSanitizer)
    {
        _fileNameSanitizer = fileNameSanitizer;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _collectionsFolder = Path.Combine(appData, "AvalonHttp", "Collections");
        
        Directory.CreateDirectory(_collectionsFolder);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
    
    public async Task<List<ApiCollection>> LoadAllAsync()
    {
        var files = Directory.GetFiles(_collectionsFolder, "*.json");
        
        var loadTasks = files.Select(LoadCollectionFromFileAsync);
        var results = await Task.WhenAll(loadTasks);
        
        foreach (var collection in results.Where(c => c != null))
        {
            var filePath = GetFilePath(collection!.Id);
            _filePathCache[collection.Id] = filePath;
        }
        
        return results
            .Where(c => c != null)
            .Cast<ApiCollection>()
            .OrderBy(c => c.Name)
            .ToList();
    }
    
    public async Task<ApiCollection?> GetByIdAsync(Guid id)
    {
        var semaphore = GetLock(id);
        await semaphore.WaitAsync();
        
        try
        {
            var filePath = GetFilePath(id);
            
            if (!File.Exists(filePath))
                return null;

            return await LoadCollectionFromFileAsync(filePath);
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    public async Task SaveAsync(ApiCollection collection)
    {
        var semaphore = GetLock(collection.Id);
        await semaphore.WaitAsync();
        
        try
        {
            collection.UpdatedAt = DateTime.Now;
            
            var newFilePath = GenerateFilePath(collection);
            var tempFilePath = newFilePath + ".tmp";
            
            var oldFilePath = _filePathCache.TryGetValue(collection.Id, out var cached) 
                ? cached 
                : GetFilePath(collection.Id);

            try
            {
                System.Diagnostics.Debug.WriteLine($"=== SAVING COLLECTION: {collection.Name} ===");
                System.Diagnostics.Debug.WriteLine($"File path: {newFilePath}");
                System.Diagnostics.Debug.WriteLine($"Requests count: {collection.Requests.Count}");
                
                foreach (var request in collection.Requests)
                {
                    System.Diagnostics.Debug.WriteLine($"  Request: {request.Name}");
                    System.Diagnostics.Debug.WriteLine($"    Body: '{request.Body}' (length: {request.Body?.Length ?? 0})");
                    System.Diagnostics.Debug.WriteLine($"    Cookies: {request.Cookies?.Count ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"    Auth Type: {request.AuthData?.Type ?? "null"}");
                
                    if (request.AuthData != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"    Auth Details: User={request.AuthData.BasicUsername}, Token={request.AuthData.BearerToken}");
                    }
                }
                
                var json = JsonSerializer.Serialize(collection, _jsonOptions);
                
                System.Diagnostics.Debug.WriteLine($"JSON length: {json.Length} characters");
                System.Diagnostics.Debug.WriteLine($"JSON preview (first 1000 chars):");
                System.Diagnostics.Debug.WriteLine(json.Substring(0, Math.Min(1000, json.Length)));
                System.Diagnostics.Debug.WriteLine("---");
                
                await File.WriteAllTextAsync(tempFilePath, json);
                
                if (oldFilePath != newFilePath && File.Exists(oldFilePath))
                {
                    File.Delete(oldFilePath);
                }

                if (File.Exists(newFilePath))
                {
                    File.Delete(newFilePath);
                }
                
                File.Move(tempFilePath, newFilePath);
                
                _filePathCache[collection.Id] = newFilePath;
                
                System.Diagnostics.Debug.WriteLine($"✅ Collection saved successfully to: {newFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SAVE ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                
                throw new InvalidOperationException($"Failed to save collection: {ex.Message}", ex);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    public async Task DeleteAsync(Guid collectionId)
    {
        var semaphore = GetLock(collectionId);
        await semaphore.WaitAsync();
        
        try
        {
            var filePath = GetFilePath(collectionId);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            
            _filePathCache.TryRemove(collectionId, out _);
            
            // We don't need to dispose the lock, because it could lead to deadlocks
            //_locks.TryRemove(collectionId, out var removedLock);
            //removedLock?.Dispose();
        }
        finally
        {
            semaphore.Release();
        }
        
        await Task.CompletedTask;
    }

    private async Task<ApiCollection?> LoadCollectionFromFileAsync(string filePath)
{
    try
    {
        System.Diagnostics.Debug.WriteLine($"=== LOADING COLLECTION FROM: {filePath} ===");
        
        var json = await File.ReadAllTextAsync(filePath);
        
        System.Diagnostics.Debug.WriteLine($"JSON length: {json.Length} characters");
        System.Diagnostics.Debug.WriteLine($"JSON preview (first 1000 chars):");
        System.Diagnostics.Debug.WriteLine(json.Substring(0, Math.Min(1000, json.Length)));
        System.Diagnostics.Debug.WriteLine("---");
        
        var collection = JsonSerializer.Deserialize<ApiCollection>(json, _jsonOptions);
        
        if (collection != null)
        {
            System.Diagnostics.Debug.WriteLine($"✅ Loaded collection: {collection.Name}");
            System.Diagnostics.Debug.WriteLine($"Requests count: {collection.Requests?.Count ?? 0}");
            
            if (collection.Requests != null)
            {
                foreach (var request in collection.Requests)
                {
                    System.Diagnostics.Debug.WriteLine($"  Request: {request.Name}");
                    System.Diagnostics.Debug.WriteLine($"    Body: '{request.Body}' (length: {request.Body?.Length ?? 0})");
                    System.Diagnostics.Debug.WriteLine($"    Cookies: {request.Cookies?.Count ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"    Auth Type: {request.AuthData?.Type ?? "null"}");
                    
                    // ✅ Initialize null collections
                    request.Headers ??= new System.Collections.ObjectModel.ObservableCollection<AvalonHttp.Models.KeyValueItemModel>();
                    request.QueryParameters ??= new System.Collections.ObjectModel.ObservableCollection<AvalonHttp.Models.KeyValueItemModel>();
                    request.Cookies ??= new System.Collections.ObjectModel.ObservableCollection<AvalonHttp.Models.KeyValueItemModel>();
                    request.AuthData ??= new AuthData();
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("⚠️ Deserialization returned null");
        }
        
        return collection;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"❌ LOAD ERROR from {filePath}: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        return null;
    }
}

    private string GenerateFilePath(ApiCollection collection)
    {
        var sanitizedName = _fileNameSanitizer.Sanitize(collection.Name);
        var fileName = $"{sanitizedName}_{collection.Id}.json";
        return Path.Combine(_collectionsFolder, fileName);
    }

    private string GetFilePath(Guid collectionId)
    {
        if (_filePathCache.TryGetValue(collectionId, out var cached))
            return cached;
        
        var files = Directory.GetFiles(_collectionsFolder, $"*_{collectionId}.json");
        return files.FirstOrDefault() ?? Path.Combine(_collectionsFolder, $"{collectionId}.json");
    }
    
    private SemaphoreSlim GetLock(Guid collectionId)
    {
        return _locks.GetOrAdd(collectionId, _ => new SemaphoreSlim(1, 1));
    }
}
