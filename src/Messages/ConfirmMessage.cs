using System;
using System.Threading.Tasks;

namespace AvalonHttp.Messages;

public class ConfirmMessage
{
    public string Title { get; }
    public string Message { get; }
    public Func<Task> OnConfirm { get; }
    
    public string ConfirmButtonText { get; }

    public ConfirmMessage(string title, string message, Func<Task> onConfirm, string confirmButtonText = "Delete")
    {
        Title = title;
        Message = message;
        OnConfirm = onConfirm;
        ConfirmButtonText = confirmButtonText;
    }
}