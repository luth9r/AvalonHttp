using System.Text.Json;
using System.Text.Json.Serialization;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class DirtyTrackerService : IDirtyTrackerService
{
    private readonly JsonSerializerOptions _options = new() 
    { 
        WriteIndented = false,
        
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public string TakeSnapshot(ApiRequest request)
    {
        return JsonSerializer.Serialize(request, _options);
    }

    public bool IsDirty(ApiRequest request, string snapshot)
    {
        if (string.IsNullOrEmpty(snapshot)) return false;
        var current = JsonSerializer.Serialize(request, _options);
        return current != snapshot;
    }
}