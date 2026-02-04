using System;
using System.Threading.Tasks;

namespace AvalonHttp.Messages;

public class ConfirmMessage
{
    public string Title { get; }
    public string Message { get; }
    // Primary Action (e.g. "Save & Exit")
    public Func<Task> OnConfirm { get; }
    public string ConfirmButtonText { get; }

    public Func<Task>? OnAlternate { get; }
    public string? AlternateButtonText { get; }
    
    public Func<Task>? OnCancel { get; }
    public string CancelButtonText { get; }

    public ConfirmMessage(
        string title, 
        string message, 
        Func<Task> onConfirm, 
        string confirmButtonText = "Save",
        string cancelButtonText = "Cancel",
        Func<Task>? onCancel = null,
        Func<Task>? onAlternate = null,
        string? alternateButtonText = null)
    {
        Title = title;
        Message = message;
        OnConfirm = onConfirm;
        ConfirmButtonText = confirmButtonText;
        CancelButtonText = cancelButtonText;
        OnCancel = onCancel;
        OnAlternate = onAlternate;
        AlternateButtonText = alternateButtonText;
    }
}