using System;

namespace CGReferenceBoard.Services.Abstractions;

public interface INotificationService
{
    void ReportInfo(string message);
    void ReportError(Exception ex);
    void ReportError(string message);
}