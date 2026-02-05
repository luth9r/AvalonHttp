using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.ViewModels.CollectionAggregate;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

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
        
        WeakReferenceMessenger.Default.Register<RequestSelectedMessage>(this, (recipient, message) =>
        {
            System.Diagnostics.Debug.WriteLine($"Request selected: {message.Request.Name}");
            
            RequestViewModel.LoadRequest(message.Request);
        });
        
        WeakReferenceMessenger.Default.Register<RequestSavedMessage>(this, async (recipient, message) =>
        {
            System.Diagnostics.Debug.WriteLine($"RequestSavedMessage received: {message.Request.Name}");
            await CollectionsViewModel.HandleRequestSavedAsync(message.Request);
        });

        RequestViewModel.PropertyChanged += (_, e) => 
        {
            if (e.PropertyName == nameof(RequestViewModel.IsDirty))
            {
                CollectionsViewModel.UpdateSelectedRequestDirtyState(RequestViewModel.IsDirty);
            }
        };
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

    // ========================================
    // Cleanup
    // ========================================
    
    public void Dispose()
    {
        // Todo: unsubscribe from events
    }
}
