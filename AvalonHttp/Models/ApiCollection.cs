using System.Collections.ObjectModel;

namespace AvalonHttp.Models;

public class ApiCollection
{
    public string Name { get; set; } = "New Collection";
    public ObservableCollection<ApiRequest> Requests { get; set; } = new();
}