using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AvalonHttp.Models;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

/// <summary>
/// Represents a view model for managing query parameters.
/// </summary>
public partial class QueryParamsViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Reference to URL parser service.
    /// </summary>
    private readonly IUrlParserService _urlParserService;
    
    /// <summary>
    /// Indicates whether the view model is currently updating its state.
    /// </summary>
    private bool _isUpdating;

    /// <summary>
    /// Collection of query parameters.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledParametersCount))]
    private ObservableCollection<KeyValueItemModel> _parameters = new();

    /// <summary>
    /// Cached count of enabled parameters.
    /// </summary>
    private bool _isCountDirty = true;

    /// <summary>
    /// Count of enabled parameters.
    /// </summary>
    public int EnabledParametersCount
    {
        get
        {
            if (_isCountDirty)
            {
                field = Parameters.Count(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key));
                _isCountDirty = false;
            }
            return field;
        }
    }

    /// <summary>
    /// Event raised when the URL changes.
    /// </summary>
    public event EventHandler<string>? UrlChanged;

    public QueryParamsViewModel(IUrlParserService urlParserService)
    {
        _urlParserService = urlParserService ?? throw new ArgumentNullException(nameof(urlParserService));
        Parameters.CollectionChanged += OnParametersCollectionChanged;
    }

    /// <summary>
    /// Load parameters from URL string.
    /// </summary>
    public void LoadFromUrl(string url)
    {
        if (_isUpdating || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            _isUpdating = true;

            var (_, parameters) = _urlParserService.ParseUrl(url);

            Clear();
            
            foreach (var param in parameters)
            {
                Parameters.Add(param);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load parameters from URL: {ex.Message}");
        }
        finally
        {
            _isUpdating = false;
            _isCountDirty = true;
            OnPropertyChanged(nameof(EnabledParametersCount));
        }
    }

    /// <summary>
    /// Build URL with query parameters.
    /// </summary>
    public string BuildUrl(string baseUrl, Func<string, string>? resolver = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        try
        {
            // If resolver provided, resolve variables first
            if (resolver != null)
            {
                var resolvedParams = Parameters.Select(p => new KeyValueItemModel
                {
                    IsEnabled = p.IsEnabled,
                    Key = resolver(p.Key),
                    Value = resolver(p.Value)
                }).ToList();
                
                return _urlParserService.BuildUrl(baseUrl, resolvedParams);
            }
            
            return _urlParserService.BuildUrl(baseUrl, Parameters);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to build URL: {ex.Message}");
            return baseUrl;
        }
    }
    
    /// <summary>
    /// Adds a new parameter to the collection.
    /// </summary>
    [RelayCommand]
    private void AddParameter()
    {
        var newParam = new KeyValueItemModel 
        { 
            IsEnabled = true, 
            Key = string.Empty, 
            Value = string.Empty 
        };
        Parameters.Add(newParam);
    }

    /// <summary>
    /// Removes a specified parameter from the collection.
    /// </summary>
    /// <param name="param">The parameter to remove.</param>
    [RelayCommand]
    private void RemoveParameter(KeyValueItemModel? param)
    {
        if (param != null)
        {
            Parameters.Remove(param);
        }
    }

    /// <summary>
    /// Toggles the enablement state of all parameters.
    /// </summary>
    /// <param name="isEnabled">The new enablement state for all parameters.</param>
    [RelayCommand]
    private void ToggleAll(bool isEnabled)
    {
        foreach (var param in Parameters)
        {
            param.IsEnabled = isEnabled;
        }
    }
    
    /// <summary>
    /// Clear all parameters with proper cleanup.
    /// </summary>
    private void Clear()
    {
        // Unsubscribe from all items
        foreach (var param in Parameters)
        {
            param.PropertyChanged -= OnParameterPropertyChanged;
        }

        Parameters.Clear();
        _isCountDirty = true;
    }
    
    /// <summary>
    /// Event handler for collection change notifications.
    /// </summary>
    ///<param name="sender">The sender of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnParametersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Invalidate cache
        _isCountDirty = true;

        // Unsubscribe from old items
        if (e.OldItems != null)
        {
            foreach (KeyValueItemModel param in e.OldItems)
            {
                param.PropertyChanged -= OnParameterPropertyChanged;
            }
        }

        // Subscribe to new items
        if (e.NewItems != null)
        {
            foreach (KeyValueItemModel param in e.NewItems)
            {
                param.PropertyChanged += OnParameterPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(EnabledParametersCount));
        NotifyUrlChanged();
    }

    /// <summary>
    /// Handles the PropertyChanged event for individual KeyValueItemModel instances
    /// within the collection of parameters. Updates internal state and notifies
    /// listeners of changes to enabled parameters or the generated URL.
    /// </summary>
    /// <param name="sender">The source of the PropertyChanged event. Typically a KeyValueItemModel instance.</param>
    /// <param name="e">An object containing the event data, including the name of the changed property.</param>
    private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyValueItemModel.Key) ||
            e.PropertyName == nameof(KeyValueItemModel.Value) ||
            e.PropertyName == nameof(KeyValueItemModel.IsEnabled))
        {
            _isCountDirty = true;
            OnPropertyChanged(nameof(EnabledParametersCount));
            NotifyUrlChanged();
        }
    }

    /// <summary>
    /// Notifies listeners that the URL has changed.
    /// </summary>
    private void NotifyUrlChanged()
    {
        if (_isUpdating)
        {
            return;
        }

        UrlChanged?.Invoke(this, string.Empty);
    }

    /// <summary>
    /// Releases all resources used by the QueryParamsViewModel instance.
    /// </summary>
    public void Dispose()
    {
        Parameters.CollectionChanged -= OnParametersCollectionChanged;
        Clear();
        
        // Clear event handlers safely
        UrlChanged = null;
    }
}
