using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class ImageServiceImpl : IImageService
{
    public Task<Bitmap?> LoadImageAsync(string path, ImageLod lod, CancellationToken ct = default)
    {
        return ImageManager.LoadBitmapAsync(path, lod);
    }

    public Task<Bitmap?> GetThumbnailAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() => ImageManager.LoadBitmap(path, ImageLod.Thumbnail), ct);
    }

    public Color ComputeAverageColor(string path)
    {
        var hex = ImageManager.ComputeAverageColor(path);
        return ColorTranslator.FromHtml(hex);
    }

    public void InvalidateCache(string path)
    {
        ImageManager.ClearCaches();
    }

    public void ClearCache()
    {
        ImageManager.ClearCaches();
    }
}