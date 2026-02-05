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

/// <summary>
/// Represents a view model for managing headers.
/// </summary>
public partial class HeadersViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Collection of header items managed within the view model.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledHeadersCount))]
    private ObservableCollection<KeyValueItemModel> _headers = new();
    
    /// <summary>
    /// Cached count of enabled headers.
    /// </summary>
    private bool _isCountDirty = true;

    /// <summary>
    /// Count of enabled headers.
    /// </summary>
    public int EnabledHeadersCount
    {
        get
        {
            if (_isCountDirty)
            {
                field = Headers.Count(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key));
                _isCountDirty = false;
            }
            return field;
        }
    }
    
    public HeadersViewModel()
    {
        Headers.CollectionChanged += OnHeadersCollectionChanged;
    }

    /// <summary>
    /// Handles changes in the headers collection, updating cached values and maintaining subscriptions
    /// to property change notifications for added or removed items.
    /// </summary>
    /// <param name="sender">The source of the event, typically the headers collection.</param>
    /// <param name="e">Event data containing information about the changes in the collection.</param>
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

    /// <summary>
    /// Handles changes in the enablement state or key of individual header items.
    /// </summary>
    /// <param name="sender">The source of the event, typically the header item.</param>
    /// <param name="e">Event data containing information about the property change.</param>
    private void OnHeaderItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyValueItemModel.IsEnabled) ||
            e.PropertyName == nameof(KeyValueItemModel.Key))
        {
            _isCountDirty = true;
            OnPropertyChanged(nameof(EnabledHeadersCount));
        }
    }

    /// <summary>
    /// Adds a new header item to the collection.
    /// </summary>
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

    /// <summary>
    /// Removes a specified header item from the collection.
    /// </summary>
    /// <param name="header">The header item to be removed.</param>
    [RelayCommand]
    private void RemoveHeader(KeyValueItemModel? header)
    {
        if (header != null)
        {
            Headers.Remove(header);
        }
    }

    /// <summary>
    /// Toggles the enablement state of all headers.
    /// </summary>
    /// <param name="isEnabled">The new enablement state to set for all headers.</param>
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
            var value = resolver?.Invoke(header.Value) ?? header.Value ?? string.Empty;

            yield return new KeyValuePair<string, string>(key, value.Trim());
        }
    }
    
    /// <summary>
    /// Clear all headers with proper cleanup.
    /// </summary>
    private void Clear()
    {
        // Unsubscribe from all items before clearing
        foreach (var header in Headers)
        {
            header.PropertyChanged -= OnHeaderItemPropertyChanged;
        }

        Headers.Clear();
        _isCountDirty = true;
    }

    /// <summary>
    /// Releases all resources used by the current instance of the <see cref="HeadersViewModel"/> class.
    /// This includes unsubscribing from collection change notifications and clearing internal state.
    /// </summary>
    public void Dispose()
    {
        Headers.CollectionChanged -= OnHeadersCollectionChanged;
        Clear();
    }
}
