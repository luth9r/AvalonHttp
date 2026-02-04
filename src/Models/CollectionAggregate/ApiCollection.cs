using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AvalonHttp.Models.CollectionAggregate;

public class ApiCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Collection";
    public string Description { get; set; } = "";
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ObservableCollection<ApiRequest> Requests { get; set; } = new();
    
    public string GenerateUniqueRequestName(string baseName)
    {
        if (!Requests.Any(r => r.Name == baseName))
        {
            return baseName;
        }

        var counter = 1;
        string name;

        do
        {
            name = $"{baseName} ({counter++})";
        } while (Requests.Any(r => r.Name == name));

        return name;
    }
    
}