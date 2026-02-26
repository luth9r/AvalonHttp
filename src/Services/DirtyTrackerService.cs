using System.Text.Json;
using System.Text.Json.Serialization;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.Services;

public class DirtyTrackerService : IDirtyTrackerService
{
    private readonly JsonSerializerOptions _options = AvalonHttp.Helpers.JsonSettings.IgnoreCycles;

    public string TakeSnapshot<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, _options);
    }

    public bool IsDirty<T>(T obj, string snapshot)
    {
        if (string.IsNullOrEmpty(snapshot))
        {
            return false;
        }

        var current = JsonSerializer.Serialize(obj, _options);
        return current != snapshot;
    }
}