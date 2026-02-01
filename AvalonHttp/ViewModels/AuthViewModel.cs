using System;
using System.Collections.Generic;
using AvalonHttp.Common.Constants;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBasicAuthSelected))]
    [NotifyPropertyChangedFor(nameof(IsBearerTokenSelected))]
    [NotifyPropertyChangedFor(nameof(IsApiKeySelected))]
    private string _selectedAuthType = AuthConstants.None;
    
    [ObservableProperty]
    private string _basicUsername = "";
    
    [ObservableProperty]
    private string _basicPassword = "";
    
    [ObservableProperty]
    private string _bearerToken = "";
    
    [ObservableProperty]
    private string _apiKeyName = AuthConstants.ApiKeyPrefix;
    
    [ObservableProperty]
    private string _apiKeyValue = "";
    
    [ObservableProperty]
    private string _apiKeyLocation = AuthConstants.LocationHeader;

    // Computed properties for UI binding
    public bool IsBasicAuthSelected => SelectedAuthType == AuthConstants.Basic;
    public bool IsBearerTokenSelected => SelectedAuthType == AuthConstants.Bearer;
    public bool IsApiKeySelected => SelectedAuthType == AuthConstants.ApiKey;
    
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

    public Dictionary<string, string> GetAuthHeaders()
    {
        var headers = new Dictionary<string, string>();

        try
        {
            switch (SelectedAuthType)
            {
                case var _ when SelectedAuthType == AuthConstants.Basic:
                    AddBasicAuthHeader(headers);
                    break;

                case var _ when SelectedAuthType == AuthConstants.Bearer:
                    AddBearerTokenHeader(headers);
                    break;

                case var _ when SelectedAuthType == AuthConstants.ApiKey:
                    if (ApiKeyLocation == AuthConstants.LocationHeader)
                    {
                        AddApiKeyHeader(headers);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to generate auth headers: {ex.Message}");
        }

        return headers;
    }

    private void AddBasicAuthHeader(Dictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(BasicUsername) && string.IsNullOrWhiteSpace(BasicPassword))
            return;

        var credentials = $"{BasicUsername}:{BasicPassword}";
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));
        headers["Authorization"] = $"Basic {base64}";
    }

    private void AddBearerTokenHeader(Dictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(BearerToken))
            return;

        headers["Authorization"] = $"Bearer {BearerToken.Trim()}";
    }

    private void AddApiKeyHeader(Dictionary<string, string> headers)
    {
        if (string.IsNullOrWhiteSpace(ApiKeyName) || string.IsNullOrWhiteSpace(ApiKeyValue))
            return;

        headers[ApiKeyName.Trim()] = ApiKeyValue.Trim();
    }

    public Dictionary<string, string> GetAuthQueryParams()
    {
        var queryParams = new Dictionary<string, string>();

        if (SelectedAuthType == AuthConstants.ApiKey
            && ApiKeyLocation == AuthConstants.LocationQuery 
            && !string.IsNullOrWhiteSpace(ApiKeyName) 
            && !string.IsNullOrWhiteSpace(ApiKeyValue))
        {
            queryParams[ApiKeyName.Trim()] = ApiKeyValue.Trim();
        }

        return queryParams;
    }

    /// <summary>
    /// Load authentication data from AuthData model.
    /// </summary>
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
            BasicUsername = authData.BasicUsername ?? "";
            BasicPassword = authData.BasicPassword ?? "";
            BearerToken = authData.BearerToken ?? "";
            ApiKeyName = authData.ApiKeyName ?? AuthConstants.ApiKeyPrefix;
            ApiKeyValue = authData.ApiKeyValue ?? "";
            ApiKeyLocation = authData.ApiKeyLocation ?? AuthConstants.LocationHeader;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load auth data: {ex.Message}");
            Clear();
        }
    }

    /// <summary>
    /// Convert current state to AuthData model.
    /// </summary>
    public AuthData ToAuthData()
    {
        return new AuthData
        {
            Type = SelectedAuthType,
            BasicUsername = BasicUsername?.Trim() ?? string.Empty,
            BasicPassword = BasicPassword,
            BearerToken = BearerToken?.Trim() ?? string.Empty,
            ApiKeyName = ApiKeyName?.Trim() ?? "",
            ApiKeyValue = ApiKeyValue?.Trim() ?? "",
            ApiKeyLocation = ApiKeyLocation
        };
    }

    /// <summary>
    /// Clear all authentication data.
    /// </summary>
    public void Clear()
    {
        SelectedAuthType = AuthConstants.None;
        BasicUsername = "";
        BasicPassword = "";
        BearerToken = "";
        ApiKeyName = AuthConstants.ApiKeyPrefix;
        ApiKeyValue = "";
        ApiKeyLocation = AuthConstants.LocationHeader;
    }

    /// <summary>
    /// Validate current authentication configuration.
    /// </summary>
    public bool IsValid()
    {
        return SelectedAuthType switch
        {
            _ when SelectedAuthType == AuthConstants.None => true,
            _ when SelectedAuthType == AuthConstants.Basic => 
                !string.IsNullOrWhiteSpace(BasicUsername) || 
                !string.IsNullOrWhiteSpace(BasicPassword),
            _ when SelectedAuthType == AuthConstants.Bearer => 
                !string.IsNullOrWhiteSpace(BearerToken),
            _ when SelectedAuthType == AuthConstants.ApiKey => 
                !string.IsNullOrWhiteSpace(ApiKeyName) && 
                !string.IsNullOrWhiteSpace(ApiKeyValue),
            _ => false
        };
    }

    /// <summary>
    /// Check if any authentication is configured.
    /// </summary>
    public bool HasAuthentication()
    {
        return SelectedAuthType != AuthConstants.None && IsValid();
    }
    
    public void Reset()
    {
        SelectedAuthType = "None";
        BasicUsername = "";
        BasicPassword = "";
        BearerToken = "";
        ApiKeyName = "";
        ApiKeyValue = "";
        ApiKeyLocation = "Header";
    }

}
