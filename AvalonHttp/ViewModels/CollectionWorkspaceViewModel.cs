using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.ViewModels.CollectionAggregate;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels;

public partial class CollectionWorkspaceViewModel : ViewModelBase, IDisposable
{
    // ========================================
    // Child ViewModels
    // ========================================
    
    public CollectionsViewModel CollectionsViewModel { get; }
    public RequestViewModel RequestViewModel { get; }
    public EnvironmentsViewModel EnvironmentsViewModel { get; }

    // ========================================
    // Workspace UI State
    // ========================================
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    private bool _isSidebarVisible = true;
    
    // Computed property - auto-sync
    public double SidebarWidth => IsSidebarVisible ? 280 : 0;
    
    // ========================================
    // Tab Selection
    // ========================================
    
    [ObservableProperty]
    private string _selectedRequestTab = "Params";
    
    [ObservableProperty]
    private string _selectedResponseTab = "Body";

    // ========================================
    // Constructor
    // ========================================
    
    public CollectionWorkspaceViewModel(
        CollectionsViewModel collectionsViewModel, 
        RequestViewModel requestViewModel,
        EnvironmentsViewModel environmentsViewModel)
    {
        CollectionsViewModel = collectionsViewModel ?? throw new ArgumentNullException(nameof(collectionsViewModel));
        RequestViewModel = requestViewModel ?? throw new ArgumentNullException(nameof(requestViewModel));
        EnvironmentsViewModel = environmentsViewModel ?? throw new ArgumentNullException(nameof(environmentsViewModel));

        // Subscribe to events
        CollectionsViewModel.RequestSelected += OnRequestSelected;
        RequestViewModel.PropertyChanged += OnRequestViewModelPropertyChanged;
        RequestViewModel.RequestSaved += OnRequestSaved;
    }
    
    // ========================================
    // Initialization
    // ========================================
    
    public async Task InitializeAsync()
    {
        await CollectionsViewModel.InitializeAsync();
    }

    // ========================================
    // UI Commands
    // ========================================
    
    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    [RelayCommand]
    private void SelectRequestTab(string? tabName)
    {
        if (!string.IsNullOrEmpty(tabName))
        {
            SelectedRequestTab = tabName;
        }
    }
    
    [RelayCommand]
    private void SelectResponseTab(string? tabName)
    {
        if (!string.IsNullOrEmpty(tabName))
        {
            SelectedResponseTab = tabName;
        }
    }

    [RelayCommand]
    private async Task CopyResponse()
    {
        if (string.IsNullOrEmpty(RequestViewModel.ResponseContent)) return;

        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.Clipboard != null)
                {
                    await mainWindow.Clipboard.SetTextAsync(RequestViewModel.ResponseContent);
                    System.Diagnostics.Debug.WriteLine("Response copied to clipboard");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
        }
    }

    // ========================================
    // Event Handlers
    // ========================================

    private void OnRequestViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RequestViewModel.IsDirty))
        {
            // Sync IsDirty state to collection item
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
    
    private async void OnRequestSaved(object? sender, ApiRequest request)
    {
        System.Diagnostics.Debug.WriteLine($"");
        System.Diagnostics.Debug.WriteLine($"🟡 OnRequestSaved triggered!");
        System.Diagnostics.Debug.WriteLine($"   Request ID: {request.Id}");
        System.Diagnostics.Debug.WriteLine($"   Name: {request.Name}");
        System.Diagnostics.Debug.WriteLine($"   Body: '{request.Body}'");
        
        try
        {
            foreach (var collection in CollectionsViewModel.Collections)
            {
                var requestVm = collection.Requests.FirstOrDefault(r => r.Id == request.Id);
                if (requestVm != null)
                {
                    System.Diagnostics.Debug.WriteLine($"   Found in collection: {collection.Name}");
                    
                    requestVm.UpdateFromModel(request);
                    
                    await CollectionsViewModel.SaveCollectionCommand.ExecuteAsync(collection);
                    
                    System.Diagnostics.Debug.WriteLine($"   ✅ Collection saved to disk!");
                    return;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"   ⚠️ Request not found in any collection!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"   ❌ Error saving: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");
        }
    }

    // ========================================
    // Cleanup
    // ========================================
    
    public void Dispose()
    {
        // Unsubscribe from events
        CollectionsViewModel.RequestSelected -= OnRequestSelected;
        RequestViewModel.PropertyChanged -= OnRequestViewModelPropertyChanged;
        CollectionsViewModel.RequestSelected -= OnRequestSelected;
        
        // Dispose child ViewModels if needed
        if (RequestViewModel is IDisposable disposableRequest)
        {
            disposableRequest.Dispose();
        }
        
        if (CollectionsViewModel is IDisposable disposableCollections)
        {
            disposableCollections.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}
