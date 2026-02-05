using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.ViewModels.CollectionAggregate;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AvalonHttp.ViewModels;

/// <summary>
/// Represents the view model for the main workspace of the application.
/// </summary>
public partial class CollectionWorkspaceViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Represents the view model for the main workspace of the application.
    /// </summary>
    public CollectionsViewModel CollectionsViewModel { get; }
    
    /// <summary>
    /// Represents the view model responsible for managing HTTP request data, including headers,
    /// </summary>
    public RequestViewModel RequestViewModel { get; }
    
    /// <summary>
    /// Represents the view model responsible for managing environments and associated environment variables.
    /// </summary>
    public EnvironmentsViewModel EnvironmentsViewModel { get; }

    /// <summary>
    /// Represents the current state of the sidebar visibility in the workspace.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    private bool _isSidebarVisible = true;
    
    public double SidebarWidth => IsSidebarVisible ? 280 : 0;
    
    public CollectionWorkspaceViewModel(
        CollectionsViewModel collectionsViewModel, 
        RequestViewModel requestViewModel,
        EnvironmentsViewModel environmentsViewModel)
    {
        CollectionsViewModel = collectionsViewModel ?? throw new ArgumentNullException(nameof(collectionsViewModel));
        RequestViewModel = requestViewModel ?? throw new ArgumentNullException(nameof(requestViewModel));
        EnvironmentsViewModel = environmentsViewModel ?? throw new ArgumentNullException(nameof(environmentsViewModel));
        
        WeakReferenceMessenger.Default.Register<RequestSelectedMessage>(this, (_, message) =>
        {
            System.Diagnostics.Debug.WriteLine($"Request selected: {message.Request.Name}");
            
            RequestViewModel.LoadRequest(message.Request);
        });
        
        WeakReferenceMessenger.Default.Register<RequestSavedMessage>(this, async (_, message) =>
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
    
    /// <summary>
    /// Initializes the view model asynchronously.
    /// </summary>
    public async Task InitializeAsync()
    {
        await CollectionsViewModel.InitializeAsync();
    }
    
    /// <summary>
    /// Toggles the visibility of the sidebar in the workspace.
    /// </summary>
    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }
    
    /// <summary>
    /// Disposes of the view model.
    /// </summary>
    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<RequestSelectedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<RequestSavedMessage>(this);
        
        if (CollectionsViewModel is IDisposable collectionsDisposable)
        {
            collectionsDisposable.Dispose();
        }
        
        if (RequestViewModel is IDisposable requestDisposable)
        {
            requestDisposable.Dispose();
        }
        
        if (EnvironmentsViewModel is IDisposable environmentsDisposable)
        {
            environmentsDisposable.Dispose();
        }
    }
}
