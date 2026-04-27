using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class ClipboardService : IClipboardService
{
    public Task CopyImageAsync(Bitmap bitmap)
    {
        return Task.CompletedTask;
    }

    public Task CopyTextAsync(string text)
    {
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync()
    {
        return Task.FromResult<string?>(null);
    }

    public Task<Bitmap?> GetImageAsync()
    {
        return Task.FromResult<Bitmap?>(null);
    }
}