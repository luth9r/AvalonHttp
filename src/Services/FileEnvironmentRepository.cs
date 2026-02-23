using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;
using Environment = AvalonHttp.Models.EnvironmentAggregate.Environment;

namespace AvalonHttp.Services;

public class FileEnvironmentRepository : IEnvironmentRepository
{
    private readonly string _environmentsDirectory;
    private readonly string _activeEnvironmentFile;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IFileNameSanitizer _fileNameSanitizer;

    private readonly ConcurrentDictionary<Guid, string> _filePathCache = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly SemaphoreSlim _activeEnvLock = new(1, 1);

    public FileEnvironmentRepository(IFileNameSanitizer fileNameSanitizer, string? basePath = null)
    {
        _fileNameSanitizer = fileNameSanitizer;
        
        var appFolder = basePath ?? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "AvalonHttp");
        _environmentsDirectory = Path.Combine(appFolder, "Environments");
        _activeEnvironmentFile = Path.Combine(appFolder, "active-environment.json");

        Directory.CreateDirectory(_environmentsDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<Environment>> LoadAllAsync()
    {
        var environments = new List<Environment>();

        try
        {
            if (!Directory.Exists(_environmentsDirectory))
            {
                return environments;
            }

            var files = Directory.GetFiles(_environmentsDirectory, "*.json");

            var loadTasks = files.Select(LoadEnvironmentFromFileAsync);
            var results = await Task.WhenAll(loadTasks);
            
            foreach (var env in results.Where(e => e != null))
            {
                var filePath = GetFilePath(env!.Id);
                _filePathCache[env.Id] = filePath;
                environments.Add(env);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load environments: {ex.Message}");
        }

        return environments;
    }

    private async Task<Environment?> LoadEnvironmentFromFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var env = JsonSerializer.Deserialize<Environment>(json, _jsonOptions);

            if (env == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(env.VariablesJson))
            {
                env.VariablesJson = "{\n  \n}";
            }

            if (!env.IsValidJson())
            {
                System.Diagnostics.Debug.WriteLine($"Environment '{env.Name}' has invalid JSON, resetting to empty object");
                env.VariablesJson = "{\n  \n}";
            }

            return env;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load environment from {filePath}: {ex.Message}");
            return null;
        }
    }

    public async Task SaveAsync(Environment environment)
    {
        var semaphore = GetLock(environment.Id);
        await semaphore.WaitAsync();

        try
        {
            if (!environment.IsValidJson())
            {
                throw new InvalidOperationException($"Environment '{environment.Name}' contains invalid JSON");
            }

            environment.UpdatedAt = DateTime.UtcNow;

            var newFilePath = GenerateFilePath(environment);
            var tempFilePath = newFilePath + ".tmp";

            var oldFilePath = _filePathCache.TryGetValue(environment.Id, out var cached) 
                ? cached 
                : GetFilePath(environment.Id);

            try
            {
                var json = JsonSerializer.Serialize(environment, _jsonOptions);
                await File.WriteAllTextAsync(tempFilePath, json);
                
                File.Move(tempFilePath, newFilePath, overwrite: true);

                if (oldFilePath != newFilePath && File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { }
                }

                _filePathCache[environment.Id] = newFilePath;
                
                System.Diagnostics.Debug.WriteLine($"Saved environment '{environment.Name}' to: {newFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save environment '{environment.Name}': {ex.Message}");
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                throw;
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task DeleteAsync(Guid environmentId)
    {
        var semaphore = GetLock(environmentId);
        await semaphore.WaitAsync();

        try
        {
            var filePath = GetFilePath(environmentId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            _filePathCache.TryRemove(environmentId, out _);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete environment: {ex.Message}");
            throw;
        }
        finally
        {
            semaphore.Release();
        }

        var activeId = await GetActiveEnvironmentIdAsync();
        if (activeId == environmentId)
        {
            await SetActiveEnvironmentAsync(null);
        }
    }

    public async Task<Environment?> GetActiveEnvironmentAsync()
    {
        var activeId = await GetActiveEnvironmentIdAsync();

        if (activeId == null)
        {
            return null;
        }

        var environments = await LoadAllAsync();
        return environments.FirstOrDefault(e => e.Id == activeId);
    }

    public async Task SetActiveEnvironmentAsync(Guid? environmentId)
    {
        await _activeEnvLock.WaitAsync();
        try
        {
            var data = new { ActiveEnvironmentId = environmentId };
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(_activeEnvironmentFile, json);
            
            System.Diagnostics.Debug.WriteLine($"Set active environment to: {environmentId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set active environment: {ex.Message}");
        }
        finally
        {
            _activeEnvLock.Release();
        }
    }

    private async Task<Guid?> GetActiveEnvironmentIdAsync()
    {
        await _activeEnvLock.WaitAsync();
        try
        {
            if (!File.Exists(_activeEnvironmentFile))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_activeEnvironmentFile);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions);

            if (data != null && data.TryGetValue("ActiveEnvironmentId", out var idElement))
            {
                if (idElement.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(idElement.GetString(), out var id))
                {
                    return id;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get active environment: {ex.Message}");
        }
        finally
        {
            _activeEnvLock.Release();
        }

        return null;
    }

    public async Task<List<Environment>> EnsureDefaultEnvironmentsAsync()
    {
        await _activeEnvLock.WaitAsync();
        try
        {
            var environments = await LoadAllAsync();
    
            if (!environments.Any(e => e.IsGlobal))
            {
                System.Diagnostics.Debug.WriteLine("Creating Global environment...");
            
                var globalEnv = new Environment
                {
                    Id = Guid.NewGuid(),
                    Name = "Globals",
                    IsGlobal = true,
                    VariablesJson = "{\n  \"appName\": \"AvalonHttp\"\n}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await SaveAsync(globalEnv);
                environments.Insert(0, globalEnv);
            }

            return environments
                .OrderByDescending(e => e.IsGlobal)
                .ThenBy(e => e.Name)
                .ToList();
        }
        finally
        {
            _activeEnvLock.Release();
        }
    }

    private string GenerateFilePath(Environment environment)
    {
        var sanitizedName = _fileNameSanitizer.Sanitize(environment.Name);
        var fileName = $"{sanitizedName}_{environment.Id}.json";
        return Path.Combine(_environmentsDirectory, fileName);
    }

    private string GetFilePath(Guid environmentId)
    {
        if (_filePathCache.TryGetValue(environmentId, out var cached))
        {
            return cached;
        }

        var files = Directory.GetFiles(_environmentsDirectory, $"*_{environmentId}.json");
        return files.FirstOrDefault() ?? Path.Combine(_environmentsDirectory, $"{environmentId}.json");
    }

    private SemaphoreSlim GetLock(Guid environmentId)
    {
        return _locks.GetOrAdd(environmentId, _ => new SemaphoreSlim(1, 1));
    }
}
