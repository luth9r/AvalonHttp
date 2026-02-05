using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AvalonHttp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    // ========================================
    // Child ViewModels
    // ========================================
    
    public CollectionWorkspaceViewModel CollectionsWorkspace { get; }
    public EnvironmentsViewModel EnvironmentsViewModel { get; }
    
    public DialogViewModel DialogViewModel { get; }

    // ========================================
    // Navigation
    // ========================================
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollectionsView))]
    [NotifyPropertyChangedFor(nameof(IsEnvironmentsView))]
    private string _currentView = "Collections";
    
    public bool IsCollectionsView => CurrentView == "Collections";
    public bool IsEnvironmentsView => CurrentView == "Environments";
    // ========================================
    // Constructor
    // ========================================
    
    public MainWindowViewModel(
        CollectionWorkspaceViewModel collectionsWorkspace,
        EnvironmentsViewModel environmentsViewModel,
        DialogViewModel dialogViewModel)
    {
        CollectionsWorkspace = collectionsWorkspace ?? 
            throw new ArgumentNullException(nameof(collectionsWorkspace));
        EnvironmentsViewModel = environmentsViewModel ?? 
            throw new ArgumentNullException(nameof(environmentsViewModel));
        DialogViewModel = dialogViewModel ?? 
            throw new ArgumentNullException(nameof(dialogViewModel));
    }
    
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
    
    [RelayCommand]
    private async Task ShowCollections()
    {
        if (CurrentView == "Collections")
        {
            return;
        }

        await CloseAllEdits();
        CurrentView = "Collections";
    }

    [RelayCommand]
    private async Task ShowEnvironments()
    {
        if (CurrentView == "Environments")
        {
            return;
        }

        await CloseAllEdits();
        CurrentView = "Environments";
    }
    
    private async Task CloseAllEdits()
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

    // ========================================
    // Application Exit
    // ========================================
    
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

    // ========================================
    // Dispose
    // ========================================
    
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        
        (CollectionsWorkspace as IDisposable)?.Dispose();
        (EnvironmentsViewModel as IDisposable)?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
