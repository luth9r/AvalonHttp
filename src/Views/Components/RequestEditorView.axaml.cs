using Avalonia.Controls;
using Avalonia.Input;

namespace AvalonHttp.Views.Components;

public partial class RequestEditorView : UserControl
{
    public RequestEditorView()
    {
        InitializeComponent();
    }
    
    private void SelectRequestTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.Tag is string tab)
        {
            if (DataContext is ViewModels.CollectionWorkspaceViewModel vm)
            {
                vm.RequestViewModel.SelectedRequestTab = tab;
            }
        }
    }

    private void SelectResponseTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.Tag is string tab)
        {
            if (DataContext is ViewModels.CollectionWorkspaceViewModel vm)
            {
                vm.RequestViewModel.SelectedResponseTab = tab;
            }
        }
    }
}
