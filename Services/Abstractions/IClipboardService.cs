using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace CGReferenceBoard.Services.Abstractions;

/// <summary>
/// Abstracts clipboard operations so ViewModels can copy/paste without
/// taking a direct dependency on Avalonia's <c>TopLevel.Clipboard</c>.
/// </summary>
public interface IClipboardService
{
    /// <summary>Copies a bitmap image to the system clipboard.</summary>
    Task CopyImageAsync(Bitmap bitmap);

    /// <summary>Copies a plain-text string to the system clipboard.</summary>
    Task CopyTextAsync(string text);

    /// <summary>Returns the current clipboard text, or <c>null</c> if the clipboard contains no text.</summary>
    Task<string?> GetTextAsync();

    /// <summary>Returns the current clipboard image, or <c>null</c> if the clipboard contains no image.</summary>
    Task<Bitmap?> GetImageAsync();
}
