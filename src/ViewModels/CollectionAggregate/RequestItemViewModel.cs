using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

namespace AvalonHttp.ViewModels.CollectionAggregate;

public partial class RequestItemViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Reference to parent view model
    /// </summary>
    private readonly CollectionItemViewModel _parent;
    
    /// <summary>
    /// Reference to source model
    /// </summary>
    private readonly ApiRequest _originalRequest;
    
    /// <summary>
    /// Stores the original name from source model.
    /// </summary>
    private string _originalName = string.Empty;
    
    public ApiRequest Request => _originalRequest;
    
    public CollectionItemViewModel Parent => _parent;
    
    public Guid Id => _originalRequest.Id;
    
    /// <summary>
    /// The name of the request item, used for display and identification.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    private string _name = string.Empty;

    /// <summary>
    /// The URL of the request item.
    /// </summary>
    [ObservableProperty]
    private string _url = string.Empty;

    /// <summary>
    /// The HTTP method of the request item.
    /// </summary>
    [ObservableProperty]
    private string _method = string.Empty;

    /// <summary>
    /// Indicates whether the request item is currently being edited.
    /// </summary>
    [ObservableProperty]
    private bool _isEditing;
    
    /// <summary>
    /// Indicates whether the request item has been modified since last save.
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>
    /// Indicates whether the request item is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
    
    public RequestItemViewModel(ApiRequest request, CollectionItemViewModel parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _originalRequest = request ?? throw new ArgumentNullException(nameof(request));
        
        _originalRequest.PropertyChanged += OnModelPropertyChanged;
        
        // Load data from model
        LoadFromModel();
    }

    /// <summary>
    /// Populates the properties of the view model with values from the associated model.
    /// This method synchronizes the Name, Url, and HTTP method from the underlying ApiRequest instance,
    /// ensuring that the view model reflects the current state of the model.
    /// </summary>
    private void LoadFromModel()
    {
        Name = _originalRequest.Name;
        Url = _originalRequest.Url;
        Method = _originalRequest.MethodString;
    }

    /// <summary>
    /// Selects the current request item.
    /// </summary>
    [RelayCommand]
    private void Select()
    {
        _parent.Parent.SelectRequest(this);
    }

    /// <summary>
    /// Starts the renaming operation for the associated request item.
    /// </summary>
    [RelayCommand]
    private void StartRename()
    {
        _originalName = Name;
        IsEditing = true;
    }

    /// <summary>
    /// Completes the renaming process for the associated request item.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanFinishRename))]
    private async Task FinishRename()
    {
        if (!CanFinishRename())
        {
            CancelRename();
            return;
        }

        // Save changes to model
        ApplyToModel();
        IsEditing = false;
    }

    /// <summary>
    /// Determines whether the renaming process for the associated request item can be completed.
    /// </summary>
    private bool CanFinishRename()
    {
        return !string.IsNullOrWhiteSpace(Name);
    }

    /// <summary>
    /// Cancels the renaming process for the associated request item.
    /// </summary>
    [RelayCommand]
    private void CancelRename()
    {
        Name = _originalName;
        IsEditing = false;
    }

    /// <summary>
    /// Deletes the current request item from the parent view model.
    /// </summary>
    [RelayCommand]
    private void Delete()
    {
        WeakReferenceMessenger.Default.Send(new ConfirmMessage(
            title: "Delete Request?",
            message: $"Are you sure you want to delete '{Name}'?",
            onConfirm: async () =>
            {
                try
                {
                    await _parent.DeleteRequestCommand.ExecuteAsync(this);
                }
                catch (Exception ex)
                {
                    WeakReferenceMessenger.Default.Send(new ErrorMessage(
                        "Failed to Delete Request",
                        $"An error occurred: {ex.Message}"
                    ));
                }
            },
            confirmButtonText: "Delete",
            onCancel: () => Task.CompletedTask
        ));
    }

    /// <summary>
    /// Duplicates the current request item and adds it to the parent view model.
    /// </summary>
    [RelayCommand]
    private async Task Duplicate()
    {
        try
        {
            await _parent.DuplicateRequestCommand.ExecuteAsync(this);
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new ErrorMessage(
                "Failed to Duplicate Request",
                $"An error occurred: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Moves the current request item to the specified collection.
    /// </summary>
    /// <param name="targetCollection">The collection to which the request item should be moved.</param>
    [RelayCommand]
    private async Task MoveToCollection(CollectionItemViewModel? targetCollection)
    {
        if (targetCollection == null || targetCollection == _parent)
        {
            return;
        }

        try
        {
            // Remove from old parent
            var oldParent = _parent;
            
            // Save changes to model
            ApplyToModel();
            
            // Move request to new parent
            oldParent.Collection.Requests.Remove(_originalRequest);
            targetCollection.Collection.Requests.Add(_originalRequest);

            // Update view model references
            oldParent.RemoveRequestFromSource(this);
            
            // Create new view model
            var movedVm = new RequestItemViewModel(_originalRequest, targetCollection);
            targetCollection.AddRequestToSource(movedVm);

            // Select new request
            targetCollection.Parent.SelectRequest(movedVm);
            
            await Task.WhenAll(
                oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent),
                targetCollection.Parent.SaveCollectionCommand.ExecuteAsync(targetCollection)
            );
            
            Dispose();
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new ErrorMessage("Failed to Move Request", ex.Message));
        }
    }

    /// <summary>
    /// Updates the associated model with the current state of the view model.
    /// This method ensures that the model's properties, such as name, URL,
    /// HTTP method, and updated timestamp, are synchronized with the corresponding
    /// properties of the view model.
    /// </summary>
    public void ApplyToModel()
    {
        _originalRequest.Name = Name;
        _originalRequest.Url = Url;
        _originalRequest.MethodString = Method;
        _originalRequest.UpdatedAt = DateTime.Now;
    }
    
    /// <summary>
    /// Create a deep copy of the request (for duplication)
    /// </summary>
    public ApiRequest CreateDeepCopy()
    {
        // Sync current VM state to model first
        ApplyToModel();
        
        return new ApiRequest
        {
            // NEW ID for duplicate
            Id = Guid.NewGuid(),
            
            // Basic properties
            Name = _originalRequest.Name,
            Url = _originalRequest.Url,
            MethodString = _originalRequest.MethodString,
            Body = _originalRequest.Body,
            
            // Timestamps
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        
            // Deep copy Headers
            Headers = new ObservableCollection<KeyValueItemModel>(
                _originalRequest.Headers.Select(h => new KeyValueItemModel
                {
                    Key = h.Key,
                    Value = h.Value,
                    IsEnabled = h.IsEnabled
                })),
        
            // Deep copy QueryParameters
            QueryParameters = new ObservableCollection<KeyValueItemModel>(
                _originalRequest.QueryParameters.Select(p => new KeyValueItemModel
                {
                    Key = p.Key,
                    Value = p.Value,
                    IsEnabled = p.IsEnabled
                })),
        
            // Deep copy Cookies
            Cookies = new ObservableCollection<KeyValueItemModel>(
                _originalRequest.Cookies.Select(c => new KeyValueItemModel
                {
                    Key = c.Key,
                    Value = c.Value,
                    IsEnabled = c.IsEnabled
                })),
        
            // Deep copy AuthData
            AuthData = _originalRequest.AuthData != null ? new AuthData
            {
                Type = _originalRequest.AuthData.Type,
                BasicUsername = _originalRequest.AuthData.BasicUsername,
                BasicPassword = _originalRequest.AuthData.BasicPassword,
                BearerToken = _originalRequest.AuthData.BearerToken,
                ApiKeyName = _originalRequest.AuthData.ApiKeyName,
                ApiKeyValue = _originalRequest.AuthData.ApiKeyValue,
                ApiKeyLocation = _originalRequest.AuthData.ApiKeyLocation
            } : new AuthData()
        };
    }

    /// <summary>
    /// Update ViewModel from Model (when model changes externally)
    /// </summary>
    /// <remarks>Used to synchronize sidebar with changes made to the model.</remarks>
    public void UpdateFromModel(ApiRequest request)
    {
        if (request == null || request.Id != Id)
        {
            return;
        }

        LoadFromModel();
        IsDirty = false;
    }

    /// <summary>
    /// Handles property change notifications from the underlying model and updates
    /// the corresponding properties in the view model to reflect the changes in real time.
    /// </summary>
    /// <param name="sender">The source object that raised the PropertyChanged event.</param>
    /// <param name="e">Provides data about the property that changed, including its name.</param>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApiRequest.Name))
        {
            Name = _originalRequest.Name;
        }
        else if (e.PropertyName == nameof(ApiRequest.MethodString))
        {
            Method = _originalRequest.MethodString;
        }
    }
    
    public void Dispose()
    {
        _originalRequest.PropertyChanged -= OnModelPropertyChanged;
    }
}
