using System;
using AvalonHttp.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvalonHttp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var assembly = typeof(JsonEditor).Assembly;
        string[] resources = assembly.GetManifestResourceNames();
        foreach (var res in resources)
        {
            System.Diagnostics.Debug.WriteLine($"Found resource: {res}");
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