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
    private readonly ISessionService _sessionService;

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
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
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
            // WeakReferenceMessenger.Default.Send(new ErrorMessage(
            //     "Failed to Load Collections",
            //     $"An error occurred while loading your collections: {ex.Message}"
            // ));
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
            var state = await _sessionService.LoadStateAsync();
            
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
                    
                    // ✅ Use Dispatcher instead of Task.Delay
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
            
            // WeakReferenceMessenger.Default.Send(new ErrorMessage(
            //     "Failed to Create Collection",
            //     $"An error occurred: {ex.Message}"
            // ));
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
                    
                    // WeakReferenceMessenger.Default.Send(new ErrorMessage(
                    //     "Failed to Delete Collection",
                    //     $"An error occurred: {ex.Message}"
                    // ));
                }
            }
        ));
    }

    [RelayCommand]
    public async Task SaveCollection(CollectionItemViewModel collection)
    {
        if (collection == null) return;

        try
        {
            await _collectionService.SaveAsync(collection.ToModel());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save collection: {ex.Message}");
            
            // WeakReferenceMessenger.Default.Send(new ErrorMessage(
            //     "Failed to Save Collection",
            //     $"An error occurred: {ex.Message}"
            // ));
            
            throw; // Re-throw so caller knows save failed
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
                Id = Guid.NewGuid(), // ✅ New ID
                Name = GenerateUniqueCollectionName($"{collection.Name} (Copy)"),
                Description = collection.Description,
                Requests = new ObservableCollection<ApiRequest>(
                    collection.Requests.Select(r =>
                    {
                        var newRequest = r.ToModel();
                        newRequest.Id = Guid.NewGuid(); // ✅ New ID for each request
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
            
            // WeakReferenceMessenger.Default.Send(new ErrorMessage(
            //     "Failed to Duplicate Collection",
            //     $"An error occurred: {ex.Message}"
            // ));
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
        
        // Notify listeners
        OnRequestSelected(requestVm.ToModel());
        
        // ✅ Proper async handling - don't block UI
        _ = Task.Run(async () =>
        {
            try
            {
                await _sessionService.SaveLastRequestAsync(requestVm.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
                // Don't show error - not critical
            }
        });
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
                await _collectionService.SaveAsync(collection.ToModel());
            }
            catch (Exception ex)
            {
                errors.Add($"{collection.Name}: {ex.Message}");
            }
        }

        if (errors.Any())
        {
            // var errorMessage = string.Join("\n", errors);
            // WeakReferenceMessenger.Default.Send(new ErrorMessage(
            //     "Failed to Save Some Collections",
            //     errorMessage
            // ));
        }
    }
    
    [RelayCommand]
    private async Task CloseAllEditModes()
    {
        var modifiedCollections = new List<CollectionItemViewModel>();
        
        foreach (var collection in Collections)
        {
            var wasEdited = false;
            
            if (collection.IsEditing)
            {
                // Finish rename will validate and save
                await collection.FinishRenameCommand.ExecuteAsync(null);
                wasEdited = true;
            }

            foreach (var request in collection.Requests)
            {
                if (request.IsEditing)
                {
                    await request.FinishRenameCommand.ExecuteAsync(null);
                    wasEdited = true;
                }
            }
            
            if (wasEdited && !modifiedCollections.Contains(collection))
            {
                modifiedCollections.Add(collection);
            }
        }
        
        // Save modified collections
        foreach (var collection in modifiedCollections)
        {
            try
            {
                await _collectionService.SaveAsync(collection.ToModel());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save collection: {ex.Message}");
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
