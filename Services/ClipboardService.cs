using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class ClipboardService : IClipboardService
{
    private Func<TopLevel?> _topLevelFn = () => null;

    public ClipboardService() { }

    public void SetTopLevelProvider(Func<TopLevel?> provider) => _topLevelFn = provider;

    private Avalonia.Input.Platform.IClipboard? Clipboard =>
        _topLevelFn()?.Clipboard;

    public async Task CopyTextAsync(string text)
    {
        var cb = Clipboard;
        if (cb is null) return;
        var dt = new DataTransfer();
        var item = new DataTransferItem();
        item.SetText(text);
        dt.Add(item);
        await cb.SetDataAsync(dt);
    }

    public Task<string?> GetTextAsync()
    {
        // Avalonia 12 IClipboard does not expose a synchronous text-get API.
        return Task.FromResult<string?>(null);
    }

    public async Task CopyImageAsync(Bitmap bitmap)
    {
        var cb = Clipboard;
        if (cb is null) return;
        var dt = new DataTransfer();
        var item = new DataTransferItem();
        item.SetBitmap(bitmap);
        dt.Add(item);
        await cb.SetDataAsync(dt);
    }

    public Task<Bitmap?> GetImageAsync()
    {
        // Image paste not yet implemented for all platforms.
        return Task.FromResult<Bitmap?>(null);
    }
}
