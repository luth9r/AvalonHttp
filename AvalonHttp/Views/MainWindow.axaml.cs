using System;
using AvalonHttp.Controls;
using AvalonHttp.ViewModels;
using AvalonHttp.ViewModels.CollectionAggregate;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AvalonHttp.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel  _viewModel;
    
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        Focusable = true;
        
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        
        DataContext = _viewModel;
        
        Loaded += OnWindowLoaded;
    }
    
    private async void OnWindowLoaded(object? sender, EventArgs e)
    {
        await _viewModel.CollectionsViewModel.InitializeAsync();
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