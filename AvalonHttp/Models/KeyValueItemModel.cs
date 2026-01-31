using CommunityToolkit.Mvvm.ComponentModel;

namespace AvalonHttp.Models;

public partial class KeyValueItemModel : ObservableObject
{
    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _value = "";

    [ObservableProperty]
    private bool _isEnabled = true;
}