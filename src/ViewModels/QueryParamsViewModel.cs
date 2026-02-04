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

public partial class QueryParamsViewModel : ViewModelBase, IDisposable
{
    private readonly IUrlParserService _urlParserService;
    private bool _isUpdating;

    // ========================================
    // Observable Properties
    // ========================================
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledParametersCount))]
    private ObservableCollection<KeyValueItemModel> _parameters = new();

    // ========================================
    // Computed Properties (with caching)
    // ========================================
    
    private int _cachedEnabledCount;
    private bool _isCountDirty = true;

    public int EnabledParametersCount
    {
        get
        {
            if (_isCountDirty)
            {
                _cachedEnabledCount = Parameters.Count(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key));
                _isCountDirty = false;
            }
            return _cachedEnabledCount;
        }
    }

    // ========================================
    // Events
    // ========================================
    
    public event EventHandler<string>? UrlChanged;

    // ========================================
    // Constructor
    // ========================================
    
    public QueryParamsViewModel(IUrlParserService urlParserService)
    {
        _urlParserService = urlParserService ?? throw new ArgumentNullException(nameof(urlParserService));
        Parameters.CollectionChanged += OnParametersCollectionChanged;
    }

    // ========================================
    // Load Methods
    // ========================================
    
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
    /// Load parameters from collection.
    /// </summary>
    public void LoadParameters(IEnumerable<KeyValueItemModel>? parameters)
    {
        if (_isUpdating)
        {
            return;
        }

        try
        {
            _isUpdating = true;

            Clear();

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    // Create new instance to break reference
                    Parameters.Add(new KeyValueItemModel
                    {
                        IsEnabled = param.IsEnabled,
                        Key = param.Key ?? string.Empty,
                        Value = param.Value ?? string.Empty
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load parameters: {ex.Message}");
        }
        finally
        {
            _isUpdating = false;
            _isCountDirty = true;
            OnPropertyChanged(nameof(EnabledParametersCount));
        }
    }

    // ========================================
    // Build/Get Methods
    // ========================================
    
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
                    Key = resolver(p.Key ?? string.Empty),
                    Value = resolver(p.Value ?? string.Empty)
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
    /// Get enabled parameters as key-value pairs (HTTP-compliant, supports duplicates).
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> GetEnabledParameters(Func<string, string>? resolver = null)
    {
        foreach (var param in Parameters)
        {
            if (!param.IsEnabled || string.IsNullOrWhiteSpace(param.Key))
            {
                continue;
            }

            // Resolve variables if resolver provided
            var key = resolver?.Invoke(param.Key.Trim()) ?? param.Key.Trim();
            var value = resolver?.Invoke(param.Value ?? string.Empty) ?? param.Value ?? string.Empty;
            
            yield return new KeyValuePair<string, string>(key, value.Trim());
        }
    }

    // ========================================
    // Export Methods
    // ========================================
    
    /// <summary>
    /// Get parameters collection (returns the actual collection, not a copy).
    /// </summary>
    public ObservableCollection<KeyValueItemModel> GetParameters() => Parameters;

    /// <summary>
    /// Export parameters as new list (creates copies).
    /// </summary>
    public List<KeyValueItemModel> ExportParameters()
    {
        return Parameters.Select(p => new KeyValueItemModel
        {
            IsEnabled = p.IsEnabled,
            Key = p.Key,
            Value = p.Value
        }).ToList();
    }

    // ========================================
    // Commands
    // ========================================
    
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

    [RelayCommand]
    private void RemoveParameter(KeyValueItemModel? param)
    {
        if (param != null)
        {
            Parameters.Remove(param);
        }
    }

    [RelayCommand]
    private void ToggleAll(bool isEnabled)
    {
        foreach (var param in Parameters)
        {
            param.IsEnabled = isEnabled;
        }
    }

    // ========================================
    // Clear
    // ========================================
    
    /// <summary>
    /// Clear all parameters with proper cleanup.
    /// </summary>
    public void Clear()
    {
        // Unsubscribe from all items
        foreach (var param in Parameters)
        {
            param.PropertyChanged -= OnParameterPropertyChanged;
        }

        Parameters.Clear();
        _isCountDirty = true;
    }

    // ========================================
    // Event Handlers
    // ========================================
    
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

    private void NotifyUrlChanged()
    {
        if (_isUpdating)
        {
            return;
        }

        UrlChanged?.Invoke(this, string.Empty);
    }

    // ========================================
    // Dispose
    // ========================================
    
    public void Dispose()
    {
        Parameters.CollectionChanged -= OnParametersCollectionChanged;
        Clear();
        
        // Clear event handlers safely
        UrlChanged = null;
        
        GC.SuppressFinalize(this);
    }
}
