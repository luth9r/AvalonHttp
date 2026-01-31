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
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        base.OnClosing(e);
    }
}