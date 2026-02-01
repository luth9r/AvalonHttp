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
    public CollectionsViewModel CollectionsViewModel { get; }
    public RequestViewModel RequestViewModel { get; }

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private double _sidebarWidth = 280;
    
    [ObservableProperty]
    private bool _isDialogOpen;

    [ObservableProperty]
    private string _dialogTitle = "";

    [ObservableProperty]
    private string _dialogMessage = "";
    
    [ObservableProperty] 
    private string _confirmButtonText = "Confirm";
    
    [ObservableProperty]
    private string _selectedRequestTab = "Params";
    
    [ObservableProperty]
    private string _selectedResponseTab = "Body";
    
    private Func<Task>? _pendingConfirmAction;

    public ObservableCollection<string> HttpMethods { get; } = new()
    {
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "PATCH",
        "HEAD",
        "OPTIONS",
    };

    public MainWindowViewModel(
        CollectionsViewModel collectionsViewModel, 
        RequestViewModel requestViewModel)
    {
        CollectionsViewModel = collectionsViewModel ?? 
            throw new ArgumentNullException(nameof(collectionsViewModel));
        RequestViewModel = requestViewModel ?? 
            throw new ArgumentNullException(nameof(requestViewModel));

        // Subscribe to events
        CollectionsViewModel.RequestSelected += OnRequestSelected;
        RequestViewModel.RequestSaved += OnRequestSaved;
        RequestViewModel.PropertyChanged += OnRequestViewModelPropertyChanged;
        
        WeakReferenceMessenger.Default.Register<ConfirmMessage>(this, OnConfirmMessageReceived);
    }
    
    private void OnConfirmMessageReceived(object recipient, ConfirmMessage message)
    {
        DialogTitle = message.Title;
        DialogMessage = message.Message;
        ConfirmButtonText = message.ConfirmButtonText ?? "Confirm";
        _pendingConfirmAction = message.OnConfirm;
        
        IsDialogOpen = true;
    }
    
    [RelayCommand]
    private async Task ExecuteConfirm()
    {
        IsDialogOpen = false;
        
        if (_pendingConfirmAction != null)
        {
            try
            {
                await _pendingConfirmAction.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Confirm action failed: {ex.Message}");
            }
            finally
            {
                _pendingConfirmAction = null;
            }
        }
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsDialogOpen = false;
        _pendingConfirmAction = null;
    }
    
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

    private async void OnRequestSaved(object? sender, ApiRequest savedRequest)
    {
        var selectedItem = CollectionsViewModel.SelectedRequest;

        if (selectedItem != null && selectedItem.Id == savedRequest.Id)
        {
            var collectionVm = selectedItem.Parent;
            
            try
            {
                await CollectionsViewModel.SaveCollectionCommand.ExecuteAsync(collectionVm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save collection after request save: {ex.Message}");
            }
        }
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
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.Clipboard != null && !string.IsNullOrEmpty(RequestViewModel.ResponseContent))
                {
                    await mainWindow.Clipboard.SetTextAsync(RequestViewModel.ResponseContent);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy response: {ex.Message}");
        }
    }
    
    [RelayCommand]
    public async Task CloseAllEdits()
    {
        try
        {
            foreach (var collection in CollectionsViewModel.Collections)
            {
                if (collection.IsEditing)
                {
                    await collection.FinishRenameCommand.ExecuteAsync(null);
                }
                
                foreach (var request in collection.Requests)
                {
                    if (request.IsEditing)
                    {
                        await request.FinishRenameCommand.ExecuteAsync(null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to close edits: {ex.Message}");
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
                    Environment.Exit(0); 
                    return Task.CompletedTask;
                },
                "Exit Anyway"
            ));
        }
        else
        {
            Environment.Exit(0);
        }
    }
    
    public void Dispose()
    {
        try
        {
            // Unsubscribe from events
            if (CollectionsViewModel != null)
            {
                CollectionsViewModel.RequestSelected -= OnRequestSelected;
            }

            if (RequestViewModel != null)
            {
                RequestViewModel.RequestSaved -= OnRequestSaved;
                RequestViewModel.PropertyChanged -= OnRequestViewModelPropertyChanged;
                
                if (RequestViewModel is IDisposable disposableRequest)
                {
                    disposableRequest.Dispose();
                }
            }

            // Unregister from messenger
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during MainWindowViewModel disposal: {ex.Message}");
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }
}
