using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using AvalonHttp.Models;
using AvalonHttp.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using AvalonHttp.Services.Interfaces;

namespace AvalonHttp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private ObservableCollection<ApiCollection> _collections;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private double _sidebarWidth = 280;

    public RequestViewModel RequestViewModel { get; }

    public ObservableCollection<string> HttpMethods { get; } = new ObservableCollection<string>()
    {
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "PATCH",
        "HEAD",
        "OPTIONS",
    };
    
    [ObservableProperty]
    private string _selectedRequestTab = "Params";
    
    [ObservableProperty]
    private string _selectedResponseTab = "Body";
    
    [RelayCommand]
    private void SelectRequestTab(string tabName)
    {
        SelectedRequestTab = tabName;
    }
    
    [RelayCommand]
    private void SelectResponseTab(string tabName)
    {
        SelectedResponseTab = tabName;
    }

    public MainWindowViewModel()
    {
        // Initialize services
        var httpService = new HttpService();
        var urlParserService = new UrlParserService();

        // Initialize ViewModels
        var headersViewModel = new HeadersViewModel();
        var queryParamsViewModel = new QueryParamsViewModel(urlParserService);
        var authViewModel = new AuthViewModel();
        RequestViewModel = new RequestViewModel(httpService, headersViewModel, queryParamsViewModel, authViewModel);
        
        InitializeCollections();
    }

    private void InitializeCollections()
    {
        Collections = new ObservableCollection<ApiCollection>
        {
            new ApiCollection
            {
                Name = "API Testing",
                Requests = new ObservableCollection<ApiRequest>
                {
                    new() { Name = "Get Users", Method = HttpMethod.Get, Url = "https://jsonplaceholder.typicode.com/users" },
                    new() { Name = "Create User", Method = HttpMethod.Post, Url = "https://jsonplaceholder.typicode.com/users" }
                }
            }
        };
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
        SidebarWidth = IsSidebarVisible ? 280 : 0;
    }

    [RelayCommand]
    private async Task CopyResponse()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow?.Clipboard != null)
            {
                await mainWindow.Clipboard.SetTextAsync(RequestViewModel.ResponseContent);
            }
        }
    }

    public void Dispose()
    {
        if (RequestViewModel is IDisposable disposableRequest)
        {
            disposableRequest.Dispose();
        }
        
        Collections.Clear();
        
        GC.SuppressFinalize(this);
    }
}
