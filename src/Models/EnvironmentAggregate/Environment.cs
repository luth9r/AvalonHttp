using System;
using System.Collections.Generic;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.Models.EnvironmentAggregate;

public partial class Environment : ObservableObject
{
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _variablesJson = "{\n  \n}";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private DateTime _updatedAt;
    
    [ObservableProperty]
    private bool _isGlobal;

    // ========================================
    // Cached Variables
    // ========================================
    
    private Dictionary<string, string>? _cachedVariables;
    private string? _lastParsedJson;

    // Invalidate cache when VariablesJson changes
    partial void OnVariablesJsonChanged(string value)
    {
        _cachedVariables = null;
        _lastParsedJson = null;
    }

    // ========================================
    // Public Methods
    // ========================================
    
    public Dictionary<string, string> GetVariables()
    {
        // Return cached result if JSON hasn't changed
        if (_cachedVariables != null && _lastParsedJson == VariablesJson)
        {
            return _cachedVariables;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(VariablesJson))
            {
                _cachedVariables = new Dictionary<string, string>();
                _lastParsedJson = VariablesJson;
                return _cachedVariables;
            }

            // Properly dispose JsonDocument to avoid memory leaks
            using var jsonDoc = JsonDocument.Parse(VariablesJson);
            var variables = new Dictionary<string, string>();

            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? "",
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => "",
                    _ => property.Value.GetRawText()
                };
                
                variables[property.Name] = value;
            }

            // Cache the result
            _cachedVariables = variables;
            _lastParsedJson = VariablesJson;
            
            return variables;
        }
        catch
        {
            _cachedVariables = new Dictionary<string, string>();
            _lastParsedJson = VariablesJson;
            return _cachedVariables;
        }
    }
    
    public bool IsValidJson()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(VariablesJson))
            {
                return true;
            }

            // Properly dispose JsonDocument
            using var jsonDoc = JsonDocument.Parse(VariablesJson);
            
            // Check if root is an object (not array or primitive)
            return jsonDoc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }

    // ========================================
    // Helper Methods
    // ========================================
    
    /// <summary>
    /// Sets variables from dictionary and updates JSON
    /// </summary>
    public void SetVariables(Dictionary<string, string> variables)
    {
        var jsonObject = new Dictionary<string, object>();
        
        foreach (var kvp in variables)
        {
            // Try to preserve type information
            if (bool.TryParse(kvp.Value, out bool boolValue))
            {
                jsonObject[kvp.Key] = boolValue;
            }
            else if (double.TryParse(kvp.Value, out double numValue))
            {
                jsonObject[kvp.Key] = numValue;
            }
            else
            {
                jsonObject[kvp.Key] = kvp.Value;
            }
        }

        VariablesJson = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
