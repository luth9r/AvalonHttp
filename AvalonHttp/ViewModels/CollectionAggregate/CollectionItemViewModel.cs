using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvalonHttp.ViewModels.CollectionAggregate;

public partial class CollectionItemViewModel : ObservableObject, IDisposable
{
    // ========================================
    // Fields
    // ========================================

    private readonly ApiCollection _collection;
    private readonly CollectionsViewModel _parent;
    private string _originalName = string.Empty;

    // ========================================
    // Properties
    // ========================================

    public CollectionsViewModel Parent => _parent;
    public ApiCollection Collection => _collection;

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isEditing;

    public ObservableCollection<RequestItemViewModel> Requests { get; } = new();

    // ========================================
    // Constructor
    // ========================================

    public CollectionItemViewModel(ApiCollection collection, CollectionsViewModel parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));

        Id = collection.Id;
        Name = collection.Name;
        Description = collection.Description;

        LoadRequests();
    }

    private void LoadRequests()
    {
        foreach (var request in _collection.Requests)
        {
            var requestVm = new RequestItemViewModel(request, this);
            Requests.Add(requestVm);
        }
    }

    // ========================================
    // Expand/Collapse
    // ========================================

    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    // ========================================
    // Rename Commands
    // ========================================

    [RelayCommand]
    private void StartRename()
    {
        _originalName = Name;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task FinishRename()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name == _originalName)
        {
            CancelRename();
            return;
        }

        IsEditing = false;

        await SaveCollection();
    }


    [RelayCommand]
    private void CancelRename()
    {
        Name = _originalName;
        IsEditing = false;
    }

    // ========================================
    // Request Management
    // ========================================

    [RelayCommand]
    private async Task AddRequest()
    {
        var request = new ApiRequest
        {
            Id = Guid.NewGuid(),
            Name = GenerateUniqueName("New Request"),
            Url = "https://api.example.com",
            MethodString = "GET",
        };

        _collection.Requests.Add(request);

        var viewModel = new RequestItemViewModel(request, this);
        Requests.Add(viewModel);

        _parent.SelectRequest(viewModel);
        await SaveCollection();
    }

    [RelayCommand]
    private async Task DeleteRequest(RequestItemViewModel? request)
    {
        if (request == null) return;

        try
        {
            var index = Requests.IndexOf(request);

            _collection.Requests.Remove(request.Request);
            Requests.Remove(request);

            request.Dispose();

            SelectAdjacentRequest(index);
            await SaveCollection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete request: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DuplicateRequest(RequestItemViewModel? request)
    {
        if (request == null) return;

        try
        {
            // Create deep copy with NEW ID
            var newRequest = request.CreateDeepCopy();
            newRequest.Name = GenerateUniqueName($"{request.Name} (Copy)");

            // Add to MODEL
            _collection.Requests.Add(newRequest);

            // Create ViewModel
            var viewModel = new RequestItemViewModel(newRequest, this);

            // Insert after original
            var index = Requests.IndexOf(request);
            Requests.Insert(index + 1, viewModel);

            // Select duplicated request
            _parent.SelectRequest(viewModel);

            // Save
            await SaveCollection();

            System.Diagnostics.Debug.WriteLine($"✅ Duplicated request:");
            System.Diagnostics.Debug.WriteLine($"   Original ID: {request.Id}");
            System.Diagnostics.Debug.WriteLine($"   New ID: {newRequest.Id}");
            System.Diagnostics.Debug.WriteLine($"   Same object? {ReferenceEquals(request.Request, newRequest)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to duplicate request: {ex.Message}");
        }
    }

    // ========================================
    // Helper Methods
    // ========================================

    private void SelectAdjacentRequest(int deletedIndex)
    {
        if (Requests.Count > 0)
        {
            var newIndex = Math.Min(deletedIndex, Requests.Count - 1);
            _parent.SelectRequest(Requests[newIndex]);
        }
        else
        {
            _parent.ClearSelection();
        }
    }

    private string GenerateUniqueName(string baseName)
    {
        if (!Requests.Any(r => r.Name == baseName))
            return baseName;

        var counter = 1;
        string name;

        do
        {
            name = $"{baseName} ({counter++})";
        } while (Requests.Any(r => r.Name == name));

        return name;
    }

    private async Task SaveCollection()
    {
        await _parent.SaveCollectionCommand.ExecuteAsync(this);
    }

    // ========================================
    // Request Reordering
    // ========================================

    [RelayCommand(CanExecute = nameof(CanMoveRequestUp))]
    private async Task MoveRequestUp(RequestItemViewModel? request)
    {
        if (request == null) return;

        try
        {
            var index = Requests.IndexOf(request);
            if (index <= 0) return;

            // Move in ViewModel
            Requests.Move(index, index - 1);

            // Move in Model
            _collection.Requests.Move(index, index - 1);

            await SaveCollection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to move request up: {ex.Message}");
        }
    }

    private bool CanMoveRequestUp(RequestItemViewModel? request)
    {
        return request != null && Requests.IndexOf(request) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveRequestDown))]
    private async Task MoveRequestDown(RequestItemViewModel? request)
    {
        if (request == null) return;

        try
        {
            var index = Requests.IndexOf(request);
            if (index < 0 || index >= Requests.Count - 1) return;

            // Move in ViewModel
            Requests.Move(index, index + 1);

            // Move in Model
            _collection.Requests.Move(index, index + 1);

            await SaveCollection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to move request down: {ex.Message}");
        }
    }

    private bool CanMoveRequestDown(RequestItemViewModel? request)
    {
        return request != null &&
               Requests.IndexOf(request) >= 0 &&
               Requests.IndexOf(request) < Requests.Count - 1;
    }

    // ========================================
    // Dispose
    // ========================================

    public void Dispose()
    {
        foreach (var request in Requests)
        {
            request.Dispose();
        }

        Requests.Clear();
    }
}