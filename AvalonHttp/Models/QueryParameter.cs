using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.Models;

public partial class QueryParameter : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _value = "";
}  
