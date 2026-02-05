using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using AvalonHttp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

/// <summary>
/// Represents a view model for managing cookies.
/// </summary>
public partial class CookiesViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Collection of cookie items managed within the view model.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledCookiesCount))]
    private ObservableCollection<KeyValueItemModel> _cookies = new();

    /// <summary>
    /// Indicates whether the cached count of enabled cookies is outdated and needs to be recalculated.
    /// </summary>
    private bool _isCountDirty = true;

    /// <summary>
    /// Count of enabled cookies.
    /// </summary>
    public int EnabledCookiesCount
    {
        get
        {
            if (_isCountDirty)
            {
                field = Cookies.Count(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.Key));
                _isCountDirty = false;
            }
            return field;
        }
    }
    
    public CookiesViewModel()
    {
        Cookies.CollectionChanged += OnCookiesCollectionChanged;
    }

    /// <summary>
    /// Handles changes in the cookies collection by updating the enablement state and performing
    /// appropriate subscription or unsubscription from property change events of individual cookie items.
    /// </summary>
    /// <param name="sender">The source of the event, typically the cookies collection.</param>
    /// <param name="e">The event data containing information about the changes in the collection.</param>
    private void OnCookiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Invalidate cache
        _isCountDirty = true;

        // Unsubscribe from old items
        if (e.OldItems != null)
        {
            foreach (KeyValueItemModel item in e.OldItems)
            {
                item.PropertyChanged -= OnCookieItemPropertyChanged;
            }
        }

        // Subscribe to new items
        if (e.NewItems != null)
        {
            foreach (KeyValueItemModel item in e.NewItems)
            {
                item.PropertyChanged += OnCookieItemPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(EnabledCookiesCount));
    }

    /// <summary>
    /// Handles changes in the enablement state or key of individual cookie items.
    /// </summary>
    /// <param name="sender">The source of the event, typically an individual cookie item.</param>
    /// <param name="e">The event data containing information about the property change.</param>
    private void OnCookieItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyValueItemModel.IsEnabled) ||
            e.PropertyName == nameof(KeyValueItemModel.Key))
        {
            _isCountDirty = true;
            OnPropertyChanged(nameof(EnabledCookiesCount));
        }
    }

    /// <summary>
    /// Add new cookie.
    /// </summary>
    [RelayCommand]
    private void AddCookie()
    {
        var cookie = new KeyValueItemModel 
        { 
            IsEnabled = true,
            Key = string.Empty,
            Value = string.Empty
        };
        Cookies.Add(cookie);
    }

    /// <summary>
    /// Removes a specified cookie from the cookies collection.
    /// </summary>
    /// <param name="cookie">The cookie to be removed. If null, the method does nothing.</param>
    [RelayCommand]
    private void RemoveCookie(KeyValueItemModel? cookie)
    {
        if (cookie != null)
        {
            Cookies.Remove(cookie);
        }
    }

    /// <summary>
    /// Toggles the enablement state of all cookies.
    /// </summary>
    /// <param name="isEnabled"></param>
    [RelayCommand]
    private void ToggleAll(bool isEnabled)
    {
        foreach (var cookie in Cookies)
        {
            cookie.IsEnabled = isEnabled;
        }
    }
    
    /// <summary>
    /// Get enabled cookies as Cookie header value.
    /// Format: "name1=value1; name2=value2"
    /// </summary>
    public string GetCookieHeaderValue(Func<string, string>? resolver = null)
    {
        if (EnabledCookiesCount == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        bool first = true;

        foreach (var cookie in Cookies)
        {
            if (!cookie.IsEnabled || string.IsNullOrWhiteSpace(cookie.Key))
            {
                continue;
            }

            if (!first)
            {
                sb.Append("; ");
            }

            first = false;

            // Resolve variables in both cookie name and value
            var key = resolver?.Invoke(cookie.Key) ?? cookie.Key;
            var value = resolver?.Invoke(cookie.Value ?? string.Empty) ?? cookie.Value ?? string.Empty;
            
            sb.Append(key).Append('=').Append(value);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Get enabled cookies as key-value pairs.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetEnabledCookies()
    {
        foreach (var cookie in Cookies)
        {
            if (!cookie.IsEnabled || string.IsNullOrWhiteSpace(cookie.Key))
            {
                continue;
            }

            yield return new KeyValuePair<string, string>(
                cookie.Key.Trim(),
                (cookie.Value ?? string.Empty).Trim()
            );
        }
    }
    
    /// <summary>
    /// Load cookies from collection.
    /// </summary>
    public void LoadCookies(IEnumerable<KeyValueItemModel>? cookies)
    {
        Clear();

        if (cookies == null)
        {
            return;
        }

        try
        {
            foreach (var cookie in cookies)
            {
                Cookies.Add(new KeyValueItemModel
                {
                    IsEnabled = cookie.IsEnabled,
                    Key = cookie.Key ?? string.Empty,
                    Value = cookie.Value ?? string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cookies: {ex.Message}");
        }
    }

    /// <summary>
    /// Load cookies from Cookie header value.
    /// Format: "name1=value1; name2=value2"
    /// </summary>
    public void LoadFromCookieHeader(string? cookieHeader)
    {
        Clear();

        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return;
        }

        try
        {
            var cookiePairs = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            foreach (var pair in cookiePairs)
            {
                var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
                
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    Cookies.Add(new KeyValueItemModel
                    {
                        IsEnabled = true,
                        Key = parts[0],
                        Value = parts.Length > 1 ? parts[1] : string.Empty
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse cookie header: {ex.Message}");
        }
    }

    /// <summary>
    /// Load cookies from CookieContainer (from response).
    /// Updates existing cookies or adds new ones.
    /// </summary>
    public void LoadFromCookieContainer(CookieContainer? cookieContainer, Uri? requestUri)
    {
        if (cookieContainer == null || requestUri == null)
        {
            return;
        }

        try
        {
            var cookies = cookieContainer.GetCookies(requestUri);
            
            foreach (Cookie cookie in cookies)
            {
                // Check if already exists
                var existing = Cookies.FirstOrDefault(c => 
                    c.Key.Equals(cookie.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Update existing value
                    existing.Value = cookie.Value;
                    existing.IsEnabled = true;
                }
                else
                {
                    // Add new cookie
                    Cookies.Add(new KeyValueItemModel
                    {
                        IsEnabled = true,
                        Key = cookie.Name,
                        Value = cookie.Value
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load cookies from container: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Clear all cookies with proper cleanup.
    /// </summary>
    private void Clear()
    {
        // Unsubscribe from all items
        foreach (var cookie in Cookies)
        {
            cookie.PropertyChanged -= OnCookieItemPropertyChanged;
        }

        Cookies.Clear();
        _isCountDirty = true;
    }

    /// <summary>
    /// Releases all resources used by the CookiesViewModel instance.
    /// This method unsubscribes from collection change notifications, clears all managed resources,
    /// and suppresses finalization to optimize garbage collection.
    /// </summary>
    public void Dispose()
    {
        Cookies.CollectionChanged -= OnCookiesCollectionChanged;
        Clear();
        GC.SuppressFinalize(this);
    }
}
