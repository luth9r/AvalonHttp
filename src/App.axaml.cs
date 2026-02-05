using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using AvalonHttp.Services;
using AvalonHttp.Services.Interfaces;
using Avalonia.Markup.Xaml;
using AvalonHttp.ViewModels;
using AvalonHttp.ViewModels.CollectionAggregate;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using AvalonHttp.Views;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace AvalonHttp;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        
        ConfigureServices(collection);
        
        Services = collection.BuildServiceProvider();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
            
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            desktop.Exit += OnApplicationExit;
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFileNameSanitizer, FileNameSanitizer>();
        services.AddSingleton<ICollectionRepository, FileCollectionRepository>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<IUrlParserService, UrlParserService>();
        services.AddSingleton<IEnvironmentRepository, FileEnvironmentRepository>();
        services.AddSingleton<IDirtyTrackerService, DirtyTrackerService>();
        services.AddSingleton<HeadersViewModel>();
        services.AddSingleton<AuthViewModel>();
        services.AddSingleton<QueryParamsViewModel>();
        services.AddSingleton<CookiesViewModel>();
        services.AddSingleton<EnvironmentsViewModel>();
        services.AddSingleton<DialogViewModel>();
        
        services.AddTransient<RequestViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<CollectionsViewModel>();
        services.AddTransient<CollectionWorkspaceViewModel>();
        
        services.AddTransient<MainWindow>();
    }
    
    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Dispose MainWindow DataContext
            if (desktop.MainWindow?.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}