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

public partial class HeadersViewModel : ViewModelBase, IDisposable
{
    // ========================================
    // Observable Properties
    // ========================================
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledHeadersCount))]
    private ObservableCollection<KeyValueItemModel> _headers = new();

    // ========================================
    // Computed Properties (with caching)
    // ========================================
    
    private int _cachedEnabledCount;
    private bool _isCountDirty = true;

    public int EnabledHeadersCount
    {
        get
        {
            if (_isCountDirty)
            {
                _cachedEnabledCount = Headers.Count(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key));
                _isCountDirty = false;
            }
            return _cachedEnabledCount;
        }
    }

    // ========================================
    // Constructor
    // ========================================
    
    public HeadersViewModel()
    {
        Headers.CollectionChanged += OnHeadersCollectionChanged;
    }

    // ========================================
    // Event Handlers
    // ========================================
    
    private void OnHeadersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Invalidate cache
        _isCountDirty = true;

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
            _isCountDirty = true;
            OnPropertyChanged(nameof(EnabledHeadersCount));
        }
    }

    // ========================================
    // Commands
    // ========================================
    
    [RelayCommand]
    private void AddHeader()
    {
        var header = new KeyValueItemModel 
        { 
            IsEnabled = true,
            Key = string.Empty,
            Value = string.Empty
        };
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

    // ========================================
    // Get Header Data
    // ========================================
    
    /// <summary>
    /// Get enabled headers as list of key-value pairs.
    /// HTTP-compliant: Allows duplicate header names.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetEnabledHeaders(Func<string, string>? resolver = null)
    {
        foreach (var header in Headers)
        {
            if (!header.IsEnabled || string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            // Resolve variables if resolver provided
            var key = resolver?.Invoke(header.Key.Trim()) ?? header.Key.Trim();
            var value = resolver?.Invoke(header.Value ?? string.Empty) ?? header.Value ?? string.Empty;
            
            yield return new KeyValuePair<string, string>(key, value.Trim());
        }
    }

    // ========================================
    // Load Header Data
    // ========================================
    
    /// <summary>
    /// Load headers from collection.
    /// </summary>
    public void LoadHeaders(IEnumerable<KeyValueItemModel>? headers)
    {
        Clear();

        if (headers == null)
        {
            return;
        }

        try
        {
            foreach (var header in headers)
            {
                // Create new instance to break reference with source
                Headers.Add(new KeyValueItemModel
                {
                    IsEnabled = header.IsEnabled,
                    Key = header.Key ?? string.Empty,
                    Value = header.Value ?? string.Empty
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
        {
            return;
        }

        try
        {
            foreach (var kvp in headerPairs)
            {
                Headers.Add(new KeyValueItemModel
                {
                    IsEnabled = true,
                    Key = kvp.Key ?? string.Empty,
                    Value = kvp.Value ?? string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load headers from pairs: {ex.Message}");
        }
    }

    // ========================================
    // Export Header Data
    // ========================================
    
    /// <summary>
    /// Get headers collection (returns the actual collection, not a copy).
    /// </summary>
    public ObservableCollection<KeyValueItemModel> GetHeaders() => Headers;

    /// <summary>
    /// Export headers as new collection (creates copies).
    /// </summary>
    public List<KeyValueItemModel> ExportHeaders()
    {
        return Headers.Select(h => new KeyValueItemModel
        {
            IsEnabled = h.IsEnabled,
            Key = h.Key,
            Value = h.Value
        }).ToList();
    }

    // ========================================
    // Duplicate Detection
    // ========================================
    
    /// <summary>
    /// Check if there are duplicate keys (for UI warning).
    /// Note: Duplicates are valid in HTTP, but user might want to know.
    /// </summary>
    public bool HasDuplicateKeys()
    {
        if (EnabledHeadersCount < 2)
        {
            return false;
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in Headers)
        {
            if (!header.IsEnabled || string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            var key = header.Key.Trim();
            if (!seenKeys.Add(key))
            {
                return true; // Found duplicate
            }
        }

        return false;
    }

    /// <summary>
    /// Get list of duplicate keys (for UI display).
    /// </summary>
    public List<string> GetDuplicateKeys()
    {
        if (EnabledHeadersCount < 2)
        {
            return new List<string>();
        }

        var keyGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in Headers)
        {
            if (!header.IsEnabled || string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            var key = header.Key.Trim();
            keyGroups[key] = keyGroups.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return keyGroups
            .Where(kvp => kvp.Value > 1)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    // ========================================
    // Clear
    // ========================================
    
    /// <summary>
    /// Clear all headers with proper cleanup.
    /// </summary>
    public void Clear()
    {
        // Unsubscribe from all items before clearing
        foreach (var header in Headers)
        {
            header.PropertyChanged -= OnHeaderItemPropertyChanged;
        }

        Headers.Clear();
        _isCountDirty = true;
    }

    // ========================================
    // Cleanup
    // ========================================
    
    public void Dispose()
    {
        Headers.CollectionChanged -= OnHeadersCollectionChanged;
        Clear();
        GC.SuppressFinalize(this);
    }
}
