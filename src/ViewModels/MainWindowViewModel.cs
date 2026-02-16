using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using AvalonHttp.Services.Interfaces;
using AvalonHttp.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AvalonHttp.ViewModels;

/// <summary>
/// Represents the view model for the main window.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Child view models for managing collections and environments.
    /// </summary>
    public CollectionWorkspaceViewModel CollectionsWorkspace { get; }
    
    /// <summary>
    /// View model responsible for managing environments.
    /// </summary>
    public EnvironmentsViewModel EnvironmentsViewModel { get; }
    
    /// <summary>
    /// View model responsible for displaying dialogs.
    /// </summary>
    public DialogViewModel DialogViewModel { get; }

    /// <summary>
    /// Indicates the current view.
    /// </summary>

    
    /// <summary>
    /// Indicates whether the current view is the collections view.
    /// </summary>
    public bool IsCollectionsView => CurrentView == "Collections";
    
    /// <summary>
    /// Indicates whether the current view is the environments view.
    /// </summary>
    public bool IsEnvironmentsView => CurrentView == "Environments";
    
    /// <summary>
    /// Indicates whether the current view is the settings view.
    /// </summary>
    public bool IsSettingsView => CurrentView == "Settings";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionsView))]
    [NotifyPropertyChangedFor(nameof(IsEnvironmentsView))]
    [NotifyPropertyChangedFor(nameof(IsSettingsView))]
    private string _currentView = "Collections";

    public SettingsViewModel SettingsViewModel { get; }
    
    private readonly ILanguageService _languageService;

    [ObservableProperty]
    private string _welcomeMessage = "Loading...";

    public MainWindowViewModel(
        CollectionWorkspaceViewModel collectionsWorkspace,
        EnvironmentsViewModel environmentsViewModel,
        DialogViewModel dialogViewModel,
        ILanguageService languageService,
        SettingsViewModel settingsViewModel)
    {
        CollectionsWorkspace = collectionsWorkspace ?? 
            throw new ArgumentNullException(nameof(collectionsWorkspace));
        EnvironmentsViewModel = environmentsViewModel ?? 
            throw new ArgumentNullException(nameof(environmentsViewModel));
        DialogViewModel = dialogViewModel ?? 
            throw new ArgumentNullException(nameof(dialogViewModel));
        _languageService = languageService ??
                           throw new ArgumentNullException(nameof(languageService));
        SettingsViewModel = settingsViewModel ??
                            throw new ArgumentNullException(nameof(settingsViewModel));

        // Initial load
        UpdateWelcomeMessage();
    }
    
    /// <summary>
    /// Initializes the view model by loading collections and environments.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await CollectionsWorkspace.InitializeAsync();
            await EnvironmentsViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Switches to the collections view.
    /// </summary>
    [RelayCommand]
    private void ShowCollections()
    {
        if (CurrentView == "Collections")
        {
            return;
        }

        CloseAllEdits();
        CurrentView = "Collections";
    }

    /// <summary>
    /// Switches to the environments view.
    /// </summary>
    [RelayCommand]
    private void ShowEnvironments()
    {
        if (CurrentView == "Environments")
        {
            return;
        }

        CloseAllEdits();
        CurrentView = "Environments";
    }

    /// <summary>
    /// Switches to the settings view.
    /// </summary>
    [RelayCommand]
    private void ShowSettings()
    {
        if (CurrentView == "Settings")
        {
            return;
        }

        CloseAllEdits();
        CurrentView = "Settings";
    }
    
    
    [RelayCommand]
    private async Task ToggleLanguageAsync()
    {
        var nextLang = _languageService.CurrentCulture.Name == "en" ? "ua" : "en";
        await _languageService.ChangeLanguageAsync(nextLang);
        UpdateWelcomeMessage();
    }

    private void UpdateWelcomeMessage()
    {
        WelcomeMessage = Loc.Tr("WelcomeMessage");
    }

    /// <summary>
    /// Closes all open edits across collections and environments.
    /// </summary>
    private void CloseAllEdits()
    {
        try
        {
            // Close collection edits
            foreach (var collection in CollectionsWorkspace.CollectionsViewModel.Collections)
            {
                if (collection.IsEditing)
                {
                    collection.CancelRenameCommand.Execute(null);
                }
                
                foreach (var request in collection.AllRequests)
                {
                    if (request.IsEditing)
                    {
                        request.CancelRenameCommand.Execute(null);
                    }
                }
            }
            
            // Close environment edits
            foreach (var environment in EnvironmentsViewModel.Environments)
            {
                if (environment.IsEditing)
                {
                    environment.CancelRenameCommand.Execute(null);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to close edits: {ex.Message}");
        }
    }


    /// <summary>
    /// Attempts to exit the application with a confirmation dialog.
    /// </summary>
    /// <param name="onConfirmed">Action to perform when the user confirms exit.</param>
    public void AttemptExitWithCallback(Action onConfirmed)
    {
        bool hasUnsavedChanges = CollectionsWorkspace.RequestViewModel.IsDirty || 
                                 EnvironmentsViewModel.HasUnsavedChanges;

        if (hasUnsavedChanges)
        {
            var message = GetUnsavedChangesMessage();
            
            WeakReferenceMessenger.Default.Send(
                DialogMessage.Destructive(
                    title: "Unsaved Changes",
                    message: message,
                    confirmText: "Cancel",
                    cancelText: "Exit Without Saving",
                    onConfirm: () => Task.CompletedTask,
                    onCancel: () =>
                    {
                        onConfirmed?.Invoke();
                        return Task.CompletedTask;
                    }
                )
            );

        }
        else
        {
            onConfirmed?.Invoke();
        }
    }

    /// <summary>
    /// Returns a message indicating whether the user has unsaved changes.
    /// </summary>
    private string GetUnsavedChangesMessage()
    {
        bool requestDirty = CollectionsWorkspace.RequestViewModel.IsDirty;
        bool envDirty = EnvironmentsViewModel.HasUnsavedChanges;
        
        if (requestDirty && envDirty)
        {
            return "You have unsaved changes in requests and environments.";
        }
        if (requestDirty)
        {
            return "You have unsaved changes in the current request.";
        }
        if (envDirty)
        {
            return "You have unsaved changes in environments.";
        }
        
        return "You have unsaved changes.";
    }

    /// <summary>
    /// Releases all resources used by the MainWindowViewModel instance and its disposable members.
    /// Unregisters the instance from all message subscriptions and disposes of the associated
    /// CollectionWorkspaceViewModel and EnvironmentsViewModel instances if they implement IDisposable.
    /// </summary>
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        
        (CollectionsWorkspace as IDisposable)?.Dispose();
        (EnvironmentsViewModel as IDisposable)?.Dispose();
    }
}
