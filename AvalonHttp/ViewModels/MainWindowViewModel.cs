using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.ViewModels.CollectionAggregate;
using CommunityToolkit.Mvvm.Messaging;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

namespace AvalonHttp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private ObservableCollection<ApiCollection> _collections;
    
    public CollectionAggregate.CollectionsViewModel CollectionsViewModel { get; }

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private double _sidebarWidth = 280;

    public RequestViewModel RequestViewModel { get; }
    
    [ObservableProperty]
    private bool _isDialogOpen;

    [ObservableProperty]
    private string _dialogTitle = "";

    [ObservableProperty]
    private string _dialogMessage = "";
    
    private Func<Task>? _pendingConfirmAction;

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
    private string _confirmButtonText = "Exit Anyway";

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

    public MainWindowViewModel(CollectionsViewModel collectionsViewModel, RequestViewModel requestViewModel)
    {
        CollectionsViewModel = collectionsViewModel;
        RequestViewModel = requestViewModel;

        CollectionsViewModel.RequestSelected += OnRequestSelected;
        RequestViewModel.RequestSaved += OnRequestSaved;
        RequestViewModel.PropertyChanged += OnRequestViewModelPropertyChanged;
        
        WeakReferenceMessenger.Default.Register<ConfirmMessage>(this, OnConfirmMessageReceived);
        
        InitializeCollections();
    }
    
    private void OnConfirmMessageReceived(object recipient, ConfirmMessage message)
    {
        DialogTitle = message.Title;
        DialogMessage = message.Message;
        _pendingConfirmAction = message.OnConfirm;
        
        IsDialogOpen = true;
    }
    
    [RelayCommand]
    private async Task ExecuteConfirm()
    {
        IsDialogOpen = false;
        if (_pendingConfirmAction != null)
        {
            await _pendingConfirmAction.Invoke();
            _pendingConfirmAction = null;
        }
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsDialogOpen = false;
        _pendingConfirmAction = null;
    }
    
    private void OnRequestViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RequestViewModel.IsDirty))
        {
            var activeItem = CollectionsViewModel.SelectedRequest;
            if (activeItem != null)
            {
                activeItem.IsDirty = RequestViewModel.IsDirty;
            }
        }
    }
    
    private void OnRequestSelected(object? sender, ApiRequest request)
    {
        RequestViewModel.LoadRequest(request);
    }

    private void OnRequestSaved(object? sender, ApiRequest savedRequest)
    {
        var selectedItem = CollectionsViewModel.SelectedRequest;

        if (selectedItem != null && selectedItem.ToModel() == savedRequest)
        {
            var collectionVm = selectedItem.Parent;
            
            if (CollectionsViewModel.SaveCollectionCommand.CanExecute(collectionVm))
            {
                CollectionsViewModel.SaveCollectionCommand.ExecuteAsync(collectionVm);
            }
        }
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
    
    [RelayCommand]
    public void CloseAllEdits()
    {
        foreach (var collection in CollectionsViewModel.Collections)
        {
            if (collection.IsEditing)
                collection.FinishRenameCommand?.Execute(null);
            
            foreach (var request in collection.Requests)
            {
                if (request.IsEditing)
                    request.FinishRenameCommand?.Execute(null);
            }
        }
    }
    
    [RelayCommand]
    private void AttemptExit()
    {
        if (RequestViewModel.IsDirty)
        {
            WeakReferenceMessenger.Default.Send(new ConfirmMessage(
                "Unsaved Changes",
                "You have unsaved changes. Exit anyway?",
                () => 
                {
                    System.Environment.Exit(0); 
                    return Task.CompletedTask;
                },
                this.ConfirmButtonText
            ));
        }
        else
        {
            System.Environment.Exit(0);
        }
    }
    
    public void Dispose()
    {
        if (RequestViewModel is IDisposable disposableRequest)
        {
            disposableRequest.Dispose();
        }
        
        CollectionsViewModel.RequestSelected -= OnRequestSelected;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        Collections.Clear();
        
        GC.SuppressFinalize(this);
    }
}
