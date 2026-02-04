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

public partial class CookiesViewModel : ViewModelBase, IDisposable
{
    // ========================================
    // Observable Properties
    // ========================================
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledCookiesCount))]
    private ObservableCollection<KeyValueItemModel> _cookies = new();

    // ========================================
    // Computed Properties
    // ========================================
    
    private int _cachedEnabledCount;
    private bool _isCountDirty = true;

    public int EnabledCookiesCount
    {
        get
        {
            if (_isCountDirty)
            {
                _cachedEnabledCount = Cookies.Count(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.Key));
                _isCountDirty = false;
            }
            return _cachedEnabledCount;
        }
    }

    // ========================================
    // Constructor
    // ========================================
    
    public CookiesViewModel()
    {
        Cookies.CollectionChanged += OnCookiesCollectionChanged;
    }

    // ========================================
    // Event Handlers
    // ========================================
    
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

    private void OnCookieItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyValueItemModel.IsEnabled) ||
            e.PropertyName == nameof(KeyValueItemModel.Key))
        {
            _isCountDirty = true;
            OnPropertyChanged(nameof(EnabledCookiesCount));
        }
    }

    // ========================================
    // Commands
    // ========================================
    
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

    [RelayCommand]
    private void RemoveCookie(KeyValueItemModel? cookie)
    {
        if (cookie != null)
        {
            Cookies.Remove(cookie);
        }
    }

    [RelayCommand]
    private void ToggleAll(bool isEnabled)
    {
        foreach (var cookie in Cookies)
        {
            cookie.IsEnabled = isEnabled;
        }
    }

    // ========================================
    // Get Cookie Data
    // ========================================
    
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

    // ========================================
    // Load Cookie Data
    // ========================================
    
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

    // ========================================
    // Export Cookie Data
    // ========================================
    
    /// <summary>
    /// Get cookies collection (returns the actual collection, not a copy).
    /// </summary>
    public ObservableCollection<KeyValueItemModel> GetCookies() => Cookies;

    /// <summary>
    /// Export cookies as new collection (creates copies).
    /// </summary>
    public List<KeyValueItemModel> ExportCookies()
    {
        return Cookies.Select(c => new KeyValueItemModel
        {
            IsEnabled = c.IsEnabled,
            Key = c.Key,
            Value = c.Value
        }).ToList();
    }

    // ========================================
    // Clear
    // ========================================
    
    /// <summary>
    /// Clear all cookies with proper cleanup.
    /// </summary>
    public void Clear()
    {
        // Unsubscribe from all items
        foreach (var cookie in Cookies)
        {
            cookie.PropertyChanged -= OnCookieItemPropertyChanged;
        }

        Cookies.Clear();
        _isCountDirty = true;
    }

    // ========================================
    // Cleanup
    // ========================================
    
    public void Dispose()
    {
        Cookies.CollectionChanged -= OnCookiesCollectionChanged;
        Clear();
        GC.SuppressFinalize(this);
    }
}
