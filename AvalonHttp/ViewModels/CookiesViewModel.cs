using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using AvalonHttp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

public partial class CookiesViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledCookiesCount))]
    private ObservableCollection<KeyValueItemModel> _cookies = new();

    public int EnabledCookiesCount => 
        Cookies.Count(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.Key));

    public CookiesViewModel()
    {
        Cookies.CollectionChanged += OnCookiesCollectionChanged;
    }

    private void OnCookiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            OnPropertyChanged(nameof(EnabledCookiesCount));
            return;
        }

        if (e.OldItems != null)
        {
            foreach (KeyValueItemModel item in e.OldItems)
            {
                item.PropertyChanged -= OnCookieItemPropertyChanged;
            }
        }

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
            OnPropertyChanged(nameof(EnabledCookiesCount));
        }
    }

    [RelayCommand]
    private void AddCookie()
    {
        var cookie = new KeyValueItemModel { IsEnabled = true };
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

    /// <summary>
    /// Get enabled cookies as Cookie header value.
    /// Format: "name1=value1; name2=value2"
    /// </summary>
    public string GetCookieHeaderValue()
    {
        var enabledCookies = Cookies
            .Where(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.Key))
            .Select(c => $"{c.Key.Trim()}={c.Value?.Trim() ?? ""}");

        return string.Join("; ", enabledCookies);
    }

    /// <summary>
    /// Get enabled cookies as key-value pairs.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetEnabledCookies()
    {
        foreach (var cookie in Cookies.Where(c => c.IsEnabled && !string.IsNullOrWhiteSpace(c.Key)))
        {
            var key = cookie.Key.Trim();
            var value = (cookie.Value ?? "").Trim();
            
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    /// <summary>
    /// Load cookies from collection.
    /// </summary>
    public void LoadCookies(IEnumerable<KeyValueItemModel>? cookies)
    {
        Clear();

        if (cookies == null)
            return;

        try
        {
            foreach (var cookie in cookies)
            {
                Cookies.Add(new KeyValueItemModel
                {
                    IsEnabled = cookie.IsEnabled,
                    Key = cookie.Key,
                    Value = cookie.Value
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
            return;

        try
        {
            var cookiePairs = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var pair in cookiePairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length >= 1)
                {
                    Cookies.Add(new KeyValueItemModel
                    {
                        IsEnabled = true,
                        Key = parts[0].Trim(),
                        Value = parts.Length > 1 ? parts[1].Trim() : ""
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
    /// </summary>
    public void LoadFromCookieContainer(CookieContainer? cookieContainer, Uri? requestUri)
    {
        if (cookieContainer == null || requestUri == null)
            return;

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
                    // Update value
                    existing.Value = cookie.Value;
                }
                else
                {
                    // Add new
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
    /// Export cookies to collection.
    /// </summary>
    public ObservableCollection<KeyValueItemModel> ToCollection()
    {
        return new ObservableCollection<KeyValueItemModel>(
            Cookies.Select(c => new KeyValueItemModel
            {
                IsEnabled = c.IsEnabled,
                Key = c.Key,
                Value = c.Value
            })
        );
    }

    /// <summary>
    /// Clear all cookies with proper cleanup.
    /// </summary>
    public void Clear()
    {
        foreach (var cookie in Cookies)
        {
            cookie.PropertyChanged -= OnCookieItemPropertyChanged;
        }

        Cookies.Clear();
    }

    public void Dispose()
    {
        try
        {
            Cookies.CollectionChanged -= OnCookiesCollectionChanged;

            foreach (var cookie in Cookies)
            {
                cookie.PropertyChanged -= OnCookieItemPropertyChanged;
            }

            Cookies.Clear();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during CookiesViewModel disposal: {ex.Message}");
        }
    }
}
