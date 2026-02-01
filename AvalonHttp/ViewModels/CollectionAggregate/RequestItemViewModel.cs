using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

namespace AvalonHttp.ViewModels.CollectionAggregate;

public partial class RequestItemViewModel : ObservableObject, IDisposable
{
    private readonly CollectionItemViewModel _parent;
    private readonly ApiRequest _originalRequest;
    private string _originalName = string.Empty;
    
    public ApiRequest Request => _originalRequest;
    
    public CollectionItemViewModel Parent => _parent;
    public Guid Id { get; }
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    private string _name;

    [ObservableProperty]
    private string _url;

    [ObservableProperty]
    private string _method;

    [ObservableProperty]
    private string _body;

    [ObservableProperty]
    private bool _isEditing = false;
    
    [ObservableProperty]
    private bool _isDirty = false;

    [ObservableProperty]
    private bool _isSelected = false;

    public RequestItemViewModel(ApiRequest request, CollectionItemViewModel parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _originalRequest = request ?? throw new ArgumentNullException(nameof(request));
        
        System.Diagnostics.Debug.WriteLine($"🔵 RequestItemViewModel constructor:");
        System.Diagnostics.Debug.WriteLine($"  Request ID: {request.Id}");
        System.Diagnostics.Debug.WriteLine($"  Request hash: {request.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"  Request.Body: '{request.Body}'");
        System.Diagnostics.Debug.WriteLine($"  _originalRequest hash: {_originalRequest.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"  Same? {ReferenceEquals(request, _originalRequest)}");
        
        Id = request.Id;
        _name = request.Name;
        _url = request.Url;
        _method = request.MethodString;
        _body = request.Body ?? string.Empty;
    }

    [RelayCommand]
    private void Select()
    {
        _parent.Parent.SelectRequest(this);
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
            Name = _originalName;
            IsEditing = false;
            return;
        }

        IsEditing = false;
        
        try
        {
            UpdateModel();
            await _parent.Parent.SaveCollectionCommand.ExecuteAsync(_parent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to finish rename: {ex.Message}");
            Name = _originalName;
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
    private void Delete()
    {
        WeakReferenceMessenger.Default.Send(new ConfirmMessage(
            "Delete Request?",
            $"Are you sure you want to delete '{Name}'?",
            async () =>
            {
                try
                {
                    await _parent.DeleteRequestCommand.ExecuteAsync(this);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete request: {ex.Message}");
                }
            }
        ));
    }

    [RelayCommand]
    private async Task Duplicate()
    {
        try
        {
            await _parent.DuplicateRequestCommand.ExecuteAsync(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to duplicate request: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task MoveToCollection(CollectionItemViewModel targetCollection)
    {
        if (targetCollection == null || targetCollection == _parent)
            return;

        try
        {
            var oldParent = _parent;
            
            // Create new ViewModel in target collection
            var movedRequest = new RequestItemViewModel(this.ToModel(), targetCollection);
            
            // Remove from old, add to new
            oldParent.Requests.Remove(this);
            targetCollection.Requests.Add(movedRequest);

            // Select the moved request
            targetCollection.Parent.SelectRequest(movedRequest);

            // Save both collections in parallel
            await Task.WhenAll(
                oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent),
                targetCollection.Parent.SaveCollectionCommand.ExecuteAsync(targetCollection)
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to move request: {ex.Message}");
            
            // WeakReferenceMessenger.Default.Send(new ErrorMessage(
            //     "Failed to Move Request",
            //     $"An error occurred: {ex.Message}"
            // ));
        }
    }
    
    private void UpdateModel()
    {
        _originalRequest.Name = Name;
        _originalRequest.Url = Url;
        _originalRequest.MethodString = Method;
        _originalRequest.Body = Body;
    }
    
    public ApiRequest ToModel()
    {
        UpdateModel();
        
        // Return a copy to avoid external mutations
        return new ApiRequest
        {
            Id = _originalRequest.Id,
            Name = _originalRequest.Name,
            Url = _originalRequest.Url,
            MethodString = _originalRequest.MethodString,
            Body = _originalRequest.Body,
            Headers = _originalRequest.Headers,
            QueryParameters = _originalRequest.QueryParameters
        };
    }

    public void UpdateFromModel(ApiRequest request)
    {
        if (request == null || request.Id != Id)
            return;

        Name = request.Name;
        Url = request.Url;
        Method = request.MethodString;
        Body = request.Body ?? string.Empty;
        
        IsDirty = false;
    }

    public void Dispose()
    {
        // No subscriptions to clean up anymore
    }
}
