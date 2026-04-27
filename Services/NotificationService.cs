using System;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class NotificationService : INotificationService
{
    public void ReportInfo(string message)
    {
    }

    public void ReportError(Exception ex)
    {
    }

    public void ReportError(string message)
    {
    }
}