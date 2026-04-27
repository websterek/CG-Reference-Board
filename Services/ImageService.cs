using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class ImageService : IImageService
{
    public Task<Bitmap?> LoadImageAsync(string path, ImageLod lod, CancellationToken ct = default)
    {
        return Task.FromResult<Bitmap?>(null);
    }

    public Task<Bitmap?> GetThumbnailAsync(string path, CancellationToken ct = default)
    {
        return Task.FromResult<Bitmap?>(null);
    }

    public System.Drawing.Color ComputeAverageColor(string path)
    {
        return System.Drawing.Color.Gray;
    }

    public void InvalidateCache(string path)
    {
    }

    public void ClearCache()
    {
    }
}