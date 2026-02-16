using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace AvalonHttp.Views.Components;

public partial class ResponseViewerView : UserControl
{
    public ResponseViewerView()
    {
        InitializeComponent();
    }

    private void SelectResponseTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.Tag is string tab)
        {
            if (DataContext is ViewModels.RequestViewModel vm)
            {
                vm.SelectedResponseTab = tab;
            }
        }
    }
}