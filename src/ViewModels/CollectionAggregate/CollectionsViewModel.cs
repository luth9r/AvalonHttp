using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

namespace AvalonHttp.ViewModels.CollectionAggregate;

public partial class CollectionsViewModel : ViewModelBase
{
    private readonly ICollectionRepository _collectionService;
    private readonly ISessionService _sessionRepo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCollections))]
    private ObservableCollection<CollectionItemViewModel> _collections = new();

    [ObservableProperty]
    private RequestItemViewModel? _selectedRequest;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _isLoading = false;

    public bool HasCollections => Collections.Count > 0;
    public bool HasSelection => SelectedRequest != null;

    public event EventHandler<ApiRequest>? RequestSelected;

    public CollectionsViewModel(
        ICollectionRepository collectionService, 
        ISessionService sessionService)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _sessionRepo = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    }

    //  Call this from View.OnLoaded or App startup, not constructor
    public async Task InitializeAsync()
    {
        await LoadCollectionsAndRestoreStateAsync();
    }
    
    private async Task LoadCollectionsAndRestoreStateAsync()
    {
        if (IsLoading) return;
        
        IsLoading = true;
        
        try
        {
            var collections = await _collectionService.LoadAllAsync();
            
            Collections.Clear();
            foreach (var collection in collections)
            {
                Collections.Add(new CollectionItemViewModel(collection, this));
            }
            
            await RestoreLastSelectionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load collections: {ex.Message}");
            
            // Show error to user
            WeakReferenceMessenger.Default.Send(new ErrorMessage(
                "Failed to Load Collections",
                $"An error occurred while loading your collections: {ex.Message}"
            ));
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task RestoreLastSelectionAsync()
    {
        try
        {
            var state = await _sessionRepo.LoadStateAsync();
            
            if (state.LastSelectedRequestId == null) 
                return;

            // Find the request across all collections
            foreach (var collection in Collections)
            {
                var request = collection.Requests.FirstOrDefault(
                    r => r.Id == state.LastSelectedRequestId);
                    
                if (request != null)
                {
                    collection.IsExpanded = true;
                    
                    // Use Dispatcher instead of Task.Delay
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SelectRequest(request);
                    });
                    
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore selection: {ex.Message}");
            // Don't show error to user - not critical
        }
    }

    [RelayCommand]
    private async Task CreateCollection()
    {
        try
        {
            var collection = new ApiCollection
            {
                Name = GenerateUniqueCollectionName("New Collection")
            };

            var viewModel = new CollectionItemViewModel(collection, this);
            Collections.Add(viewModel);
            
            await _collectionService.SaveAsync(collection);
            
            // Start renaming immediately
            viewModel.StartRenameCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create collection: {ex.Message}");
            
            WeakReferenceMessenger.Default.Send(new ErrorMessage(
                "Failed to Create Collection",
                $"An error occurred: {ex.Message}"
            ));
        }
    }

    [RelayCommand]
    private void DeleteCollection(CollectionItemViewModel collection)
    {
        if (collection == null) return;

        WeakReferenceMessenger.Default.Send(new ConfirmMessage(
            "Delete Collection?",
            $"Are you sure you want to delete '{collection.Name}' and all its requests? This action cannot be undone.",
            async () => 
            {
                try
                {
                    await _collectionService.DeleteAsync(collection.Id);
                    Collections.Remove(collection);
                    
                    // Clear selection if deleted collection contained selected request
                    if (SelectedRequest?.Parent == collection)
                    {
                        ClearSelection();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete collection: {ex.Message}");
                    
                    WeakReferenceMessenger.Default.Send(new ErrorMessage(
                        "Failed to Delete Collection",
                        $"An error occurred: {ex.Message}"
                    ));
                }
            }
        ));
    }

    [RelayCommand]
    private async Task SaveCollection(CollectionItemViewModel collectionVm)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== SAVING COLLECTION: {collectionVm.Name} ===");
            
            collectionVm.Collection.Name = collectionVm.Name;
            collectionVm.Collection.Description = collectionVm.Description;
            collectionVm.Collection.UpdatedAt = DateTime.Now;
    
            System.Diagnostics.Debug.WriteLine($"Collection hash: {collectionVm.Collection.GetHashCode()}");
            System.Diagnostics.Debug.WriteLine($"Requests in collection: {collectionVm.Collection.Requests.Count}");
    
            foreach (var req in collectionVm.Collection.Requests)
            {
                System.Diagnostics.Debug.WriteLine($"  Request: {req.Name}");
                System.Diagnostics.Debug.WriteLine($"    Hash: {req.GetHashCode()}");
                System.Diagnostics.Debug.WriteLine($"    Body: '{req.Body}' (length: {req.Body?.Length ?? 0})");
            }
            
            await _collectionService.SaveAsync(collectionVm.Collection);
    
            System.Diagnostics.Debug.WriteLine($"✅ Collection saved successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save collection: {ex.Message}");
            WeakReferenceMessenger.Default.Send(new ErrorMessage(
                "File System Error",
                $"Could not save collection '{collectionVm.Name}': {ex.Message}"
            ));
        }
    }


    [RelayCommand]
    private async Task DuplicateCollection(CollectionItemViewModel collection)
    {
        if (collection == null) return;

        try
        {
            var newCollection = new ApiCollection
            {
                Id = Guid.NewGuid(), // New ID
                Name = GenerateUniqueCollectionName($"{collection.Name} (Copy)"),
                Description = collection.Description,
                Requests = new ObservableCollection<ApiRequest>(
                    collection.Requests.Select(r =>
                    {
                        var newRequest = r.CreateDeepCopy();
                        newRequest.Id = Guid.NewGuid(); // New ID for each request
                        return newRequest;
                    }))
            };

            var viewModel = new CollectionItemViewModel(newCollection, this);
            Collections.Add(viewModel);
            
            await _collectionService.SaveAsync(newCollection);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to duplicate collection: {ex.Message}");
            
            WeakReferenceMessenger.Default.Send(new ErrorMessage(
                "Failed to Duplicate Collection",
                $"An error occurred: {ex.Message}"
            ));
        }
    }
    
    public void SelectRequest(RequestItemViewModel requestVm)
    {
        if (requestVm == null || SelectedRequest == requestVm) 
            return;
    
        // Deselect previous
        if (SelectedRequest != null)
        {
            SelectedRequest.IsSelected = false;
        }
    
        // Select new
        SelectedRequest = requestVm;
        SelectedRequest.IsSelected = true;
    
        System.Diagnostics.Debug.WriteLine($"=== SELECTING REQUEST ===");
        System.Diagnostics.Debug.WriteLine($"  Name: {requestVm.Request.Name}");
        System.Diagnostics.Debug.WriteLine($"  Request hash: {requestVm.Request.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"  Body: '{requestVm.Request.Body}'");
    
        // Pass ORIGINAL object
        OnRequestSelected(requestVm.Request); 
    
        System.Diagnostics.Debug.WriteLine($"  Passed to OnRequestSelected");
    
        // Save session
        _ = _sessionRepo.SaveLastRequestAsync(requestVm.Id);
    }

    public void ClearSelection()
    {
        if (SelectedRequest != null)
        {
            SelectedRequest.IsSelected = false;
            SelectedRequest = null;
        }
    }

    private void OnRequestSelected(ApiRequest request)
    {
        RequestSelected?.Invoke(this, request);
    }

    public async Task SaveAllAsync()
    {
        var errors = new List<string>();
    
        foreach (var collection in Collections)
        {
            try
            {
                // Update properties before saving
                collection.Collection.Name = collection.Name;
                collection.Collection.Description = collection.Description;
                collection.Collection.UpdatedAt = DateTime.Now;
            
                // Save the original collection
                await _collectionService.SaveAsync(collection.Collection);
            }
            catch (Exception ex)
            {
                errors.Add($"{collection.Name}: {ex.Message}");
            }
        }

        if (errors.Any())
        {
            System.Diagnostics.Debug.WriteLine($"Save errors: {string.Join(", ", errors)}");
        }
    }
    
    public async Task HandleRequestSavedAsync(ApiRequest request)
    {
        foreach (var collection in Collections)
        {
            var requestVm = collection.Requests.FirstOrDefault(r => r.Id == request.Id);
            if (requestVm != null)
            {
                requestVm.UpdateFromModel(request);
                await SaveCollectionCommand.ExecuteAsync(collection);
                return;
            }
        }
    }
    
    public void UpdateSelectedRequestDirtyState(bool isDirty)
    {
        if (SelectedRequest != null)
        {
            SelectedRequest.IsDirty = isDirty;
        }
    }
    
    [RelayCommand]
    private async Task CloseAllEditModes()
    {
        foreach (var collection in Collections)
        {
            if (collection.IsEditing)
                await collection.FinishRenameCommand.ExecuteAsync(null);

            foreach (var request in collection.Requests)
            {
                if (request.IsEditing)
                    await request.FinishRenameCommand.ExecuteAsync(null);
            }
        }
    }

    private string GenerateUniqueCollectionName(string baseName)
    {
        var name = baseName;
        var counter = 1;
        
        while (Collections.Any(c => c.Name == name))
        {
            name = $"{baseName} ({counter++})";
        }
        
        return name;
    }
}
