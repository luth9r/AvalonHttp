using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.Models.CollectionAggregate;

public partial class ApiRequest : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [ObservableProperty]
    private string _name = "New Request";
    
    [ObservableProperty]
    private string _url = "";

    [JsonIgnore]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MethodString))]
    private HttpMethod _method = HttpMethod.Get;
    
    public string MethodString
    {
        get => Method.Method;
        set => Method = new HttpMethod(value);
    }
    
    [ObservableProperty]
    private string _body = "";
    
    public ObservableCollection<KeyValueData> Headers { get; set; } = new();
    
    public ObservableCollection<KeyValueData> QueryParams { get; set; } = new();

    public AuthData Auth { get; set; } = new();
}