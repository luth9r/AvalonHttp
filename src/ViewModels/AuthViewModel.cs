using System;
using System.Collections.Generic;
using System.Text;
using AvalonHttp.Common.Constants;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.ViewModels;

/// <summary>
/// Represents the authentication view model used to manage authentication settings and data for requests.
/// </summary>
public partial class AuthViewModel : ViewModelBase
{
    /// <summary>
    /// Represents the currently selected authentication type.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBasicAuthSelected))]
    [NotifyPropertyChangedFor(nameof(IsBearerTokenSelected))]
    [NotifyPropertyChangedFor(nameof(IsApiKeySelected))]
    [NotifyPropertyChangedFor(nameof(HasAuthentication))]
    private string _selectedAuthType = AuthConstants.None;
    
    /// <summary>
    /// Represents the username used for basic authentication.
    /// </summary>
    [ObservableProperty]
    private string _basicUsername = string.Empty;
    
    /// <summary>
    /// Represents the password used for basic authentication.
    /// </summary>
    [ObservableProperty]
    private string _basicPassword = string.Empty;
    
    /// <summary>
    /// Represents the bearer token used for authentication.
    /// </summary>
    [ObservableProperty]
    private string _bearerToken = string.Empty;
    
    /// <summary>
    /// Represents the name of the API key used for authentication.
    /// </summary>
    [ObservableProperty]
    private string _apiKeyName = AuthConstants.ApiKeyPrefix;
    
    /// <summary>
    /// Represents the value of the API key used for authentication.
    /// </summary>
    [ObservableProperty]
    private string _apiKeyValue = string.Empty;
    
    /// <summary>
    /// Represents the location of the API key used for authentication.
    /// </summary>
    [ObservableProperty]
    private string _apiKeyLocation = AuthConstants.LocationHeader;
    
    public bool IsBasicAuthSelected => SelectedAuthType == AuthConstants.Basic;
    public bool IsBearerTokenSelected => SelectedAuthType == AuthConstants.Bearer;
    public bool IsApiKeySelected => SelectedAuthType == AuthConstants.ApiKey;
    public bool HasAuthentication => SelectedAuthType != AuthConstants.None && IsValid();
    
    /// <summary>
    /// Represents a list of supported authentication types.
    /// </summary>
    public List<string> AuthTypes { get; } =
    [
        AuthConstants.None,
        AuthConstants.Basic,
        AuthConstants.Bearer,
        AuthConstants.ApiKey,
    ];
    
    /// <summary>
    /// Represents a list of supported API key locations.
    /// </summary>
    public List<string> ApiKeyLocations { get; } =
    [
        AuthConstants.LocationHeader,
        AuthConstants.LocationQuery,
    ];

    /// <summary>
    /// Generates authentication headers based on the selected authentication type and additional settings.
    /// </summary>
    /// <param name="resolver">
    /// An optional function used to resolve variables in authentication data.
    /// If not provided, no variable resolution will be performed.
    /// </param>
    /// <returns>
    /// A dictionary containing authentication headers, where the key is the header name
    /// and the value is the corresponding header value.
    /// </returns>
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

    /// <summary>
    /// Adds a basic authentication header to the provided dictionary of headers
    /// using the configured username and password, resolved through the optional resolver.
    /// </summary>
    /// <param name="headers">
    /// A dictionary where the key is the header name, and the value is the corresponding header value.
    /// The basic authentication header will be added to this dictionary.
    /// </param>
    /// <param name="resolver">
    /// An optional function used to resolve variables in the username and password.
    /// If not provided, the values will be used as configured without variable resolution.
    /// </param>
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

    /// <summary>
    /// Adds a bearer token authentication header to the provided dictionary of headers
    /// </summary>
    /// <param name="headers"> A dictionary where the key is the header name, and the value is the corresponding header value. </param>
    /// <param name="resolver"> An optional function used to resolve variables in the bearer token. </param>
    private void AddBearerTokenHeader(Dictionary<string, string> headers, Func<string, string>? resolver)
    {
        var token = Resolve(BearerToken, resolver);
        
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        headers["Authorization"] = $"Bearer {token}";
    }

    /// <summary>
    /// Adds an API key authentication header to the provided dictionary of headers.
    /// </summary>
    /// <param name="headers"> A dictionary where the key is the header name, and the value is the corresponding header value. </param>
    /// <param name="resolver"> An optional function used to resolve variables in the API key. </param>
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

    /// <summary>
    /// Generates query parameters based on the selected authentication type and additional settings.
    /// </summary>
    /// <param name="resolver"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Resolves a given value using the provided resolver function, if available. If no resolver is provided, the original value is returned.
    /// </summary>
    /// <param name="value">
    /// The string value to be resolved.
    /// </param>
    /// <param name="resolver">
    /// An optional function for resolving the value. If null, the input value will be returned as-is.
    /// </param>
    /// <returns>
    /// The resolved string value if a resolver is provided, otherwise the original input value.
    /// </returns>
    private static string Resolve(string value, Func<string, string>? resolver)
    {
        return resolver?.Invoke(value) ?? value;
    }

    /// <summary>
    /// Loads authentication data from the provided AuthData object.
    /// </summary>
    /// <param name="authData">The authentication data to load into the view model.</param>
    public void LoadFromAuthData(AuthData? authData)
    {
        if (authData == null)
        {
            Clear();
            return;
        }

        try
        {
            SelectedAuthType = authData.Type;
            BasicUsername = authData.BasicUsername;
            BasicPassword = authData.BasicPassword;
            BearerToken = authData.BearerToken;
            ApiKeyName = authData.ApiKeyName;
            ApiKeyValue = authData.ApiKeyValue;
            ApiKeyLocation = authData.ApiKeyLocation;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load auth data: {ex.Message}");
            Clear();
        }
    }

    /// <summary>
    /// Converts the current authentication-related properties of the view model into an AuthData object.
    /// </summary>
    /// <returns>
    /// An AuthData object containing authentication information, such as the type, credentials, token, API key details, and its location.
    /// </returns>
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

    /// <summary>
    /// Checks whether the current authentication settings are valid.
    /// </summary>
    /// <returns>True if the authentication settings are valid, otherwise false.</returns>
    private bool IsValid()
    {
        return SelectedAuthType switch
        {
            AuthConstants.None => true,
            AuthConstants.Basic => 
                !string.IsNullOrWhiteSpace(BasicUsername) || 
                !string.IsNullOrWhiteSpace(BasicPassword),
            AuthConstants.Bearer => 
                !string.IsNullOrWhiteSpace(BearerToken),
            AuthConstants.ApiKey => 
                !string.IsNullOrWhiteSpace(ApiKeyName) && 
                !string.IsNullOrWhiteSpace(ApiKeyValue),
            _ => false
        };
    }
    
    /// <summary>
    /// Clears all authentication-related properties of the view model.
    /// </summary>
    private void Clear()
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
