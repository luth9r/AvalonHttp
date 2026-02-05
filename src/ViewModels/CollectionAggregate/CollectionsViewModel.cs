using System;
using System.Collections.Generic;
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

public partial class CollectionsViewModel : ViewModelBase, IDisposable
{
    private readonly ICollectionRepository _collectionService;
    private readonly ISessionService _sessionRepo;
    private readonly CompositeDisposable _cleanUp = new();
    
    // Source of truth for collections
    private readonly SourceList<CollectionItemViewModel> _collectionsSource = new();
    
    // UI binding for visible collections
    private readonly ReadOnlyObservableCollection<CollectionItemViewModel> _collections;
    public ReadOnlyObservableCollection<CollectionItemViewModel> Collections => _collections;

    [ObservableProperty]
    private RequestItemViewModel? _selectedRequest;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _isLoading = false;
    
    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool HasCollections => _collections.Count > 0;
    public bool HasSelection => SelectedRequest != null;

    public event EventHandler<ApiRequest>? RequestSelected;

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
    

    //  Call this from View.OnLoaded or App startup, not constructor
    public async Task InitializeAsync()
    {
        await LoadCollectionsAndRestoreStateAsync();
    }
    
    private async Task LoadCollectionsAndRestoreStateAsync()
    {
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
    
    private async Task RestoreLastSelectionAsync()
    {
        try
        {
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
                    await _collectionService.DeleteAsync(collection.Id);
                    
                    _collectionsSource.Remove(collection);
                    
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
    
        // Notify listeners
        OnRequestSelected(requestVm.Request);
    
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
    
    public async Task HandleRequestSavedAsync(ApiRequest request)
    {
        foreach (var collection in _collectionsSource.Items)
        {
            var requestVm = collection.AllRequests.FirstOrDefault(r => r.Id == request.Id);
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
        foreach (var collection in _collectionsSource.Items)
        {
            if (collection.IsEditing)
            {
                await collection.FinishRenameCommand.ExecuteAsync(null);
            }

            foreach (var request in collection.AllRequests)
            {
                if (request.IsEditing)
                {
                    await request.FinishRenameCommand.ExecuteAsync(null);
                }
            }
        }
    }

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
