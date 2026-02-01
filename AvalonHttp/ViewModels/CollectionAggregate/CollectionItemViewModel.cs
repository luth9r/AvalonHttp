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
    private readonly ApiCollection _collection; // Store reference
    private readonly CollectionsViewModel _parent;
    private string _originalName = string.Empty;
    
    public CollectionsViewModel Parent => _parent;
    public ApiCollection Collection => _collection; // Expose collection


    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isEditing = false;

    public ObservableCollection<RequestItemViewModel> Requests { get; } = new();

    public CollectionItemViewModel(ApiCollection collection, CollectionsViewModel parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _id = collection.Id;
        _name = collection.Name;
        _description = collection.Description;

        System.Diagnostics.Debug.WriteLine($"🟢 === CollectionItemViewModel Constructor ===");
        System.Diagnostics.Debug.WriteLine($"Collection: {collection.Name}");
        System.Diagnostics.Debug.WriteLine($"Collection hash: {collection.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"Collection.Requests count: {collection.Requests.Count}");
        
        foreach (var request in collection.Requests)
        {
            System.Diagnostics.Debug.WriteLine($"🟢 Creating RequestItemVM:");
            System.Diagnostics.Debug.WriteLine($"    Request: {request.Name}");
            System.Diagnostics.Debug.WriteLine($"    Hash: {request.GetHashCode()}");
            System.Diagnostics.Debug.WriteLine($"    Body: '{request.Body}'");
            
            var requestVm = new RequestItemViewModel(request, this);
            Requests.Add(requestVm);
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
        _originalName = Name;
        IsEditing = true;
    }

    [RelayCommand(CanExecute = nameof(CanFinishRename))]
    private async Task FinishRename()
    {
        if (!CanFinishRename())
        {
            // Restore original name if invalid
            Name = _originalName;
            IsEditing = false;
            return;
        }

        IsEditing = false;
        _collection.Name = Name;
        _collection.UpdatedAt = DateTime.Now;
        
        try
        {
            await _parent.SaveCollectionCommand.ExecuteAsync(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save collection: {ex.Message}");
            // TODO: Show error to user
        }
    }
    
    private bool CanFinishRename()
    {
        return !string.IsNullOrWhiteSpace(Name);
    }
    
    [RelayCommand]
    private void CancelRename()
    {
        Name = _originalName;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task AddRequest()
    {
        try
        {
            var request = new ApiRequest
            {
                Name = "New Request",
                Url = "https://api.example.com",
                MethodString = "GET"
            };

            _collection.Requests.Add(request);
            var viewModel = new RequestItemViewModel(request, this);
            Requests.Add(viewModel);
            
            // Select the new request
            _parent.SelectRequest(viewModel);
            
            await _parent.SaveCollectionCommand.ExecuteAsync(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to add request: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteRequest(RequestItemViewModel request)
    {
        if (request == null) return;

        try
        {
            var index = Requests.IndexOf(request);
            _collection.Requests.Remove(request.Request);
            Requests.Remove(request);
            
            // Select adjacent request if available
            if (Requests.Count > 0)
            {
                var newIndex = Math.Min(index, Requests.Count - 1);
                _parent.SelectRequest(Requests[newIndex]);
            }
            else
            {
                _parent.ClearSelection();
            }
            
            await _parent.SaveCollectionCommand.ExecuteAsync(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete request: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DuplicateRequest(RequestItemViewModel request)
    {
        if (request == null) return;

        try
        {
            var newRequest = request.ToModel();
            newRequest.Id = Guid.NewGuid();
            newRequest.Name = GenerateUniqueName($"{request.Name} (Copy)");

            _collection.Requests.Add(newRequest);
            var viewModel = new RequestItemViewModel(newRequest, this);
            var index = Requests.IndexOf(request);
            Requests.Insert(index + 1, viewModel);
            
            // Select the duplicated request
            _parent.SelectRequest(viewModel);
            
            await _parent.SaveCollectionCommand.ExecuteAsync(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to duplicate request: {ex.Message}");
        }
    }
    
    private string GenerateUniqueName(string baseName)
    {
        var name = baseName;
        var counter = 1;
        
        while (Requests.Any(r => r.Name == name))
        {
            name = $"{baseName} ({counter++})";
        }
        
        return name;
    }
    
    public void UpdateFromModel(ApiCollection collection)
    {
        Name = collection.Name;
        Description = collection.Description;
        
        // Update requests (simple version - replace all)
        Requests.Clear();
        foreach (var request in collection.Requests)
        {
            Requests.Add(new RequestItemViewModel(request, this));
        }
    }
}
