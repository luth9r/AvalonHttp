using System;
using AvalonHttp.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AvalonHttp.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _isShuttingDown = false;
    
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        Loaded += OnWindowLoaded;
        this.PropertyChanged += MainWindow_PropertyChanged;
    }
    
    private void TopLevel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
    
    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void MainWindow_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            var isMaximized = WindowState == WindowState.Maximized;
                
            // Обращаемся к Path по их именам (x:Name в XAML)
            MaximizePath.IsVisible = !isMaximized;
            RestorePath.IsVisible = isMaximized;
        }
    }
    
    private async void OnWindowLoaded(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isShuttingDown)
        {
            base.OnClosing(e);
            return;
        }
        
        if (DataContext is MainWindowViewModel vm)
        {
            bool isCollectionDirty = vm.CollectionsWorkspace.RequestViewModel.IsDirty;
            bool isEnvDirty = vm.EnvironmentsViewModel.HasUnsavedChanges;
            
            if (isCollectionDirty || isEnvDirty) 
            {
                e.Cancel = true;
                
                _viewModel.AttemptExitWithCallback(() => 
                {
                    _isShuttingDown = true;
                    Close();
                });
            }
        }
        
        base.OnClosing(e);
    }
}