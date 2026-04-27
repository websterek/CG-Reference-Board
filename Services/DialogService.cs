using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.Views;

namespace CGReferenceBoard.Services;

public class DialogService : IDialogService
{
    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
    {
        return Task.FromResult(true);
    }

    public Task<string?> ShowTextInputAsync(string title, string prompt)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> ShowSaveFileAsync(string title, string defaultExtension, string filterName, string[] patterns)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string[]?> ShowOpenFilesAsync(string title, bool allowMultiple, string filterName, string[] patterns)
    {
        return Task.FromResult<string[]?>(null);
    }

    public Task<(bool Success, string? Path)> ShowCreateDatabaseWizardAsync()
    {
        return Task.FromResult<(bool, string?)>((false, null));
    }

    public void ShowToast(string message)
    {
    }
}