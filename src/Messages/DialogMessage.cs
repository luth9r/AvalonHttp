using System;
using System.Threading.Tasks;

namespace AvalonHttp.Messages;

/// <summary>
/// Represents a customizable dialog message with support for icons, buttons, and callbacks.
/// </summary>
public class DialogMessage
{
    /// <summary>
    /// Dialog type determines icon and color scheme.
    /// </summary>
    public DialogType Type { get; }
    
    /// <summary>
    /// Dialog title text.
    /// </summary>
    public string Title { get; }
    
    /// <summary>
    /// Dialog message/description.
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Primary action callback (e.g., "Confirm", "Save").
    /// </summary>
    public Func<Task>? OnConfirm { get; }
    
    /// <summary>
    /// Text for primary button.
    /// </summary>
    public string ConfirmButtonText { get; }
    
    /// <summary>
    /// Cancel action callback.
    /// </summary>
    public Func<Task>? OnCancel { get; }
    
    /// <summary>
    /// Text for cancel button.
    /// </summary>
    public string CancelButtonText { get; }
    
    /// <summary>
    /// Custom icon name (FluentIcon). If null, uses default based on Type.
    /// </summary>
    public string? CustomIcon { get; }

    public DialogMessage(
        DialogType type,
        string title,
        string message,
        Func<Task>? onConfirm = null,
        string confirmButtonText = "OK",
        Func<Task>? onCancel = null,
        string cancelButtonText = "Cancel",
        string? customIcon = null)
    {
        Type = type;
        Title = title;
        Message = message;
        OnConfirm = onConfirm;
        ConfirmButtonText = confirmButtonText;
        OnCancel = onCancel;
        CancelButtonText = cancelButtonText;
        CustomIcon = customIcon;
    }
    
    // ============================================
    // Factory Methods
    // ============================================
    
    /// <summary>
    /// Creates an info dialog (blue info icon, OK button only).
    /// </summary>
    public static DialogMessage Info(
        string title,
        string message,
        Func<Task>? onConfirm = null)
    {
        return new DialogMessage(
            DialogType.Info,
            title,
            message,
            onConfirm: onConfirm,
            confirmButtonText: "OK",
            onCancel: null
        );
    }
    
    /// <summary>
    /// Creates a success dialog (green check icon, OK button only).
    /// </summary>
    public static DialogMessage Success(
        string title,
        string message,
        Func<Task>? onConfirm = null)
    {
        return new DialogMessage(
            DialogType.Success,
            title,
            message,
            onConfirm: onConfirm,
            confirmButtonText: "OK",
            onCancel: null
        );
    }
    
    /// <summary>
    /// Creates an error dialog (red X icon, OK button only).
    /// </summary>
    public static DialogMessage Error(
        string title,
        string message,
        Func<Task>? onConfirm = null)
    {
        return new DialogMessage(
            DialogType.Error,
            title,
            message,
            onConfirm: onConfirm,
            confirmButtonText: "OK",
            onCancel: null
        );
    }
    
    /// <summary>
    /// Creates a confirmation dialog (yellow warning icon, Confirm + Cancel buttons).
    /// </summary>
    public static DialogMessage Confirm(
        string title,
        string message,
        Func<Task> onConfirm,
        Func<Task>? onCancel = null,
        string confirmText = "Confirm",
        string cancelText = "Cancel")
    {
        return new DialogMessage(
            DialogType.Warning,
            title,
            message,
            onConfirm: onConfirm,
            confirmButtonText: confirmText,
            onCancel: onCancel ?? (() => Task.CompletedTask),
            cancelButtonText: cancelText
        );
    }
    
    /// <summary>
    /// Creates a destructive action dialog (red trash icon, Delete + Cancel buttons).
    /// Supports 3-button layout: [Alternate] [Cancel] [Confirm]
    /// </summary>
    public static DialogMessage Destructive(
        string title,
        string message,
        Func<Task> onConfirm,
        Func<Task>? onCancel = null,
        string confirmText = "Delete",
        string cancelText = "Cancel")
    {
        return new DialogMessage(
            DialogType.Destructive,
            title,
            message,
            onConfirm: onConfirm,
            confirmButtonText: confirmText,
            onCancel: onCancel,
            cancelButtonText: cancelText
        );
    }
}

/// <summary>
/// Dialog type determines visual appearance (icon, colors).
/// </summary>
public enum DialogType
{
    Info,        // Blue, Info icon
    Success,     // Green, Check icon
    Warning,     // Yellow, Alert icon
    Error,       // Red, X icon
    Destructive  // Red, Trash icon
}
