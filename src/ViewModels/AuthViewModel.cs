using System;
using System.Collections.Generic;
using System.Text;
using AvalonHttp.Common.Constants;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    // ========================================
    // Observable Properties
    // ========================================
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBasicAuthSelected))]
    [NotifyPropertyChangedFor(nameof(IsBearerTokenSelected))]
    [NotifyPropertyChangedFor(nameof(IsApiKeySelected))]
    [NotifyPropertyChangedFor(nameof(HasAuthentication))]
    private string _selectedAuthType = AuthConstants.None;
    
    [ObservableProperty]
    private string _basicUsername = string.Empty;
    
    [ObservableProperty]
    private string _basicPassword = string.Empty;
    
    [ObservableProperty]
    private string _bearerToken = string.Empty;
    
    [ObservableProperty]
    private string _apiKeyName = AuthConstants.ApiKeyPrefix;
    
    [ObservableProperty]
    private string _apiKeyValue = string.Empty;
    
    [ObservableProperty]
    private string _apiKeyLocation = AuthConstants.LocationHeader;

    // ========================================
    // Computed Properties
    // ========================================
    
    public bool IsBasicAuthSelected => SelectedAuthType == AuthConstants.Basic;
    public bool IsBearerTokenSelected => SelectedAuthType == AuthConstants.Bearer;
    public bool IsApiKeySelected => SelectedAuthType == AuthConstants.ApiKey;
    public bool HasAuthentication => SelectedAuthType != AuthConstants.None && IsValid();
    
    public List<string> AuthTypes { get; } = new() 
    { 
        AuthConstants.None, 
        AuthConstants.Basic, 
        AuthConstants.Bearer, 
        AuthConstants.ApiKey 
    };
    
    public List<string> ApiKeyLocations { get; } = new() 
    { 
        AuthConstants.LocationHeader, 
        AuthConstants.LocationQuery 
    };

    // ========================================
    // Get Auth Headers
    // ========================================
    
    public Dictionary<string, string> GetAuthHeaders(Func<string, string>? resolver = null)
    {
        var headers = new Dictionary<string, string>();

        try
        {
            switch (SelectedAuthType)
            {
                case var _ when SelectedAuthType == AuthConstants.Basic:
                    AddBasicAuthHeader(headers, resolver);
                    break;

                case var _ when SelectedAuthType == AuthConstants.Bearer:
                    AddBearerTokenHeader(headers, resolver);
                    break;

                case var _ when SelectedAuthType == AuthConstants.ApiKey 
                    && ApiKeyLocation == AuthConstants.LocationHeader:
                    AddApiKeyHeader(headers, resolver);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to generate auth headers: {ex.Message}");
        }

        return headers;
    }

    private void AddBasicAuthHeader(Dictionary<string, string> headers, Func<string, string>? resolver)
    {
        var username = Resolve(BasicUsername, resolver);
        var password = Resolve(BasicPassword, resolver);
        
        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{password}"));
        
        headers["Authorization"] = $"Basic {credentials}";
    }

    private void AddBearerTokenHeader(Dictionary<string, string> headers, Func<string, string>? resolver)
    {
        var token = Resolve(BearerToken, resolver);
        
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        headers["Authorization"] = $"Bearer {token}";
    }

    private void AddApiKeyHeader(Dictionary<string, string> headers, Func<string, string>? resolver)
    {
        var keyName = Resolve(ApiKeyName, resolver);
        var keyValue = Resolve(ApiKeyValue, resolver);
        
        if (string.IsNullOrWhiteSpace(keyName) || string.IsNullOrWhiteSpace(keyValue))
        {
            return;
        }

        headers[keyName] = keyValue;
    }

    // ========================================
    // Get Auth Query Params
    // ========================================
    
    public Dictionary<string, string> GetAuthQueryParams(Func<string, string>? resolver = null)
    {
        var queryParams = new Dictionary<string, string>();

        if (SelectedAuthType == AuthConstants.ApiKey 
            && ApiKeyLocation == AuthConstants.LocationQuery)
        {
            var keyName = Resolve(ApiKeyName, resolver);
            var keyValue = Resolve(ApiKeyValue, resolver);
            
            if (!string.IsNullOrWhiteSpace(keyName) && !string.IsNullOrWhiteSpace(keyValue))
            {
                queryParams[keyName] = keyValue;
            }
        }

        return queryParams;
    }

    // ========================================
    // Helper Methods
    // ========================================
    
    private static string Resolve(string value, Func<string, string>? resolver)
    {
        return resolver?.Invoke(value) ?? value;
    }

    // ========================================
    // Model Conversion
    // ========================================
    
    public void LoadFromAuthData(AuthData? authData)
    {
        if (authData == null)
        {
            Clear();
            return;
        }

        try
        {
            SelectedAuthType = authData.Type ?? AuthConstants.None;
            BasicUsername = authData.BasicUsername ?? string.Empty;
            BasicPassword = authData.BasicPassword ?? string.Empty;
            BearerToken = authData.BearerToken ?? string.Empty;
            ApiKeyName = authData.ApiKeyName ?? AuthConstants.ApiKeyPrefix;
            ApiKeyValue = authData.ApiKeyValue ?? string.Empty;
            ApiKeyLocation = authData.ApiKeyLocation ?? AuthConstants.LocationHeader;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load auth data: {ex.Message}");
            Clear();
        }
    }

    public AuthData ToAuthData()
    {
        return new AuthData
        {
            Type = SelectedAuthType,
            BasicUsername = BasicUsername.Trim(),
            BasicPassword = BasicPassword,
            BearerToken = BearerToken.Trim(),
            ApiKeyName = ApiKeyName.Trim(),
            ApiKeyValue = ApiKeyValue.Trim(),
            ApiKeyLocation = ApiKeyLocation
        };
    }

    // ========================================
    // Validation
    // ========================================
    
    public bool IsValid()
    {
        return SelectedAuthType switch
        {
            var t when t == AuthConstants.None => true,
            var t when t == AuthConstants.Basic => 
                !string.IsNullOrWhiteSpace(BasicUsername) || 
                !string.IsNullOrWhiteSpace(BasicPassword),
            var t when t == AuthConstants.Bearer => 
                !string.IsNullOrWhiteSpace(BearerToken),
            var t when t == AuthConstants.ApiKey => 
                !string.IsNullOrWhiteSpace(ApiKeyName) && 
                !string.IsNullOrWhiteSpace(ApiKeyValue),
            _ => false
        };
    }

    // ========================================
    // Clear/Reset
    // ========================================
    
    public void Clear()
    {
        SelectedAuthType = AuthConstants.None;
        BasicUsername = string.Empty;
        BasicPassword = string.Empty;
        BearerToken = string.Empty;
        ApiKeyName = AuthConstants.ApiKeyPrefix;
        ApiKeyValue = string.Empty;
        ApiKeyLocation = AuthConstants.LocationHeader;
    }
}
