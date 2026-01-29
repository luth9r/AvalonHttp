using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AvalonHttp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<ApiCollection> _collections = new();

    [ObservableProperty]
    private ApiRequest? _selectedRequest;

    [ObservableProperty]
    private string _requestBody = "";

    [ObservableProperty]
    private string _responseContent = "No response yet";

    [ObservableProperty]
    private string _statusCode = "Ready";

    [ObservableProperty]
    private string _responseTime = "Time: --";

    [ObservableProperty]
    private string _responseHeaders = "";

    [ObservableProperty]
    private string _selectedMethodString = "GET";

    public ObservableCollection<string> HttpMethods { get; } = new() 
        { "GET", "POST", "PUT", "DELETE", "PATCH" };

    public MainWindowViewModel()
    {
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        var collection = new ApiCollection { Name = "JSONPlaceholder API" };
        collection.Requests.Add(new ApiRequest 
        { 
            Name = "Get Posts", 
            Url = "https://jsonplaceholder.typicode.com/posts", 
            Method = Models.HttpMethod.GET 
        });
        collection.Requests.Add(new ApiRequest 
        { 
            Name = "Get Post #1", 
            Url = "https://jsonplaceholder.typicode.com/posts/1", 
            Method = Models.HttpMethod.GET 
        });
        
        Collections.Add(collection);
        SelectedRequest = Collections[0].Requests[0];
    }

    [RelayCommand]
    private async Task SendRequest()
    {
        if (SelectedRequest == null) return;
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "AvalonHttp/1.0");
        
        try 
        {
            StatusCode = "Sending...";
            ResponseContent = "Loading...";
            
            var startTime = DateTime.Now;
            
            HttpResponseMessage response = SelectedRequest.Method switch
            {
                Models.HttpMethod.GET => await client.GetAsync(SelectedRequest.Url),
                Models.HttpMethod.POST => await client.PostAsync(SelectedRequest.Url, 
                    new StringContent(RequestBody, System.Text.Encoding.UTF8, "application/json")),
                Models.HttpMethod.PUT => await client.PutAsync(SelectedRequest.Url, 
                    new StringContent(RequestBody, System.Text.Encoding.UTF8, "application/json")),
                Models.HttpMethod.DELETE => await client.DeleteAsync(SelectedRequest.Url),
                _ => await client.GetAsync(SelectedRequest.Url)
            };
            
            var elapsedTime = (DateTime.Now - startTime).TotalMilliseconds;
            
            var content = await response.Content.ReadAsStringAsync();
            
            // Форматуємо JSON
            try
            {
                var jsonDoc = JsonDocument.Parse(content);
                ResponseContent = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch 
            {
                ResponseContent = content;
            }
            
            StatusCode = $"{(int)response.StatusCode} {response.ReasonPhrase}";
            ResponseTime = $"Time: {elapsedTime:F0}ms";
            
            var headers = string.Join("\n", 
                response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"));
            ResponseHeaders = headers;
        } 
        catch (Exception ex) 
        {
            ResponseContent = $"Error: {ex.Message}\n\n{ex.StackTrace}";
            StatusCode = "Error";
            ResponseTime = "Time: --";
        }
    }
}
