using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.Models.CollectionAggregate;

public partial class ApiRequest : ObservableObject
{
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "New Request";

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private HttpMethod _method = HttpMethod.Get;

    [ObservableProperty]
    private string _body = "";

    [ObservableProperty]
    private ObservableCollection<KeyValueItemModel> _headers = new();

    [ObservableProperty]
    private ObservableCollection<KeyValueItemModel> _queryParameters = new();
    
    [ObservableProperty]
    private ObservableCollection<KeyValueItemModel> _cookies = new();

    [ObservableProperty]
    private AuthData _authData = new();

    public string MethodString
    {
        get => Method.Method;
        set => Method = new HttpMethod(value);
    }
}