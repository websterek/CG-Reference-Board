using System;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class NotificationService : INotificationService
{
    /// <summary>
    /// Raised when a toast message should be shown in the UI.
    /// The View subscribes to this and drives the toast animation.
    /// </summary>
    public event Action<string>? ToastNotified;

    public void ReportInfo(string message)
    {
        ToastNotified?.Invoke(message);
    }

    public void ReportError(Exception ex)
    {
        ToastNotified?.Invoke($"Error: {ex.Message}");
    }

    public void ReportError(string message)
    {
        ToastNotified?.Invoke($"Error: {message}");
    }

    /// <summary>Shows a neutral toast (no error/info prefix).</summary>
    public void ShowToast(string message)
    {
        ToastNotified?.Invoke(message);
    }
}
