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

/// <summary>
/// Represents a view model for an individual item in a collection, providing properties and methods
/// to manage and interact with a collection of requests.
/// </summary>
public partial class CollectionItemViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Reference to collection model
    /// </summary>
    private readonly ApiCollection _collection;
    
    /// <summary>
    /// Reference to parent view model
    /// </summary>
    private readonly CollectionsViewModel _parent;
    
    /// <summary>
    /// Stores the original name from source model.
    /// </summary>
    private string _originalName = string.Empty;

    /// <summary>
    /// Manages the disposal of subscriptions and other resources within the lifetime of the object.
    /// </summary>
    private readonly CompositeDisposable _cleanUp = new();
    
    /// <summary>
    /// Source of truth for requests.
    /// </summary>
    private readonly SourceList<RequestItemViewModel> _requestsSource = new();

    /// <summary>
    /// A read-only collection of filtered requests managed within the current collection item view model.
    /// </summary>
    private readonly ReadOnlyObservableCollection<RequestItemViewModel> _filteredRequests;

    /// <summary>
    /// Represents the collection of requests that are currently filtered and visible.
    /// This collection is a read-only observable collection derived from the original set of requests.
    /// </summary>
    public ReadOnlyObservableCollection<RequestItemViewModel> FilteredRequests => _filteredRequests;
    
    public IEnumerable<RequestItemViewModel> AllRequests => _requestsSource.Items;
    
    public CollectionsViewModel Parent => _parent;
    public ApiCollection Collection => _collection;

    /// <summary>
    /// Unique identifier for the collection.
    /// </summary>
    [ObservableProperty]
    private Guid _id;

    /// <summary>
    /// The name of the collection item, used for display and identification.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    private string _name = string.Empty;

    /// <summary>
    /// A description of the collection item.
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Indicates whether the collection item is currently expanded.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    /// Indicates whether the collection item is currently being edited.
    /// </summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>
    /// Indicates whether the collection item is currently visible.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;
    
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
                var query = _parent.SearchText;
                var hasMatches = _filteredRequests.Count > 0;
                
                if (string.IsNullOrWhiteSpace(query))
                {
                    IsVisible = true;
                    IsExpanded = true;
                }
                else
                {
                    var collectionMatches = Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                    IsVisible = collectionMatches || hasMatches;
                    
                    if (hasMatches && !collectionMatches)
                    {
                        IsExpanded = true;
                    }
                }

                MoveRequestUpCommand.NotifyCanExecuteChanged();
                MoveRequestDownCommand.NotifyCanExecuteChanged();
            })
            .DisposeWith(_cleanUp);
        
        LoadRequests();
    }
    
    /// <summary>
    /// Creates a filter predicate based on the specified query.
    /// </summary>
    /// <param name="query">The search query to filter requests by.</param>
    /// <returns>A predicate function that determines if a request item matches the search criteria.</returns>
    private Func<RequestItemViewModel, bool> CreateFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return x => true;
        }

        bool isCollectionMatch = Name.Contains(query, StringComparison.OrdinalIgnoreCase);

        return x => isCollectionMatch
                    || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads the request items associated with the current collection into the internal data source.
    /// </summary>
    private void LoadRequests()
    {
        var vms = _collection.Requests.Select(r => new RequestItemViewModel(r, this));
        _requestsSource.AddRange(vms);
    }
    
    /// <summary>
    /// Toggles the expanded state of the collection item.
    /// </summary>
    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    /// <summary>
    /// Initiates the rename process for the current collection item by storing its original name
    /// and enabling editing mode.
    /// </summary>
    [RelayCommand]
    private void StartRename()
    {
        _originalName = Name;
        IsEditing = true;
    }

    /// <summary>
    /// Completes the renaming process for the associated collection item.
    /// </summary>
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


    /// <summary>
    /// Reverts any unsaved changes made to the collection item's name and description by restoring their original values from the internal snapshot.'
    /// </summary>
    [RelayCommand]
    private void CancelRename()
    {
        Name = _originalName;
        IsEditing = false;
    }

    /// <summary>
    /// Adds a new request to the collection.
    /// </summary>
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

        // Add to model
        _collection.Requests.Add(request);
    
        var viewModel = new RequestItemViewModel(request, this);
    
        // Just add to source of truth
        _requestsSource.Add(viewModel);

        _parent.SelectRequest(viewModel);
        await SaveCollection();
    }

    /// <summary>
    /// Deletes the specified request from the collection.
    /// </summary>
    /// <param name="request"></param>
    [RelayCommand]
    private async Task DeleteRequest(RequestItemViewModel? request)
    {
        if (request == null)
        {
            return;
        }

        try
        {
            // Remove from model
            var index = _requestsSource.Items.IndexOf(request);

            // Remove from source of truth
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

    /// <summary>
    /// Duplicates the specified request and inserts the duplicate into the collection.
    /// </summary>
    /// <param name="request">The request to duplicate. If null, the operation is skipped.</param>
    /// <returns>A task that represents the asynchronous operation of duplicating and saving the request.</returns>
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

        // Create new instance to break reference with source
        var newRequest = request.CreateDeepCopy();
        newRequest.Name = _collection.GenerateUniqueRequestName($"{request.Name} (Copy)");
        
        // Add to model
        _collection.Requests.Insert(index + 1, newRequest);
        
        // Create new view model
        var viewModel = new RequestItemViewModel(newRequest, this);
        _requestsSource.Insert(index + 1, viewModel);

        // Select new request
        _parent.SelectRequest(viewModel);
        await SaveCollection();
    }
    
    /// <summary>
    /// Moves the specified request to the specified target position within the collection.
    /// </summary>
    /// <param name="request">The request to be moved.</param>
    /// <param name="target">The target request to which the request will be moved.</param>
    /// <param name="insertAfter">If true, the request will be inserted after the target; otherwise, before.</param>
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

        // Update both in sync using Edit batch
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
    
    /// <summary>
    /// Inserts the specified request into the collection at the specified target position.
    /// </summary>
    /// <param name="request">The request to be inserted.</param>
    /// <param name="target">The target request to which the request will be inserted.</param>
    /// <param name="insertAfter">If true, the request will be inserted after the target; otherwise, before.</param>
    public void InsertRequest(RequestItemViewModel request, RequestItemViewModel? target, bool insertAfter)
    {
        // Get current indices from SourceList
        var items = _requestsSource.Items.ToList();
        var insertIndex = -1;

        if (target != null)
        {
            // Calculate target position
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

        // Insert into SourceList
        _requestsSource.Insert(insertIndex, request);

        // Insert into model
        _collection.Requests.Insert(insertIndex, request.Request);
    }
    
    /// <summary>
    /// Saves the current state of the collection to the underlying model.
    /// </summary>
    private async Task SaveCollection()
    {
        await _parent.SaveCollectionCommand.ExecuteAsync(this);
    }
    
    /// <summary>
    /// Moves the specified request up in the collection.
    /// </summary>
    /// <param name="request">The request to be moved up.</param>
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

    /// <summary>
    /// Determines whether the request can be moved up in the collection.
    /// </summary>
    /// <param name="request">The request to be checked for upward movement.</param>
    /// <returns>True if the request can be moved up, otherwise false.</returns>
    private bool CanMoveRequestUp(RequestItemViewModel? request)
    {
        if (request == null)
        {
            return false;
        }

        var items = _requestsSource.Items;
        return items.Contains(request) && items.IndexOf(request) > 0;
    }

    /// <summary>
    /// Moves the specified request down in the collection.
    /// </summary>
    /// <param name="request">The request to be moved down.</param>
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

    /// <summary>
    /// Determines whether the request can be moved down in the collection.
    /// </summary>
    /// <param name="request">The request to be checked for downward movement.</param>
    /// <returns>True if the request can be moved down, otherwise false.</returns>
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
    
    /// <summary>
    /// Adds a request to the source of truth.
    /// </summary>
    /// <param name="requestVm">The request view model to be added.</param>
    public void AddRequestToSource(RequestItemViewModel requestVm)
    {
        _requestsSource.Add(requestVm);
    }
    
    /// <summary>
    /// Removes a request from the source of truth.
    /// </summary>
    /// <param name="requestVm">The request view model to be removed.</param>
    public void RemoveRequestFromSource(RequestItemViewModel requestVm)
    {
        _requestsSource.Remove(requestVm);
        _collection.Requests.Remove(requestVm.Request);
    }

    /// <summary>
    /// Releases all resources used by the current instance of the CollectionItemViewModel class.
    /// </summary>
    public void Dispose()
    {
        _cleanUp.Dispose();
        foreach (var request in _requestsSource.Items) request.Dispose();
        _requestsSource.Clear();
    }
}