using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;
using Environment = AvalonHttp.Models.EnvironmentAggregate.Environment;

namespace AvalonHttp.Services;

public class FileEnvironmentRepository : IEnvironmentRepository
{
    private readonly string _environmentsDirectory;
    private readonly string _activeEnvironmentFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileEnvironmentRepository()
    {
        var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "AvalonHttp");
        _environmentsDirectory = Path.Combine(appFolder, "Environments");
        _activeEnvironmentFile = Path.Combine(appFolder, "active-environment.json");

        Directory.CreateDirectory(_environmentsDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    // ========================================
    // Load Operations
    // ========================================
    
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

            foreach (var file in files)
            {
                try
                {
                    var env = await LoadEnvironmentFromFileAsync(file);
                    if (env != null)
                    {
                        environments.Add(env);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load environment from {file}: {ex.Message}");
                }
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
        var json = await File.ReadAllTextAsync(filePath);
        var env = JsonSerializer.Deserialize<Environment>(json, _jsonOptions);

        if (env == null)
        {
            return null;
        }

        // Initialize empty JSON if needed
        if (string.IsNullOrWhiteSpace(env.VariablesJson))
        {
            env.VariablesJson = "{\n  \n}";
        }

        // Validate JSON format
        if (!env.IsValidJson())
        {
            System.Diagnostics.Debug.WriteLine($"Environment '{env.Name}' has invalid JSON, resetting to empty object");
            env.VariablesJson = "{\n  \n}";
        }

        return env;
    }

    // ========================================
    // Save Operations
    // ========================================
    
    public async Task SaveAsync(Environment environment)
    {
        try
        {
            // Validate JSON before saving
            if (!environment.IsValidJson())
            {
                throw new InvalidOperationException($"Environment '{environment.Name}' contains invalid JSON");
            }

            environment.UpdatedAt = DateTime.UtcNow;

            var fileName = $"{SanitizeFileName(environment.Name)}_{environment.Id}.json";
            var filePath = Path.Combine(_environmentsDirectory, fileName);

            // Clean up old files with different names but same ID
            await CleanupOldFilesAsync(environment.Id, fileName);

            var json = JsonSerializer.Serialize(environment, _jsonOptions);
            
            // Write to temp file first, then rename (atomic operation)
            var tempFile = Path.Combine(_environmentsDirectory, $"{fileName}.tmp");
            await File.WriteAllTextAsync(tempFile, json);
            
            // Replace old file atomically
            File.Move(tempFile, filePath, overwrite: true);
            
            System.Diagnostics.Debug.WriteLine($"Saved environment '{environment.Name}' to: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save environment '{environment.Name}': {ex.Message}");
            throw;
        }
    }

    private async Task CleanupOldFilesAsync(Guid environmentId, string currentFileName)
    {
        try
        {
            var files = Directory.GetFiles(_environmentsDirectory, $"*_{environmentId}.json");

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (fileName != currentFileName)
                {
                    System.Diagnostics.Debug.WriteLine($"Deleting old file: {file}");
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup old files: {ex.Message}");
        }
    }

    // ========================================
    // Delete Operations
    // ========================================
    
    public async Task DeleteAsync(Guid environmentId)
    {
        try
        {
            var files = Directory.GetFiles(_environmentsDirectory, $"*_{environmentId}.json");

            foreach (var file in files)
            {
                File.Delete(file);
                System.Diagnostics.Debug.WriteLine($"Deleted environment file: {file}");
            }

            // Clear active environment if deleted
            var activeId = await GetActiveEnvironmentIdAsync();
            if (activeId == environmentId)
            {
                await SetActiveEnvironmentAsync(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete environment: {ex.Message}");
            throw;
        }
    }

    // ========================================
    // Active Environment
    // ========================================
    
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
    }

    private async Task<Guid?> GetActiveEnvironmentIdAsync()
    {
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

        return null;
    }

    // ========================================
    // Default Environments
    // ========================================
    
    public async Task<List<Environment>> EnsureDefaultEnvironmentsAsync()
    {
        var environments = await LoadAllAsync();
    
        System.Diagnostics.Debug.WriteLine($"Loaded {environments.Count} environments");
        
        // Ensure Global environment exists
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
            
            System.Diagnostics.Debug.WriteLine($"Global environment created with ID: {globalEnv.Id}");
        }

        // Sort: Globals first, then alphabetically
        var sorted = environments
            .OrderByDescending(e => e.IsGlobal)
            .ThenBy(e => e.Name)
            .ToList();
    
        System.Diagnostics.Debug.WriteLine($"Returning {sorted.Count} environments:");
        foreach (var env in sorted)
        {
            System.Diagnostics.Debug.WriteLine($"  - {env.Name} (IsGlobal: {env.IsGlobal})");
        }
    
        return sorted;
    }

    // ========================================
    // Helper Methods
    // ========================================
    
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        
        // Limit length to avoid issues with long file paths
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }
        
        return sanitized;
    }
}
