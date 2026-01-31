using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvalonHttp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

public partial class HeadersViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<KeyValueItemModel> _headers = new();
    
    public int EnabledHeadersCount => 
        Headers.Count(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key));

    public HeadersViewModel()
    {
        Headers.CollectionChanged += (s, e) => OnPropertyChanged(nameof(EnabledHeadersCount));
    }

    [RelayCommand]
    private void AddHeader()
    {
        var header = new KeyValueItemModel { IsEnabled = true };
        header.PropertyChanged += (s, e) => OnPropertyChanged(nameof(EnabledHeadersCount));
        Headers.Add(header);
    }

    [RelayCommand]
    private void RemoveHeader(KeyValueItemModel header)
    {
        Headers.Remove(header);
    }

    public Dictionary<string, string> GetEnabledHeaders()
    {
        return Headers
            .Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key))
            .ToDictionary(h => h.Key, h => h.Value ?? "");
    }
}