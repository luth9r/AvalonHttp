using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;

namespace AvalonHttp.Services;

public class CollectionService
{
    private readonly string _collectionsFolder;
    private readonly JsonSerializerOptions _jsonOptions;

    public CollectionService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _collectionsFolder = Path.Combine(appData, "AvalonHttp", "Collections");
        
        if (!Directory.Exists(_collectionsFolder))
        {
            Directory.CreateDirectory(_collectionsFolder);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<ApiCollection>> LoadAllAsync()
    {
        var collections = new List<ApiCollection>();

        if (!Directory.Exists(_collectionsFolder))
            return collections;

        var files = Directory.GetFiles(_collectionsFolder, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var collection = JsonSerializer.Deserialize<ApiCollection>(json, _jsonOptions);
                
                if (collection != null)
                {
                    collections.Add(collection);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load: {ex.Message}");
            }
        }

        return collections.OrderBy(c => c.Name).ToList();
    }

    public async Task SaveAsync(ApiCollection collection)
    {
        collection.UpdatedAt = DateTime.Now;
        
        var fileName = $"{SanitizeFileName(collection.Name)}_{collection.Id}.json";
        var filePath = Path.Combine(_collectionsFolder, fileName);

        var json = JsonSerializer.Serialize(collection, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task DeleteAsync(Guid collectionId)
    {
        var files = Directory.GetFiles(_collectionsFolder, $"*_{collectionId.ToString()}.json");
        
        foreach (var file in files)
        {
            File.Delete(file);
        }
        
        await Task.CompletedTask;
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}