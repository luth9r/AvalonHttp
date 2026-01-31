using System;
using System.ComponentModel;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest;

namespace AvalonHttp.ViewModels.CollectionAggregate;

public partial class RequestItemViewModel : ObservableObject
{
    public CollectionItemViewModel Parent { get; set; }
    
    [ObservableProperty]
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
    private bool _isDirty;

    [ObservableProperty]
    private bool _isSelected = false;
    
    public Guid Id => _originalRequest.Id;

    private readonly ApiRequest _originalRequest;

    public RequestItemViewModel(ApiRequest request, CollectionItemViewModel parent)
    {
        Parent = parent;
        _originalRequest = request;
        
        _name = request.Name;
        _url = request.Url;
        _method = request.MethodString;
        _body = request.Body;
        
        _originalRequest.PropertyChanged += OnModelPropertyChanged;
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
    
    partial void OnNameChanged(string value)
    {
        if (_originalRequest.Name != value)
        {
            _originalRequest.Name = value;
        }
    }

    [RelayCommand]
    private void Select()
    {
        Parent.Parent.SelectRequest(this);
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
    }

    [RelayCommand]
    private void Delete()
    {
        WeakReferenceMessenger.Default.Send(new ConfirmMessage(
            "Delete Request?",
            $"Are you sure you want to delete '{Name}'?",
            async () =>
            {
                await Parent.DeleteRequestCommand.ExecuteAsync(this);
            }
        ));
    }

    [RelayCommand]
    private async Task Duplicate()
    {
        await Parent.DuplicateRequestCommand.ExecuteAsync(this);
    }

    [RelayCommand]
    private void MoveToCollection(CollectionItemViewModel targetCollection)
    {
        Parent.Requests.Remove(this);
        
        var oldParent = Parent;
        Parent = targetCollection;
        
        targetCollection.Requests.Add(this);

        _ = oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent);
        _ = Parent.Parent.SaveCollectionCommand.ExecuteAsync(Parent);
    }
    
    public ApiRequest ToModel()
    {
        return _originalRequest;
    }
}