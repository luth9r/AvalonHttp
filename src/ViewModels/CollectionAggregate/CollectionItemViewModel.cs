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
    
    
    private readonly ObservableCollection<RequestItemViewModel> _filteredRequests = new();
    public ObservableCollection<RequestItemViewModel> FilteredRequests => _filteredRequests;
    
    [ObservableProperty]
    private bool _isVisible = true;

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
        ResetFilter();
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

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Add to MODEL
            _collection.Requests.Add(request);

            // Create ViewModel
            var viewModel = new RequestItemViewModel(request, this);
            Requests.Add(viewModel);

            // Add to FilteredRequests
            if (string.IsNullOrWhiteSpace(_parent.SearchText))
            {
                FilteredRequests.Add(viewModel);
            }
            else
            {
                // Reapply filter
                ApplyFilter(_parent.SearchText);
            }

            // Select new request
            _parent.SelectRequest(viewModel);
        });

        await SaveCollection();
    }

    [RelayCommand]
    private async Task DeleteRequest(RequestItemViewModel? request)
    {
        if (request == null) return;

        try
        {
            var index = Requests.IndexOf(request);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Remove from MODEL
                _collection.Requests.Remove(request.Request);
            
                // Remove from VM
                Requests.Remove(request);
            
                // Remove from FilteredRequests
                FilteredRequests.Remove(request);

                // Dispose
                request.Dispose();

                // Select adjacent
                SelectAdjacentRequest(index);
            });
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
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Add to MODEL
                _collection.Requests.Insert(index + 1, newRequest);

                // Create ViewModel
                var viewModel = new RequestItemViewModel(newRequest, this);

                // Insert after original in VM
                Requests.Insert(index + 1, viewModel);

                // Update FilteredRequests
                if (string.IsNullOrWhiteSpace(_parent.SearchText))
                {
                    // No filter - add to FilteredRequests at same position
                    var filteredIndex = FilteredRequests.IndexOf(request);
                    if (filteredIndex >= 0)
                    {
                        FilteredRequests.Insert(filteredIndex + 1, viewModel);
                    }
                    else
                    {
                        FilteredRequests.Add(viewModel);
                    }
                }
                else
                {
                    // Reapply filter
                    ApplyFilter(_parent.SearchText);
                }

                // Select duplicated request
                _parent.SelectRequest(viewModel);
            });

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
    
    public void ApplyFilter(string query)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                IsVisible = true;
                ResetFilter();
                return;
            }

            var lowerQuery = query.ToLower();
            var matchedRequests = Requests.Where(r => r.Name.ToLower().Contains(lowerQuery)).ToList();
        
            FilteredRequests.Clear();
            foreach (var req in matchedRequests)
            {
                FilteredRequests.Add(req);
            }
        
            bool collectionNameMatches = Name.ToLower().Contains(lowerQuery);
            IsVisible = collectionNameMatches || FilteredRequests.Any();
        
            if (collectionNameMatches && !FilteredRequests.Any())
            {
                ResetFilter();
            }
        
            if (FilteredRequests.Any() && !collectionNameMatches)
            {
                IsExpanded = true;
            }
        });
    }
    
    private void ResetFilter()
    {
        FilteredRequests.Clear();
        foreach (var req in Requests)
        {
            FilteredRequests.Add(req);
        }
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