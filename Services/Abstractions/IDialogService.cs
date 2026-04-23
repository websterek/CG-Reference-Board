using System.Threading.Tasks;

namespace CGReferenceBoard.Services.Abstractions;

/// <summary>
/// Abstracts all user-facing dialog interactions so that ViewModels can trigger
/// dialogs without taking a direct dependency on Avalonia window types.
/// Implementations live in the View layer and are injected at startup.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a modal confirmation dialog and returns <c>true</c> if the user confirmed.</summary>
    Task<bool> ShowConfirmAsync(
        string title,
        string message,
        string confirmText = "OK",
        string cancelText = "Cancel");

    /// <summary>Shows a single-line text input dialog and returns the entered text, or <c>null</c> if cancelled.</summary>
    Task<string?> ShowTextInputAsync(string title, string prompt);

    /// <summary>Shows a native save-file dialog and returns the chosen path, or <c>null</c> if cancelled.</summary>
    Task<string?> ShowSaveFileAsync(
        string title,
        string defaultExtension,
        string filterName,
        string[] patterns);

    /// <summary>Shows a native open-file dialog and returns the chosen paths, or <c>null</c> if cancelled.</summary>
    Task<string[]?> ShowOpenFilesAsync(
        string title,
        bool allowMultiple,
        string filterName,
        string[] patterns);

    /// <summary>
    /// Shows the Create Database wizard dialog.
    /// Returns a tuple of (success, output path).
    /// </summary>
    Task<(bool Success, string? Path)> ShowCreateDatabaseWizardAsync();

    /// <summary>Displays a transient toast notification with the given message.</summary>
    void ShowToast(string message);
}
