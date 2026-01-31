using System;
using System.Collections.ObjectModel;

namespace AvalonHttp.Models.CollectionAggregate;

public class ApiCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Collection";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ObservableCollection<ApiRequest> Requests { get; set; } = new();
}