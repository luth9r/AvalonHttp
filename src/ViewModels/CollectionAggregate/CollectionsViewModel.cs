using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

namespace AvalonHttp.ViewModels.CollectionAggregate;

/// <summary>
/// Represents the view model for managing and interacting with a collection of items in the application.
/// </summary>
public partial class CollectionsViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Reference to collection repository
    /// </summary>
    private readonly ICollectionRepository _collectionService;
    
    /// <summary>
    /// Reference to session repository.
    /// </summary>
    private readonly ISessionService _sessionRepo;

    /// <summary>
    /// Manages the lifecycle of disposable resources to ensure proper cleanup for the view model.
    /// </summary>
    private readonly CompositeDisposable _cleanUp = new();
    
    /// <summary>
    /// Source of truth for collections.
    /// </summary>
    private readonly SourceList<CollectionItemViewModel> _collectionsSource = new();
    
    /// <summary>
    /// UI binding for collections.
    /// </summary>
    private readonly ReadOnlyObservableCollection<CollectionItemViewModel> _collections;

    /// <summary>
    /// Provides a read-only collection of collection items, representing the collections available in the application.
    /// </summary>
    public ReadOnlyObservableCollection<CollectionItemViewModel> Collections => _collections;

    /// <summary>
    /// The currently selected request item.
    /// </summary>
    [ObservableProperty]
    private RequestItemViewModel? _selectedRequest;
    
    /// <summary>
    /// Indicates whether the collections are currently being loaded.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _isLoading;

    /// <summary>
    /// Represents the search query entered by the user to filter collections.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    /// <summary>
    /// Indicates whether there is a selection in the collections list.
    /// </summary>
    public bool HasSelection => SelectedRequest != null;


    public CollectionsViewModel(
        ICollectionRepository collectionService, 
        ISessionService sessionService)
    {
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _sessionRepo = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        
        _collectionsSource.Connect()
            .AutoRefresh(x => x.IsVisible) // Auto-refresh when visibility changes
            .Filter(x => x.IsVisible)
            .Sort(SortExpressionComparer<CollectionItemViewModel>
                .Ascending(vm => _collectionsSource.Items.IndexOf(vm)))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Bind(out _collections)
            .Subscribe()
            .DisposeWith(_cleanUp);
        
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .DistinctUntilChanged()
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(query => 
            {
                System.Diagnostics.Debug.WriteLine($"Search query: '{query}'");
            })
            .DisposeWith(_cleanUp);
    }

    /// <summary>
    /// Initializes the view model by loading collections and restoring the state asynchronously.
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadCollectionsAndRestoreStateAsync();
    }
    
    /// <summary>
    /// Loads all collections from disk and populates the view model.
    /// </summary>
    private async Task LoadCollectionsAndRestoreStateAsync()
    {
        // Prevent multiple concurrent loads
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        
        try
        {
            var collections = await _collectionService.LoadAllAsync();
            
            // Dispose old VMs
            foreach (var vm in _collectionsSource.Items)
            {
                vm.Dispose();
            }
            _collectionsSource.Clear();

            // Load new collections
            var viewModels = collections.Select(c => new CollectionItemViewModel(c, this));
            _collectionsSource.AddRange(viewModels);
            
            await RestoreLastSelectionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load collections: {ex.Message}");
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                "Failed to Load Collections",
                $"Could not load collections from disk: {ex.Message}"
            ));
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    /// <summary>
    /// Restores the last selected request from the session state.
    /// </summary>
    private async Task RestoreLastSelectionAsync()
    {
        try
        {
            // Load session state
            var state = await _sessionRepo.LoadStateAsync();
            if (state.LastSelectedRequestId == null)
            {
                return;
            }

            // Search through all collections
            foreach (var collection in _collectionsSource.Items)
            {
                var request = collection.AllRequests.FirstOrDefault(r => r.Id == state.LastSelectedRequestId);
                if (request != null)
                {
                    collection.IsExpanded = true;
                    
                    // Select request asynchronously to avoid UI thread deadlock
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
        }
    }

    /// <summary>
    /// Creates a new collection and adds it to the view model.
    /// </summary>
    [RelayCommand]
    private async Task CreateCollection()
    {
        try
        {
            // Create new collection
            var collection = new ApiCollection
            {
                Name = GenerateUniqueCollectionName("New Collection")
            };

            // Add to view model
            var viewModel = new CollectionItemViewModel(collection, this);
            _collectionsSource.Add(viewModel);
            
            await _collectionService.SaveAsync(collection);
            
            // Start renaming immediately
            viewModel.StartRenameCommand.Execute(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create collection: {ex.Message}");
            
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                "Failed to Create Collection",
                $"Could not create new collection: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Deletes the specified collection from the view model.
    /// </summary>
    /// <param name="collection">The collection to delete. </param>
    [RelayCommand]
    private void DeleteCollection(CollectionItemViewModel collection)
    {
        if (collection == null)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(DialogMessage.Destructive(
            "Delete Collection?",
            $"Are you sure you want to delete '{collection.Name}' and all its requests? This action cannot be undone.",
            confirmText: "Delete",
            onConfirm: async () => 
            {
                try
                {
                    // Delete collection from disk
                    await _collectionService.DeleteAsync(collection.Id);
                    
                    // Remove from view model
                    _collectionsSource.Remove(collection);
                    
                    // Clear selection if necessary
                    if (SelectedRequest?.Parent == collection)
                    {
                        ClearSelection();
                    }
                    
                    collection.Dispose();
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Collection '{collection.Name}' deleted");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete collection: {ex.Message}");
                    
                    WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                        "Failed to Delete Collection",
                        $"Could not delete collection: {ex.Message}"
                    ));
                }
            }
        ));
    }

    /// <summary>
    /// Saves the specified collection to disk.
    /// </summary>
    /// <param name="collectionVm">The collection view model to save.</param>
    [RelayCommand]
    private async Task SaveCollection(CollectionItemViewModel collectionVm)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== SAVING COLLECTION: {collectionVm.Name} ===");
            
            // Sync VM → Model
            collectionVm.Collection.Name = collectionVm.Name;
            collectionVm.Collection.Description = collectionVm.Description;
            collectionVm.Collection.UpdatedAt = DateTime.Now;
    
            System.Diagnostics.Debug.WriteLine($"Requests in collection: {collectionVm.Collection.Requests.Count}");
            
            await _collectionService.SaveAsync(collectionVm.Collection);
    
            System.Diagnostics.Debug.WriteLine($"✅ Collection saved successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save collection: {ex.Message}");
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                "File System Error",
                $"Could not save collection '{collectionVm.Name}': {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Duplicates the specified collection and adds it to the view model.
    /// </summary>
    /// <param name="collection">The collection to duplicate.</param>
    [RelayCommand]
    private async Task DuplicateCollection(CollectionItemViewModel collection)
    {
        if (collection == null)
        {
            return;
        }

        try
        {
            // Create deep copy
            var newCollection = new ApiCollection
            {
                Id = Guid.NewGuid(),
                Name = GenerateUniqueCollectionName($"{collection.Name} (Copy)"),
                Description = collection.Description,
                Requests = new ObservableCollection<ApiRequest>(
                    collection.AllRequests.Select(r =>
                    {
                        var newRequest = r.CreateDeepCopy();
                        newRequest.Id = Guid.NewGuid();
                        return newRequest;
                    }))
            };

            var viewModel = new CollectionItemViewModel(newCollection, this);
            
            // Insert after original
            var index = _collectionsSource.Items.ToList().IndexOf(collection);
            _collectionsSource.Insert(index + 1, viewModel);
            
            await _collectionService.SaveAsync(newCollection);
            
            System.Diagnostics.Debug.WriteLine($"✅ Duplicated collection with {viewModel.AllRequests.Count()} requests");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to duplicate collection: {ex.Message}");
            
            WeakReferenceMessenger.Default.Send(DialogMessage.Error(
                "Failed to Duplicate Collection",
                $"Could not duplicate collection: {ex.Message}"
            ));
        }
    }
    
    /// <summary>
    /// Selects the specified request item and updates the session state.
    /// </summary>
    /// <param name="requestVm">The request item view model to select.</param>
    public void SelectRequest(RequestItemViewModel requestVm)
    {
        if (requestVm == null || SelectedRequest == requestVm)
        {
            return;
        }

        // Deselect previous
        if (SelectedRequest != null)
        {
            SelectedRequest.IsSelected = false;
        }
    
        // Select new
        SelectedRequest = requestVm;
        SelectedRequest.IsSelected = true;
    
        System.Diagnostics.Debug.WriteLine($"=== SELECTING REQUEST: {requestVm.Request.Name} ===");
        
        // Notify other views
        WeakReferenceMessenger.Default.Send(new RequestSelectedMessage(requestVm.Request));
    
        // Save session
        _ = _sessionRepo.SaveLastRequestAsync(requestVm.Id);
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        if (SelectedRequest != null)
        {
            SelectedRequest.IsSelected = false;
            SelectedRequest = null;
        }
    }

    /// <summary>
    /// Updates an existing request within the collections and persists the changes asynchronously.
    /// If the request is found in any collection, it updates the corresponding request view model
    /// and saves the collection.
    /// </summary>
    /// <param name="request">The updated request model containing the changes to synchronize.</param>
    public async Task HandleRequestSavedAsync(ApiRequest request)
    {
        foreach (var collection in _collectionsSource.Items)
        {
            // Find corresponding request
            var requestVm = collection.AllRequests.FirstOrDefault(r => r.Id == request.Id);
            if (requestVm != null)
            {
                // Update VM
                requestVm.UpdateFromModel(request);
                
                // Save collection
                await SaveCollectionCommand.ExecuteAsync(collection);
                return;
            }
        }
    }

    /// <summary>
    /// Updates the dirty state of the currently selected request.
    /// </summary>
    /// <param name="isDirty">A boolean value indicating whether the selected request is dirty.</param>
    public void UpdateSelectedRequestDirtyState(bool isDirty)
    {
        if (SelectedRequest != null)
        {
            SelectedRequest.IsDirty = isDirty;
        }
    }
    
    /// <summary>
    /// Closes all edit modes in all collections.
    /// </summary>
    [RelayCommand]
    private void CloseAllEditModes()
    {
        foreach (var collection in _collectionsSource.Items)
        {
            if (collection.IsEditing)
            {
                collection.CancelRenameCommand.Execute(null);
            }

            foreach (var request in collection.AllRequests)
            {
                if (request.IsEditing)
                {
                    request.CancelRenameCommand.Execute(null);
                }
            }
        }
    }

    /// <summary>
    /// Generates a unique collection name based on the specified base name.
    /// </summary>
    /// <param name="baseName">The base name for the collection.</param>
    /// <returns>A unique collection name.</returns>
    private string GenerateUniqueCollectionName(string baseName)
    {
        var existingNames = _collectionsSource.Items.Select(c => c.Name).ToHashSet();
    
        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        var counter = 1;
        string name;
    
        do
        {
            name = $"{baseName} ({counter++})";
        } while (existingNames.Contains(name));
    
        return name;
    }
    
    /// <summary>
    /// Moves the specified collection to a new position in the list.
    /// </summary>
    /// <param name="source">The collection to move.</param>
    /// <param name="target">The target collection for the move operation.</param>
    /// <param name="insertAfter">A boolean indicating whether to insert the source collection after the target collection.</param>
    public void MoveCollection(CollectionItemViewModel source, CollectionItemViewModel target, bool insertAfter)
    {
        var items = _collectionsSource.Items.ToList();
    
        var oldIndex = items.IndexOf(source);
        var targetIndex = items.IndexOf(target);

        if (oldIndex < 0 || targetIndex < 0)
        {
            return;
        }

        if (insertAfter)
        {
            targetIndex++;
        }

        if (oldIndex < targetIndex)
        {
            targetIndex--;
        }

        targetIndex = Math.Clamp(targetIndex, 0, items.Count - 1);

        if (oldIndex == targetIndex)
        {
            return;
        }

        _collectionsSource.Edit(list => {
            list.RemoveAt(oldIndex);
            list.Insert(targetIndex, source);
        });
        
    }

    /// <summary>
    /// Releases all resources used by the view model, including disposable objects and subscriptions.
    /// </summary>
    public void Dispose()
    {
        _cleanUp?.Dispose();
        
        foreach (var collection in _collectionsSource.Items)
        {
            collection.Dispose();
        }
        
        _collectionsSource.Dispose();
    }
}
