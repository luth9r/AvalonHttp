using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace AvalonHttp.Views.Components;

public partial class RequestTabsView : UserControl
{
    public RequestTabsView()
    {
        InitializeComponent();
    }

    private void SelectRequestTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.Tag is string tab)
        {
            if (DataContext is ViewModels.RequestViewModel vm)
            {
                vm.SelectedRequestTab = tab;
            }
        }
    }
}