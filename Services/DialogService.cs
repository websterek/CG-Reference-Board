using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.Views;

namespace CGReferenceBoard.Services;

public class DialogService : IDialogService
{
    private Func<Window?> _ownerFn = () => null;

    public DialogService() { }

    public void SetOwnerProvider(Func<Window?> provider) => _ownerFn = provider;

    private Window? Owner => _ownerFn();

    public async Task<bool> ShowConfirmAsync(
        string title, string message,
        string confirmText = "OK", string cancelText = "Cancel")
    {
        var owner = Owner;
        if (owner is null) return false;

        var tcs = new TaskCompletionSource<bool>();

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var lbl = new TextBlock
        {
            Text = message,
            Margin = new Thickness(16),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(lbl, 0);

        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8),
            Spacing = 8
        };
        Grid.SetRow(btns, 1);

        var win = new Window
        {
            Title = title,
            Width = 380,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = grid
        };

        var okBtn = new Button { Content = confirmText };
        var cancelBtn = new Button { Content = cancelText };
        okBtn.Click += (_, _) => { tcs.TrySetResult(true); win.Close(); };
        cancelBtn.Click += (_, _) => { tcs.TrySetResult(false); win.Close(); };
        win.Closing += (_, _) => tcs.TrySetResult(false);

        btns.Children.Add(cancelBtn);
        btns.Children.Add(okBtn);
        grid.Children.Add(lbl);
        grid.Children.Add(btns);

        await win.ShowDialog(owner);
        return await tcs.Task;
    }

    public async Task<string?> ShowTextInputAsync(string title, string prompt)
    {
        var owner = Owner;
        if (owner is null) return null;

        var dlg = new TextInputDialog(title, "") { Title = title };
        return await dlg.ShowAsync(owner, prompt);
    }

    public async Task<string?> ShowSaveFileAsync(
        string title, string defaultExtension, string filterName, string[] patterns)
    {
        var owner = Owner;
        if (owner is null) return null;

        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(filterName) { Patterns = patterns }
            }
        };
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    public async Task<string[]?> ShowOpenFilesAsync(
        string title, bool allowMultiple, string filterName, string[] patterns)
    {
        var owner = Owner;
        if (owner is null) return null;

        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(filterName) { Patterns = patterns }
            }
        };
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (files is null || files.Count == 0) return null;
        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
    }

    public async Task<(bool Success, string? Path)> ShowCreateDatabaseWizardAsync()
    {
        var owner = Owner;
        if (owner is null) return (false, null);

        var dlg = new CreateDatabaseWizardDialog();
        return await dlg.ShowAsync(owner);
    }

    public void ShowToast(string message)
    {
        // Toast is rendered by the View layer via INotificationService.
        _ = message;
    }
}
