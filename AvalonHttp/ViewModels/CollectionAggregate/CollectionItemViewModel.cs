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
    private readonly CollectionsViewModel _parent;
    private string _originalName = string.Empty;
    
    public CollectionsViewModel Parent => _parent;

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
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));;
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
            newRequest.Id = Guid.NewGuid(); // ✅ New unique ID
            newRequest.Name = GenerateUniqueName($"{request.Name} (Copy)");

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
