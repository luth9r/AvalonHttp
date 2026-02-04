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
    // ========================================
    // Fields
    // ========================================
    
    private readonly CollectionItemViewModel _parent;
    
    private readonly ApiRequest _originalRequest;
    
    private string _originalName = string.Empty;
    
    // ========================================
    // Properties
    // ========================================
    
    public ApiRequest Request => _originalRequest;
    
    public CollectionItemViewModel Parent => _parent;
    
    public Guid Id => _originalRequest.Id;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FinishRenameCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _method = string.Empty;

    [ObservableProperty]
    private bool _isEditing;
    
    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isSelected;

    // ========================================
    // Constructor
    // ========================================
    
    public RequestItemViewModel(ApiRequest request, CollectionItemViewModel parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _originalRequest = request ?? throw new ArgumentNullException(nameof(request));
        
        _originalRequest.PropertyChanged += OnModelPropertyChanged;
        
        // Load data from model
        LoadFromModel();
    }

    // ========================================
    // Load from Model
    // ========================================
    
    private void LoadFromModel()
    {
        Name = _originalRequest.Name;
        Url = _originalRequest.Url;
        Method = _originalRequest.MethodString;
    }
    
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

    // ========================================
    // Commands
    // ========================================
    
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
            CancelRename();
            return;
        }

        IsEditing = false;
        
        // Only save if name changed
        if (Name == _originalName)
            return;
        
        SyncToModel();
        await _parent.Parent.SaveCollectionCommand.ExecuteAsync(_parent);
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
                    WeakReferenceMessenger.Default.Send(new ErrorMessage(
                        "Failed to Delete Request",
                        $"An error occurred: {ex.Message}"
                    ));
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
            WeakReferenceMessenger.Default.Send(new ErrorMessage(
                "Failed to Duplicate Request",
                $"An error occurred: {ex.Message}"
            ));
        }
    }

    [RelayCommand]
    private async Task MoveToCollection(CollectionItemViewModel? targetCollection)
    {
        if (targetCollection == null || targetCollection == _parent)
            return;

        try
        {
            var oldParent = _parent;
            
            SyncToModel();
            
            oldParent.Collection.Requests.Remove(_originalRequest);
            targetCollection.Collection.Requests.Add(_originalRequest);

            oldParent.RemoveRequestFromSource(this);
            
            var movedVm = new RequestItemViewModel(_originalRequest, targetCollection);
            targetCollection.AddRequestToSource(movedVm);

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

    // ========================================
    // Model Synchronization
    // ========================================
    
    /// <summary>
    /// Sync ViewModel properties to Model
    /// </summary>
    public void SyncToModel()
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
        SyncToModel();
        
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
    public void UpdateFromModel(ApiRequest request)
    {
        if (request == null || request.Id != Id)
            return;
        LoadFromModel();
        IsDirty = false;
    }
    

    // ========================================
    // Dispose
    // ========================================
    
    public void Dispose()
    {
        _originalRequest.PropertyChanged -= OnModelPropertyChanged;
    }
}
