using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using AvalonHttp.ViewModels.EnvironmentAggregate;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
    // Global Dialog State
    // ========================================
    
    [ObservableProperty] 
    private bool _isDialogOpen;
    
    [ObservableProperty] 
    private string _dialogTitle = string.Empty;
    
    [ObservableProperty] 
    private string _dialogMessage = string.Empty;
    
    [ObservableProperty] 
    private string _confirmButtonText = "Confirm";
    
    private Func<Task>? _pendingConfirmAction;

    // ========================================
    // Constructor
    // ========================================
    
    public MainWindowViewModel(
        CollectionWorkspaceViewModel collectionsWorkspace,
        EnvironmentsViewModel environmentsViewModel)
    {
        CollectionsWorkspace = collectionsWorkspace ?? 
            throw new ArgumentNullException(nameof(collectionsWorkspace));
        EnvironmentsViewModel = environmentsViewModel ?? 
            throw new ArgumentNullException(nameof(environmentsViewModel));

        // Subscribe to global confirm messages
        WeakReferenceMessenger.Default.Register<ConfirmMessage>(this, OnConfirmMessageReceived);
    }

    // ========================================
    // Initialization
    // ========================================
    
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

    // ========================================
    // Navigation Commands
    // ========================================
    
    [RelayCommand]
    private async Task ShowCollections()
    {
        if (CurrentView == "Collections") return;
        
        await CloseAllEdits();
        CurrentView = "Collections";
    }

    [RelayCommand]
    private async Task ShowEnvironments()
    {
        if (CurrentView == "Environments") return;
        
        await CloseAllEdits();
        CurrentView = "Environments";
    }

    // ========================================
    // Dialog Handling
    // ========================================
    
    private void OnConfirmMessageReceived(object recipient, ConfirmMessage message)
    {
        DialogTitle = message.Title;
        DialogMessage = message.Message;
        ConfirmButtonText = message.ConfirmButtonText ?? "Confirm";
        _pendingConfirmAction = message.OnConfirm;
        IsDialogOpen = true;
    }

    [RelayCommand]
    private async Task ExecuteConfirm()
    {
        IsDialogOpen = false;
        
        if (_pendingConfirmAction != null)
        {
            try 
            { 
                await _pendingConfirmAction.Invoke(); 
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Confirm action failed: {ex.Message}"); 
            }
            finally 
            { 
                _pendingConfirmAction = null; 
            }
        }
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsDialogOpen = false;
        _pendingConfirmAction = null;
    }

    // ========================================
    // Helper Methods
    // ========================================
    
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
                
                foreach (var request in CollectionsWorkspace.CollectionsViewModel.Collections)
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
            
            OnConfirmMessageReceived(this, new ConfirmMessage(
                "Unsaved Changes",
                message,
                () => 
                {
                    onConfirmed?.Invoke();
                    return Task.CompletedTask;
                },
                "Exit Anyway"
            ));
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
            return "You have unsaved changes in requests and environments. Exit anyway?";
        }
        else if (requestDirty)
        {
            return "You have unsaved changes in the current request. Exit anyway?";
        }
        else if (envDirty)
        {
            return "You have unsaved changes in environments. Exit anyway?";
        }
        
        return "You have unsaved changes. Exit anyway?";
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
