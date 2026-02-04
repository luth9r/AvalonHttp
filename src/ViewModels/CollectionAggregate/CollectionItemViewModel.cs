using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace AvalonHttp.ViewModels.CollectionAggregate;

public partial class CollectionItemViewModel : ObservableObject, IDisposable
{
    // ========================================
    // Fields
    // ========================================

    private readonly ApiCollection _collection;
    private readonly CollectionsViewModel _parent;
    private string _originalName = string.Empty;
    private readonly CompositeDisposable _cleanUp = new();
    
    // Source of truth for requests
    private readonly SourceList<RequestItemViewModel> _requestsSource = new();
    
    // UI binding for filtered requests
    private readonly ReadOnlyObservableCollection<RequestItemViewModel> _filteredRequests;
    public ReadOnlyObservableCollection<RequestItemViewModel> FilteredRequests => _filteredRequests;
    
    public IEnumerable<RequestItemViewModel> AllRequests => _requestsSource.Items;

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
        
       _requestsSource.Connect()

            .Filter(_parent.WhenAnyValue(x => x.SearchText)
                .Select(CreateFilter)) 
            .Sort(SortExpressionComparer<RequestItemViewModel>
                .Ascending(vm => _requestsSource.Items.IndexOf(vm)))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Bind(out _filteredRequests)
            .Subscribe(_ =>
            {
                var hasMatches = _filteredRequests.Count > 0;
                var query = _parent.SearchText;
                
                IsVisible = string.IsNullOrWhiteSpace(query) || 
                            Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                            hasMatches;

                if (!string.IsNullOrWhiteSpace(query) && hasMatches)
                {
                    IsExpanded = true;
                }

                MoveRequestUpCommand.NotifyCanExecuteChanged();
                MoveRequestDownCommand.NotifyCanExecuteChanged();
            })
            .DisposeWith(_cleanUp);
        
        LoadRequests();
    }
    
    private Func<RequestItemViewModel, bool> CreateFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return x => true;
        }

        var lowerQuery = query.ToLower();

        bool isCollectionMatch = Name.Contains(query, StringComparison.OrdinalIgnoreCase);

        return x => isCollectionMatch || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadRequests()
    {
        var vms = _collection.Requests.Select(r => new RequestItemViewModel(r, this));
        _requestsSource.AddRange(vms);
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
            Name = _collection.GenerateUniqueRequestName("New Request"),
            Url = "https://api.example.com",
            MethodString = "GET",
        };

        _collection.Requests.Add(request);
    
        var viewModel = new RequestItemViewModel(request, this);
    
        // Just add to source of truth
        _requestsSource.Add(viewModel);

        _parent.SelectRequest(viewModel);
        await SaveCollection();
    }

    [RelayCommand]
    private async Task DeleteRequest(RequestItemViewModel? request)
    {
        if (request == null)
        {
            return;
        }

        try
        {
            var index = _requestsSource.Items.IndexOf(request);

            _collection.Requests.Remove(request.Request);
            _requestsSource.Remove(request);
        
            request.Dispose();

            if (_requestsSource.Count > 0)
            {
                var newIndex = Math.Min(index, _requestsSource.Count - 1);
                _parent.SelectRequest(_requestsSource.Items.ElementAt(newIndex));
            }
            else
            {
                _parent.ClearSelection();
            }
        
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
        if (request == null)
        {
            return;
        }

        var index = _requestsSource.Items.ToList().IndexOf(request);
        if (index == -1)
        {
            return;
        }

        var newRequest = request.CreateDeepCopy();
        newRequest.Name = _collection.GenerateUniqueRequestName($"{request.Name} (Copy)");
        
        _collection.Requests.Insert(index + 1, newRequest);
        
        var viewModel = new RequestItemViewModel(newRequest, this);
        _requestsSource.Insert(index + 1, viewModel);

        _parent.SelectRequest(viewModel);
        await SaveCollection();
    }
    
    public void MoveRequest(RequestItemViewModel request, RequestItemViewModel target, bool insertAfter)
    {
        // Get current indices from SourceList
        var items = _requestsSource.Items.ToList();
        var oldIndex = items.IndexOf(request);
        var targetIndex = items.IndexOf(target);

        if (oldIndex < 0 || targetIndex < 0)
        {
            return;
        }

        // Calculate target position
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

        // ✅ Update both in sync using Edit batch
        _requestsSource.Edit(innerList =>
        {
            // Move in SourceList
            innerList.RemoveAt(oldIndex);
            innerList.Insert(targetIndex, request);
        
            // Move in model to match
            _collection.Requests.Move(oldIndex, targetIndex);
        });

        // Save asynchronously
        _ = Task.Run(async () => await Parent.SaveCollectionCommand.ExecuteAsync(this));
    }
    
    public void InsertRequest(RequestItemViewModel request, RequestItemViewModel? target, bool insertAfter)
    {
        var items = _requestsSource.Items.ToList();
        var insertIndex = -1;

        if (target != null)
        {
            var targetIndex = items.IndexOf(target);
            if (targetIndex >= 0)
            {
                insertIndex = insertAfter ? targetIndex + 1 : targetIndex;
            }
        }

        if (insertIndex == -1 || insertIndex > items.Count)
        {
            insertIndex = items.Count;
        }

        _requestsSource.Insert(insertIndex, request);

        _collection.Requests.Insert(insertIndex, request.Request);
    }
    
    

    // ========================================
    // Helper Methods
    // ========================================
    

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
        if (request == null)
        {
            return;
        }

        var index = _requestsSource.Items.ToList().IndexOf(request);
        if (index <= 0)
        {
            return;
        }

        _requestsSource.Move(index, index - 1);
        _collection.Requests.Move(index, index - 1);

        await SaveCollection();
    }

    private bool CanMoveRequestUp(RequestItemViewModel? request)
    {
        if (request == null)
        {
            return false;
        }

        var items = _requestsSource.Items;
        return items.Contains(request) && items.IndexOf(request) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveRequestDown))]
    private async Task MoveRequestDown(RequestItemViewModel? request)
    {
        if (request == null)
        {
            return;
        }

        var items = _requestsSource.Items.ToList();
        var index = items.IndexOf(request);
        if (index < 0 || index >= items.Count - 1)
        {
            return;
        }

        _requestsSource.Move(index, index + 1);
        _collection.Requests.Move(index, index + 1);

        await SaveCollection();
    }

    private bool CanMoveRequestDown(RequestItemViewModel? request)
    {
        if (request == null)
        {
            return false;
        }

        var items = _requestsSource.Items;
        var index = items.IndexOf(request);
        return index >= 0 && index < items.Count() - 1;
    }
    
    public void AddRequestToSource(RequestItemViewModel requestVm)
    {
        _requestsSource.Add(requestVm);
    }
    
    public void RemoveRequestFromSource(RequestItemViewModel requestVm)
    {
        _requestsSource.Remove(requestVm);
        _collection.Requests.Remove(requestVm.Request);
    }

    // ========================================
    // Dispose
    // ========================================

    public void Dispose()
    {
        _cleanUp.Dispose();
        foreach (var request in _requestsSource.Items) request.Dispose();
        _requestsSource.Clear();
    }
}