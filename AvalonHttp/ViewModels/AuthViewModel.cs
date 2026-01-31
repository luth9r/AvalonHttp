using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedAuthType = "None";
    
    // Basic Auth
    [ObservableProperty]
    private string _basicUsername = "";
    
    [ObservableProperty]
    private string _basicPassword = "";
    
    // Bearer Token
    [ObservableProperty]
    private string _bearerToken = "";
    
    // API Key
    [ObservableProperty]
    private string _apiKeyName = "X-API-Key";
    
    [ObservableProperty]
    private string _apiKeyValue = "";
    
    [ObservableProperty]
    private string _apiKeyLocation = "Header"; // Header or Query
    
    public List<string> AuthTypes { get; } = new() 
    { 
        "None", 
        "Basic Auth", 
        "Bearer Token", 
        "API Key" 
    };
    
    public List<string> ApiKeyLocations { get; } = new() 
    { 
        "Header", 
        "Query Parameter" 
    };

    /// <summary>
    /// Gets the authentication headers based on the selected authentication type.
    /// </summary>
    public Dictionary<string, string> GetAuthHeaders()
    {
        var headers = new Dictionary<string, string>();

        switch (SelectedAuthType)
        {
            case "Basic Auth":
                if (!string.IsNullOrWhiteSpace(BasicUsername) || !string.IsNullOrWhiteSpace(BasicPassword))
                {
                    var credentials = $"{BasicUsername}:{BasicPassword}";
                    var base64 = System.Convert.ToBase64String(
                        System.Text.Encoding.UTF8.GetBytes(credentials));
                    headers["Authorization"] = $"Basic {base64}";
                }
                break;

            case "Bearer Token":
                if (!string.IsNullOrWhiteSpace(BearerToken))
                {
                    headers["Authorization"] = $"Bearer {BearerToken}";
                }
                break;

            case "API Key":
                if (ApiKeyLocation == "Header" && !string.IsNullOrWhiteSpace(ApiKeyName) 
                    && !string.IsNullOrWhiteSpace(ApiKeyValue))
                {
                    headers[ApiKeyName] = ApiKeyValue;
                }
                break;
        }

        return headers;
    }
    
    public Dictionary<string, string> GetAuthQueryParams()
    {
        var queryParams = new Dictionary<string, string>();

        if (SelectedAuthType == "API Key" 
            && ApiKeyLocation == "Query Parameter" 
            && !string.IsNullOrWhiteSpace(ApiKeyName) 
            && !string.IsNullOrWhiteSpace(ApiKeyValue))
        {
            queryParams[ApiKeyName] = ApiKeyValue;
        }

        return queryParams;
    }
}
