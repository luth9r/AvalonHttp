using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services;
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
    private ObservableCollection<CollectionItemViewModel> _collections = new();

    [ObservableProperty]
    private CollectionItemViewModel? _selectedCollection;

    [ObservableProperty]
    private RequestItemViewModel? _selectedRequest;
    
    [ObservableProperty]
    private bool _isSelected = false;

    public event EventHandler<ApiRequest>? RequestSelected;

    public CollectionsViewModel(ICollectionRepository collectionService, ISessionService sessionService)
    {
        _collectionService = collectionService;
        _sessionService = sessionService;
        _ = LoadCollectionsAndRestoreStateAsync();
    }

    [RelayCommand]
    private async Task LoadCollectionsAndRestoreStateAsync()
    {
        var collections = await _collectionService.LoadAllAsync();
        
        Collections.Clear();
        foreach (var collection in collections)
        {
            Collections.Add(new CollectionItemViewModel(collection, this));
        }
        
        await RestoreLastSelectionAsync();
    }
    
    private async Task RestoreLastSelectionAsync()
    {
        var state = await _sessionService.LoadStateAsync();
        
        if (state.LastSelectedRequestId != null)
        {
            RequestItemViewModel? targetRequest = null;

            foreach (var collection in Collections)
            {
                var req = collection.Requests.FirstOrDefault(r => r.Id == state.LastSelectedRequestId);
                if (req != null)
                {
                    targetRequest = req;

                    collection.IsExpanded = true; 
                    break;
                }
            }

            if (targetRequest != null)
            {
                await Task.Delay(50);
                SelectRequest(targetRequest);
            }
        }
    }

    [RelayCommand]
    private async Task CreateCollection()
    {
        var collection = new ApiCollection
        {
            Name = "New Collection"
        };

        var viewModel = new CollectionItemViewModel(collection, this);
        Collections.Add(viewModel);
        await _collectionService.SaveAsync(collection);
    }

    [RelayCommand]
    private async Task DeleteCollection(CollectionItemViewModel collection)
    {
        WeakReferenceMessenger.Default.Send(new ConfirmMessage(
            "Delete Collection?",
            $"Are you sure you want to delete '{collection.Name}' and all its requests? This action cannot be undone.",
            async () => 
            {
                await _collectionService.DeleteAsync(collection.Id);
                Collections.Remove(collection);
            }
        ));
    }

    [RelayCommand]
    private async Task SaveCollection(CollectionItemViewModel collection)
    {
        await _collectionService.SaveAsync(collection.ToModel());
    }

    [RelayCommand]
    private async Task DuplicateCollection(CollectionItemViewModel collection)
    {
        var newCollection = new ApiCollection
        {
            Name = $"{collection.Name} (Copy)",
            Description = collection.Description,
            Requests = new ObservableCollection<ApiRequest>(
                Enumerable.Select<RequestItemViewModel, ApiRequest>(collection.Requests, r => r.ToModel()))
        };

        var viewModel = new CollectionItemViewModel(newCollection, this);
        Collections.Add(viewModel);
        await _collectionService.SaveAsync(newCollection);
    }
    
    public void SelectRequest(RequestItemViewModel requestVm)
    {
        if (SelectedRequest == requestVm) return;
        
        if (SelectedRequest != null)
        {
            SelectedRequest.IsSelected = false;
        }
        
        SelectedRequest = requestVm;
        SelectedRequest.IsSelected = true;
        
        OnRequestSelected(requestVm.ToModel());
        
        _ = _sessionService.SaveLastRequestAsync(requestVm.ToModel().Id);
    }

    public void OnRequestSelected(ApiRequest request)
    {
        RequestSelected?.Invoke(this, request);
    }

    public async Task SaveAllAsync()
    {
        foreach (var collection in Collections)
        {
            await _collectionService.SaveAsync(collection.ToModel());
        }
    }
    
    [RelayCommand]
    private void CloseAllEditModes()
    {
        foreach (var collection in Collections)
        {
            if (collection.IsEditing)
            {
                collection.IsEditing = false;
            }

            foreach (var request in collection.Requests)
            {
                if (request.IsEditing)
                {
                    request.IsEditing = false;
                }
            }
        }
    }
}