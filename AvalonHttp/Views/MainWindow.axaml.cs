using System;
using AvalonHttp.Controls;
using AvalonHttp.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AvalonHttp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Focusable = true;
    }
    
    private void SelectRequestTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tabName && DataContext is MainWindowViewModel vm)
        {
            vm.SelectRequestTabCommand.Execute(tabName);
        }
    }
    
    private void SelectResponseTab(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tabName && DataContext is MainWindowViewModel vm)
        {
            vm.SelectResponseTabCommand.Execute(tabName);
        }
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.RequestViewModel.IsDirty)
            {
                e.Cancel = true;
                vm.AttemptExitCommand.Execute(null);
            }
        }
        base.OnClosing(e);
    }
}