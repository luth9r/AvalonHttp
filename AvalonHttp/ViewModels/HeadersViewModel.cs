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
    private ObservableCollection<HeaderItem> _headers;

    public HeadersViewModel()
    {
        _headers = new ObservableCollection<HeaderItem>
        {
            new() { IsEnabled = true, Key = "Content-Type", Value = "application/json" },
            new() { IsEnabled = false, Key = "Authorization", Value = "Bearer token..." }
        };
    }

    [RelayCommand]
    private void AddHeader()
    {
        Headers.Add(new HeaderItem { IsEnabled = true, Key = "", Value = "" });
    }

    [RelayCommand]
    private void RemoveHeader(HeaderItem header)
    {
        Headers.Remove(header);
    }

    public Dictionary<string, string> GetEnabledHeaders()
    {
        return Headers
            .Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Key))
            .ToDictionary(h => h.Key, h => h.Value ?? string.Empty);
    }
}