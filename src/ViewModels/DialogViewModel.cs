using System;
using System.Threading.Tasks;
using AvalonHttp.Messages;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AvalonHttp.ViewModels;

public partial class DialogViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDialogOpen;
    
    [ObservableProperty]
    private string _dialogTitle = string.Empty;
    
    [ObservableProperty]
    private string _dialogMessage = string.Empty;
    
    [ObservableProperty]
    private string _confirmButtonText = "OK";
    
    [ObservableProperty]
    private string _cancelButtonText = "Cancel";
    
    [ObservableProperty]
    private bool _isCancelButtonVisible = true;
    
    [ObservableProperty]
    private IBrush _confirmButtonBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
    
    [ObservableProperty]
    private DialogType _currentType = DialogType.Info;
    
    [ObservableProperty]
    private bool _isConfirmDestructive;

    private Func<Task>? _onConfirm;
    private Func<Task>? _onCancel;

    public DialogViewModel()
    {
        System.Diagnostics.Debug.WriteLine("🔵 DialogViewModel created");
        System.Diagnostics.Debug.WriteLine($"🔵 This instance: {GetHashCode()}");
        // Listen for dialog messages
        WeakReferenceMessenger.Default.Register<DialogMessage>(this, OnDialogMessageReceived);
        
        System.Diagnostics.Debug.WriteLine($"🔵 Registered for DialogMessage");
    }

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
        ConfigureAppearance(message.Type, message.CustomIcon);
        
        IsDialogOpen = true;
        
        System.Diagnostics.Debug.WriteLine($"🟢 IsDialogOpen set to: {IsDialogOpen}");
    }

    private void ConfigureAppearance(DialogType type, string? customIcon)
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

    private void ClearState()
    {
        _onConfirm = null;
        _onCancel = null;
    }
}