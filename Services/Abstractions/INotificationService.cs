using System;

namespace CGReferenceBoard.Services.Abstractions;

public interface INotificationService
{
    void ReportInfo(string message);
    void ReportError(Exception ex);
    void ReportError(string message);

    /// <summary>Shows a neutral informational toast message.</summary>
    void ShowToast(string message);

    /// <summary>Raised when a toast message is ready to display in the UI.</summary>
    event Action<string>? ToastNotified;
}
