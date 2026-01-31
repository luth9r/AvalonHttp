using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

namespace AvalonHttp.ViewModels.CollectionAggregate;

public partial class CollectionItemViewModel : ObservableObject
{
    public readonly CollectionsViewModel _parent;
    
    public CollectionsViewModel Parent => _parent;

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isEditing = false;

    [ObservableProperty]
    private ObservableCollection<RequestItemViewModel> _requests = new();

    public CollectionItemViewModel(ApiCollection collection, CollectionsViewModel parent)
    {
        _parent = parent;
        _id = collection.Id;
        _name = collection.Name;
        _description = collection.Description;

        foreach (var request in collection.Requests)
        {
            Requests.Add(new RequestItemViewModel(request, this));
        }
    }

    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    private void StartRename()
    {
        IsEditing = true;
    }

    [RelayCommand]
    private async Task FinishRename()
    {
        IsEditing = false;
        await _parent.SaveCollectionCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private async Task AddRequest()
    {
        var request = new ApiRequest
        {
            Name = "New Request",
            Url = "https://",
            MethodString = "GET"
        };

        var viewModel = new RequestItemViewModel(request, this);
        Requests.Add(viewModel);
        await _parent.SaveCollectionCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private async Task DeleteRequest(RequestItemViewModel request)
    {
        Requests.Remove(request);
        await _parent.SaveCollectionCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private async Task DuplicateRequest(RequestItemViewModel request)
    {
        var newRequest = request.ToModel();
        newRequest.Name = $"{request.Name} (Copy)";

        var viewModel = new RequestItemViewModel(newRequest, this);
        var index = Requests.IndexOf(request);
        Requests.Insert(index + 1, viewModel);
        
        await _parent.SaveCollectionCommand.ExecuteAsync(this);
    }

    public ApiCollection ToModel()
    {
        return new ApiCollection
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Requests = new ObservableCollection<ApiRequest>(
                Enumerable.Select<RequestItemViewModel, ApiRequest>(Requests, r => r.ToModel()))
        };
    }
}
