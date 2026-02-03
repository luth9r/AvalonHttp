using AvalonHttp.ViewModels.EnvironmentAggregate;
using Avalonia.Controls;
using Avalonia.Input;

namespace AvalonHttp.Views;

public partial class EnvironmentsView : UserControl
{
    public EnvironmentsView()
    {
        InitializeComponent();
    }
    
    private async void OnSidebarClicked(object? sender, PointerPressedEventArgs e)
    {
        // Don't close if clicking directly on a TextBox (to allow editing)
        if (e.Source is TextBox)
            return;
            
        if (DataContext is EnvironmentsViewModel vm)
        {
            await vm.CloseAllEditModesCommand.ExecuteAsync(null);
        }
    }
}