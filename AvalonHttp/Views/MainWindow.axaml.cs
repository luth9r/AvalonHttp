using System;
using Avalonia.Controls;

namespace AvalonHttp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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