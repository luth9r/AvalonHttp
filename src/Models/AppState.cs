using System;

namespace AvalonHttp.Models;

public class AppState
{
    public Guid? LastSelectedRequestId { get; set; }
    public Guid? LastSelectedCollectionId { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? Language { get; set; }
    
    public string Theme { get; set; } = "Dark";
    
    // Future: Add more session data here
    // public WindowPosition? WindowPosition { get; set; }
    // public Dictionary<string, string>? UserPreferences { get; set; }
}