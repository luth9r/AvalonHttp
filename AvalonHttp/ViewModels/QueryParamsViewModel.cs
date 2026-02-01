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
    private bool _isUpdating = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledParametersCount))]
    private ObservableCollection<KeyValueItemModel> _parameters;

    public int EnabledParametersCount => 
        Parameters.Count(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key));

    public event EventHandler<string>? UrlChanged;

    public QueryParamsViewModel(IUrlParserService urlParserService)
    {
        _urlParserService = urlParserService ?? throw new ArgumentNullException(nameof(urlParserService));
        _parameters = new ObservableCollection<KeyValueItemModel>();
        _parameters.CollectionChanged += OnParametersCollectionChanged;
    }

    /// <summary>
    /// Load parameters from URL string.
    /// </summary>
    public void LoadFromUrl(string url)
    {
        if (_isUpdating || string.IsNullOrWhiteSpace(url))
            return;

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
            OnPropertyChanged(nameof(EnabledParametersCount));
        }
    }

    /// <summary>
    /// Load parameters from collection.
    /// </summary>
    public void LoadParameters(IEnumerable<KeyValueItemModel>? parameters)
    {
        if (_isUpdating)
            return;

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
                        Key = param.Key,
                        Value = param.Value
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
            OnPropertyChanged(nameof(EnabledParametersCount));
        }
    }

    /// <summary>
    /// Build URL with query parameters.
    /// </summary>
    public string BuildUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return string.Empty;

        try
        {
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
    public IEnumerable<KeyValuePair<string, string>> GetEnabledParameters()
    {
        foreach (var param in Parameters.Where(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key)))
        {
            var key = param.Key.Trim();
            var value = (param.Value ?? "").Trim();
            
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    /// <summary>
    /// Export parameters to collection.
    /// </summary>
    public ObservableCollection<KeyValueItemModel> ToCollection()
    {
        return new ObservableCollection<KeyValueItemModel>(
            Parameters.Select(p => new KeyValueItemModel
            {
                IsEnabled = p.IsEnabled,
                Key = p.Key,
                Value = p.Value
            })
        );
    }

    [RelayCommand]
    private void AddParameter()
    {
        var newParam = new KeyValueItemModel { IsEnabled = true, Key = "", Value = "" };
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
    }

    private void OnParametersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Handle Reset action
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            OnPropertyChanged(nameof(EnabledParametersCount));
            NotifyUrlChanged();
            return;
        }

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
            OnPropertyChanged(nameof(EnabledParametersCount));
            NotifyUrlChanged();
        }
    }

    private void NotifyUrlChanged()
    {
        if (_isUpdating)
            return;

        UrlChanged?.Invoke(this, string.Empty);
    }

    public void Dispose()
    {
        try
        {
            // Unsubscribe from collection changes
            if (Parameters != null)
            {
                Parameters.CollectionChanged -= OnParametersCollectionChanged;

                // Unsubscribe from all parameter property changes
                foreach (var param in Parameters)
                {
                    param.PropertyChanged -= OnParameterPropertyChanged;
                }

                Parameters.Clear();
            }

            // Clear event handlers (safer than setting to null)
            if (UrlChanged != null)
            {
                foreach (var handler in UrlChanged.GetInvocationList())
                {
                    UrlChanged -= (EventHandler<string>)handler;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during QueryParamsViewModel disposal: {ex.Message}");
        }
    }
}
