using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace CGReferenceBoard.Services.Abstractions;

public interface IImageService
{
    Task<Bitmap?> LoadImageAsync(string path, ImageLod lod, CancellationToken ct = default);
    Task<Bitmap?> GetThumbnailAsync(string path, CancellationToken ct = default);
    System.Drawing.Color ComputeAverageColor(string path);
    void InvalidateCache(string path);
    void ClearCache();
}