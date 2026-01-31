using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private ObservableCollection<KeyValueItemModel> _parameters;

    public int EnabledParametersCount => 
        Parameters.Count(p => p.IsEnabled && !string.IsNullOrWhiteSpace(p.Key));

    public event EventHandler<string>? UrlChanged;

    public QueryParamsViewModel(IUrlParserService urlParserService)
    {
        _urlParserService = urlParserService;
        _parameters = new ObservableCollection<KeyValueItemModel>();
        _parameters.CollectionChanged += OnParametersChanged;
    }

    public void LoadFromUrl(string url)
    {
        if (_isUpdating) return;

        try
        {
            _isUpdating = true;

            var (_, parameters) = _urlParserService.ParseUrl(url);

            Parameters.Clear();
            foreach (var param in parameters)
            {
                param.PropertyChanged += OnParameterPropertyChanged;
                Parameters.Add(param);
            }
        }
        finally
        {
            _isUpdating = false;
            OnPropertyChanged(nameof(EnabledParametersCount));
        }
    }

    public string BuildUrl(string baseUrl)
    {
        return _urlParserService.BuildUrl(baseUrl, Parameters);
    }

    [RelayCommand]
    private void AddParameter()
    {
        var newParam = new KeyValueItemModel { IsEnabled = true, Key = "", Value = "" };
        newParam.PropertyChanged += OnParameterPropertyChanged;
        Parameters.Add(newParam);
    }

    [RelayCommand]
    private void RemoveParameter(KeyValueItemModel param)
    {
        param.PropertyChanged -= OnParameterPropertyChanged;
        Parameters.Remove(param);
    }

    private void OnParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (e.NewItems != null)
        {
            foreach (KeyValueItemModel param in e.NewItems)
            {
                param.PropertyChanged += OnParameterPropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (KeyValueItemModel param in e.OldItems)
            {
                param.PropertyChanged -= OnParameterPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(EnabledParametersCount));
        NotifyUrlChanged();
    }

    private void OnParameterPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
        if (_isUpdating) return;
        UrlChanged?.Invoke(this, string.Empty);
    }

    public void Dispose()
    {
        if (_parameters != null)
        {
            _parameters.CollectionChanged -= OnParametersChanged;
        }

        if (_parameters != null)
        {
            foreach (var param in _parameters)
            {
                param.PropertyChanged -= OnParameterPropertyChanged;
            }
        }
        
        _parameters?.Clear();
        
        UrlChanged = null;
    }
}
