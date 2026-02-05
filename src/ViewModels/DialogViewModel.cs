using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AvalonHttp.ViewModels;

/// <summary>
/// Represents the view model for a dialog window.
/// </summary>
public partial class DialogViewModel : ObservableObject
{
    /// <summary>
    /// Indicates whether the dialog window is currently open.
    /// </summary>
    [ObservableProperty]
    private bool _isDialogOpen;
    
    /// <summary>
    /// The title of the dialog window.
    /// </summary>
    [ObservableProperty]
    private string _dialogTitle = string.Empty;
    
    /// <summary>
    /// The message to be displayed in the dialog window.
    /// </summary>
    [ObservableProperty]
    private string _dialogMessage = string.Empty;
    
    /// <summary>
    /// The text to be displayed on the confirm button.
    /// </summary>
    [ObservableProperty]
    private string _confirmButtonText = "OK";
    
    /// <summary>
    /// The text to be displayed on the cancel button.
    /// </summary>
    [ObservableProperty]
    private string _cancelButtonText = "Cancel";
    
    /// <summary>
    /// Indicates whether the cancel button should be visible.
    /// </summary>
    [ObservableProperty]
    private bool _isCancelButtonVisible = true;
    
    /// <summary>
    /// The brush to be used for the confirm button.
    /// </summary>
    [ObservableProperty]
    private IBrush _confirmButtonBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
    
    /// <summary>
    /// The type of dialog to be displayed.
    /// </summary>
    [ObservableProperty]
    private DialogType _currentType = DialogType.Info;
    
    /// <summary>
    /// Indicates whether the dialog should be displayed as a destructive action.
    /// </summary>
    [ObservableProperty]
    private bool _isConfirmDestructive;

    /// <summary>
    /// Represents the delegate function to be executed when the confirm action is triggered in the dialog.
    /// </summary>
    private Func<Task>? _onConfirm;

    /// <summary>
    /// Represents the delegate function to be executed when the cancel action is triggered in the dialog.
    /// </summary>
    private Func<Task>? _onCancel;

    public DialogViewModel()
    {
        System.Diagnostics.Debug.WriteLine("🔵 DialogViewModel created");
        System.Diagnostics.Debug.WriteLine($"🔵 This instance: {GetHashCode()}");
        // Listen for dialog messages
        WeakReferenceMessenger.Default.Register<DialogMessage>(this, OnDialogMessageReceived);
        
        System.Diagnostics.Debug.WriteLine($"🔵 Registered for DialogMessage");
    }

    /// <summary>
    /// Handles the reception of a <see cref="DialogMessage"/> and processes its data to update the dialog state.
    /// </summary>
    /// <param name="recipient">The recipient of the message, typically the current instance of the view model.</param>
    /// <param name="message">The <see cref="DialogMessage"/> containing details such as title, message, button texts, and configurations.</param>
    private void OnDialogMessageReceived(object recipient, DialogMessage message)
    {
        System.Diagnostics.Debug.WriteLine($"🟢 DialogMessage received!");
        System.Diagnostics.Debug.WriteLine($"🟢 Recipient: {recipient.GetHashCode()}");
        System.Diagnostics.Debug.WriteLine($"🟢 Title: {message.Title}");
        
        CurrentType = message.Type;
        DialogTitle = message.Title;
        DialogMessage = message.Message;
        ConfirmButtonText = message.ConfirmButtonText;
        CancelButtonText = message.CancelButtonText;
        
        _onConfirm = message.OnConfirm;
        _onCancel = message.OnCancel;
        
        // Configure appearance based on type
        ConfigureAppearance(message.Type);
        
        IsDialogOpen = true;
        
        System.Diagnostics.Debug.WriteLine($"🟢 IsDialogOpen set to: {IsDialogOpen}");
    }

    /// <summary>
    /// Configures the appearance of the dialog based on the specified type.
    /// </summary>
    /// <param name="type">The type of dialog to configure appearance for.</param>
    private void ConfigureAppearance(DialogType type)
    {
        switch (type)
        {
            case DialogType.Info:
                ConfirmButtonBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
                break;
            
            case DialogType.Success:
                ConfirmButtonBrush = new SolidColorBrush(Color.Parse("#10B981"));
                break;
            
            case DialogType.Warning:
                ConfirmButtonBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
                break;
            
            case DialogType.Error:
                ConfirmButtonBrush = new SolidColorBrush(Color.Parse("#EF4444"));
                break;
            
            case DialogType.Destructive:
                ConfirmButtonBrush = new SolidColorBrush(Color.Parse("#EF4444"));
                break;
        }
    }

    /// <summary>
    /// Executes the confirm action in the dialog.
    /// </summary>
    [RelayCommand]
    private async Task ExecuteConfirm()
    {
        IsDialogOpen = false;
        
        if (_onConfirm != null)
        {
            await _onConfirm();
        }
        
        ClearState();
    }

    /// <summary>
    /// Executes the cancel action in the dialog.
    /// </summary>
    [RelayCommand]
    private async Task ExecuteCancel()
    {
        IsDialogOpen = false;
        
        if (_onCancel != null)
        {
            await _onCancel();
        }
        
        ClearState();
    }

    /// <summary>
    /// Clears the state of the dialog.
    /// </summary>
    private void ClearState()
    {
        _onConfirm = null;
        _onCancel = null;
    }
}