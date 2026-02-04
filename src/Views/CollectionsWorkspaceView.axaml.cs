using AvalonHttp.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace AvalonHttp.Views;

public partial class CollectionsWorkspaceView : UserControl
{
    public CollectionsWorkspaceView()
    {
        InitializeComponent();
    }

    private void SelectRequestTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && 
            border.Tag is string tabName && 
            DataContext is CollectionWorkspaceViewModel vm)
        {
            vm.RequestViewModel.SelectRequestTabCommand.Execute(tabName);
        }
    }
    
    private void SelectResponseTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && 
            border.Tag is string tabName && 
            DataContext is CollectionWorkspaceViewModel vm)
        {
            vm.RequestViewModel.SelectResponseTabCommand.Execute(tabName);
        }
    }
}