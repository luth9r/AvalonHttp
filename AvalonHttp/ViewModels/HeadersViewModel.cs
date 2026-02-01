using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AvalonHttp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

public partial class HeadersViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledHeadersCount))]
    private ObservableCollection<KeyValueItemModel> _headers = new();

    public int EnabledHeadersCount => 
        Headers.Count(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key));

    public HeadersViewModel()
    {
        Headers.CollectionChanged += OnHeadersCollectionChanged;
    }

    private void OnHeadersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Handle Reset action (e.g., Clear())
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Don't need to unsubscribe here - already done in Clear()
            OnPropertyChanged(nameof(EnabledHeadersCount));
            return;
        }

        // Unsubscribe from old items
        if (e.OldItems != null)
        {
            foreach (KeyValueItemModel item in e.OldItems)
            {
                item.PropertyChanged -= OnHeaderItemPropertyChanged;
            }
        }

        // Subscribe to new items
        if (e.NewItems != null)
        {
            foreach (KeyValueItemModel item in e.NewItems)
            {
                item.PropertyChanged += OnHeaderItemPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(EnabledHeadersCount));
    }

    private void OnHeaderItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyValueItemModel.IsEnabled) ||
            e.PropertyName == nameof(KeyValueItemModel.Key))
        {
            OnPropertyChanged(nameof(EnabledHeadersCount));
        }
    }

    [RelayCommand]
    private void AddHeader()
    {
        var header = new KeyValueItemModel { IsEnabled = true };
        Headers.Add(header);
    }

    [RelayCommand]
    private void RemoveHeader(KeyValueItemModel? header)
    {
        if (header != null)
        {
            Headers.Remove(header);
        }
    }

    [RelayCommand]
    private void ToggleAll(bool isEnabled)
    {
        foreach (var header in Headers)
        {
            header.IsEnabled = isEnabled;
        }
    }

    /// <summary>
    /// Get enabled headers as list of key-value pairs.
    /// HTTP-compliant: Allows duplicate header names.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetEnabledHeaders()
    {
        foreach (var header in Headers.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key)))
        {
            var key = header.Key.Trim();
            var value = (header.Value ?? "").Trim();
            
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    /// <summary>
    /// Load headers from collection.
    /// </summary>
    public void LoadHeaders(IEnumerable<KeyValueItemModel>? headers)
    {
        Clear();

        if (headers == null)
            return;

        try
        {
            foreach (var header in headers)
            {
                // Create new instance to break reference with source
                Headers.Add(new KeyValueItemModel
                {
                    IsEnabled = header.IsEnabled,
                    Key = header.Key,
                    Value = header.Value
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load headers: {ex.Message}");
        }
    }

    /// <summary>
    /// Load headers from key-value pairs (e.g., from HttpResponseHeaders).
    /// Supports duplicate keys.
    /// </summary>
    public void LoadFromPairs(IEnumerable<KeyValuePair<string, string>>? headerPairs)
    {
        Clear();

        if (headerPairs == null)
            return;

        try
        {
            foreach (var kvp in headerPairs)
            {
                Headers.Add(new KeyValueItemModel
                {
                    IsEnabled = true,
                    Key = kvp.Key,
                    Value = kvp.Value
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load headers from pairs: {ex.Message}");
        }
    }

    /// <summary>
    /// Export headers to collection.
    /// </summary>
    public ObservableCollection<KeyValueItemModel> ToCollection()
    {
        return new ObservableCollection<KeyValueItemModel>(
            Headers.Select(h => new KeyValueItemModel
            {
                IsEnabled = h.IsEnabled,
                Key = h.Key,
                Value = h.Value
            })
        );
    }

    /// <summary>
    /// Clear all headers with proper cleanup.
    /// </summary>
    public void Clear()
    {
        // ✅ Critical: Unsubscribe before Clear() because CollectionChanged 
        // fires with Action=Reset and e.OldItems=null
        foreach (var header in Headers)
        {
            header.PropertyChanged -= OnHeaderItemPropertyChanged;
        }

        Headers.Clear();
    }

    /// <summary>
    /// Check if there are duplicate keys (for UI warning).
    /// Note: Duplicates are valid in HTTP, but user might want to know.
    /// </summary>
    public bool HasDuplicateKeys()
    {
        var enabledKeys = Headers
            .Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key))
            .Select(h => h.Key.Trim())
            .ToList();

        return enabledKeys.Count != enabledKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    /// <summary>
    /// Get list of duplicate keys (for UI display).
    /// </summary>
    public List<string> GetDuplicateKeys()
    {
        return Headers
            .Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key))
            .Select(h => h.Key.Trim())
            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
    }
}
