using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace CGReferenceBoard.Services;

/// <summary>
/// Level-of-detail tiers for image loading.
/// </summary>
public enum ImageLod
{
    /// <summary>No bitmap loaded — show placeholder color rectangle.</summary>
    Placeholder = 0,

    /// <summary>Small thumbnail (~200 px wide) for zoomed-out views.</summary>
    Thumbnail = 1,

    /// <summary>Mid-resolution bitmap (~512 px wide) for intermediate zoom levels.</summary>
    Medium = 2,

    /// <summary>Full-resolution bitmap (up to 2048 px wide) for close-up views.</summary>
    Full = 3
}

/// <summary>
/// Manages image loading, thumbnail generation, average-color extraction,
/// and LOD-based lifecycle for board cells.
/// All public methods are thread-safe.
/// </summary>
public static class ImageManager
{
    // ───────── constants ─────────
    private const string ThumbSubDir = ".thumbs";
    private const int ThumbnailMaxWidth = 200;
    private const int ColorSampleGrid = 8; // sample an 8×8 grid for average color

    /// <summary>Maximum decoded pixel width for Full-LOD bitmaps (≥ 300 px on screen).</summary>
    public const int MaxFullDecodeWidth = 2048;

    /// <summary>
    /// Maximum decoded pixel width for Medium-LOD bitmaps (140–299 px on screen).
    /// 512 px gives ≥ 3× oversampling at the 140 px boundary and ≥ 1.7× at 299 px,
    /// while using ¼ the memory of a Full-LOD bitmap.
    /// </summary>
    public const int MaxMediumDecodeWidth = 512;

    // ───────── caches ─────────
    // Average color cache: filePath → hex string  e.g. "#FF3A2B1C"
    private static readonly ConcurrentDictionary<string, string> _colorCache = new();

    // ───────── thumbnail generation ─────────

    /// <summary>
    /// Returns the path to a thumbnail for <paramref name="imagePath"/>.
    /// Generates one (JPEG, ≤ 200 px wide) on first call; returns cached path thereafter.
    /// Returns null if the source image cannot be read.
    /// </summary>
    public static string? EnsureThumbnail(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        string dir = Path.GetDirectoryName(imagePath)!;
        string thumbDir = Path.Combine(dir, ThumbSubDir);
        string thumbFileName = Path.GetFileNameWithoutExtension(imagePath) + "_thumb.jpg";
        string thumbPath = Path.Combine(thumbDir, thumbFileName);

        if (File.Exists(thumbPath))
            return thumbPath;

        try
        {
            Directory.CreateDirectory(thumbDir);

            using var codec = SKCodec.Create(imagePath);
            if (codec == null)
                return null;

            var info = codec.Info;
            if (info.Width <= ThumbnailMaxWidth)
            {
                // Image is already small — just copy it
                File.Copy(imagePath, thumbPath, true);
                return thumbPath;
            }

            // Decode at reduced size using sample-size trick
            int sampleSize = Math.Max(1, info.Width / ThumbnailMaxWidth);
            var scaledInfo = new SKImageInfo(
                Math.Max(1, info.Width / sampleSize),
                Math.Max(1, info.Height / sampleSize),
                SKColorType.Rgba8888,
                SKAlphaType.Premul);

            using var bitmap = new SKBitmap(scaledInfo);
            var result = codec.GetPixels(scaledInfo, bitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                return null;

            // Resize to exact target width
            double scale = (double)ThumbnailMaxWidth / bitmap.Width;
            int newW = ThumbnailMaxWidth;
            int newH = Math.Max(1, (int)(bitmap.Height * scale));

            using var resized = bitmap.Resize(new SKImageInfo(newW, newH, SKColorType.Rgba8888), SKSamplingOptions.Default);
            if (resized == null)
                return null;

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
            using var stream = File.Create(thumbPath);
            data.SaveTo(stream);

            return thumbPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Asynchronous wrapper around <see cref="EnsureThumbnail"/>.
    /// </summary>
    public static Task<string?> EnsureThumbnailAsync(string? imagePath)
        => Task.Run(() => EnsureThumbnail(imagePath));

    // ───────── average color ─────────

    /// <summary>
    /// Returns the average colour of <paramref name="imagePath"/> as a hex string (e.g. "#FF8A6B42").
    /// The result is cached in memory. If the image cannot be read, returns a default dark-grey.
    /// </summary>
    public static string ComputeAverageColor(string? imagePath)
    {
        const string fallback = "#FF2A2A2A";
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return fallback;

        if (_colorCache.TryGetValue(imagePath, out var cached))
            return cached;

        try
        {
            using var codec = SKCodec.Create(imagePath);
            if (codec == null)
                return fallback;

            var info = codec.Info;
            // Decode to a tiny size for fast color sampling
            int sampleSize = Math.Max(1, Math.Max(info.Width, info.Height) / ColorSampleGrid);
            var smallInfo = new SKImageInfo(
                Math.Max(1, info.Width / sampleSize),
                Math.Max(1, info.Height / sampleSize),
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul);

            using var bitmap = new SKBitmap(smallInfo);
            var result = codec.GetPixels(smallInfo, bitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                return fallback;

            long r = 0, g = 0, b = 0;
            int count = 0;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var px = bitmap.GetPixel(x, y);
                    if (px.Alpha < 30)
                        continue; // skip near-transparent
                    r += px.Red;
                    g += px.Green;
                    b += px.Blue;
                    count++;
                }
            }

            if (count == 0)
                return fallback;

            int avgR = (int)(r / count);
            int avgG = (int)(g / count);
            int avgB = (int)(b / count);

            string hex = $"#FF{avgR:X2}{avgG:X2}{avgB:X2}";

            // Evict oldest-accessible entries when the cache grows too large.
            const int maxCacheSize = 500;
            if (_colorCache.Count >= maxCacheSize)
            {
                foreach (var k in _colorCache.Keys.Take(50).ToList())
                    _colorCache.TryRemove(k, out _);
            }
            _colorCache[imagePath] = hex;
            return hex;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Asynchronous wrapper around <see cref="ComputeAverageColor"/>.
    /// </summary>
    public static Task<string> ComputeAverageColorAsync(string? imagePath)
        => Task.Run(() => ComputeAverageColor(imagePath));

    // ───────── capped bitmap loading ─────────

    /// <summary>
    /// Loads an Avalonia Bitmap from disk, downscaling images wider than
    /// <paramref name="maxWidth"/> during decode to limit memory consumption.
    /// Returns null on failure.
    /// </summary>
    private static Bitmap? LoadBitmapCapped(string path, int maxWidth)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec == null)
                return LoadBitmapFallback(path, maxWidth);

            var info = codec.Info;
            if (info.Width <= maxWidth)
                return LoadBitmapFallback(path, maxWidth);

            // Decode at reduced resolution via sample-size trick.
            // Use Bgra8888 — Avalonia's native pixel format — so no channel
            // conversion is needed when writing to WriteableBitmap later.
            int sampleSize = Math.Max(1, info.Width / maxWidth);
            var scaledInfo = new SKImageInfo(
                Math.Max(1, info.Width / sampleSize),
                Math.Max(1, info.Height / sampleSize),
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            using var skBitmap = new SKBitmap(scaledInfo);
            var result = codec.GetPixels(scaledInfo, skBitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                return LoadBitmapFallback(path, maxWidth);

            // Resize to exact target width for consistent quality
            double scale = (double)maxWidth / skBitmap.Width;
            int targetW = maxWidth;
            int targetH = Math.Max(1, (int)(skBitmap.Height * scale));

            using var resized = skBitmap.Resize(
                new SKImageInfo(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul),
                SKSamplingOptions.Default);
            var source = resized ?? skBitmap;

            // Write pixels directly into an Avalonia WriteableBitmap — this eliminates
            // the previous encode (JPEG/PNG) → MemoryStream → Avalonia-decode round-trip,
            // saving a full compress+decompress cycle for every large image loaded.
            var wb = new WriteableBitmap(
                new Avalonia.PixelSize(source.Width, source.Height),
                new Avalonia.Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var fb = wb.Lock())
            {
                unsafe
                {
                    int srcRowBytes = source.RowBytes;
                    int dstRowBytes = fb.RowBytes;
                    int copyRowBytes = Math.Min(srcRowBytes, dstRowBytes);
                    nint srcBase = source.GetPixels();
                    nint dstBase = fb.Address;

                    for (int y = 0; y < source.Height; y++)
                    {
                        Buffer.MemoryCopy(
                            (void*)(srcBase + y * srcRowBytes),
                            (void*)(dstBase + y * dstRowBytes),
                            dstRowBytes,
                            copyRowBytes);
                    }
                }
            }

            return wb;
        }
        catch
        {
            try
            { return new Bitmap(path); }
            catch { return null; }
        }
    }

    /// <summary>
    /// Loads a bitmap from disk, capping the decoded width at <paramref name="maxWidth"/> pixels.
    /// Pass <see cref="MaxFullDecodeWidth"/> for Full LOD, <see cref="MaxMediumDecodeWidth"/>
    /// for Medium LOD, or <see cref="int.MaxValue"/> to load at original resolution.
    /// Returns null on failure.
    /// </summary>
    public static Bitmap? LoadBitmapFromPath(string? path, int maxWidth = MaxFullDecodeWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        try
        {
            return LoadBitmapCapped(path, maxWidth);
        }
        catch
        {
            return null;
        }
    }

    // ───────── LOD-aware bitmap loading ─────────

    /// <summary>
    /// Loads an Avalonia <see cref="Bitmap"/> appropriate for the requested LOD.
    /// <list type="bullet">
    ///   <item><see cref="ImageLod.Placeholder"/> — returns null (caller shows color rect).</item>
    ///   <item><see cref="ImageLod.Thumbnail"/> — loads the thumbnail; falls back to full if no thumb exists.</item>
    ///   <item><see cref="ImageLod.Full"/> — loads the full-resolution image.</item>
    /// </list>
    /// Returns null on failure.
    /// </summary>
    public static Bitmap? LoadBitmap(string? imagePath, ImageLod lod)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        if (lod == ImageLod.Placeholder)
            return null;

        try
        {
            if (lod == ImageLod.Thumbnail)
            {
                var thumbPath = EnsureThumbnail(imagePath);
                if (thumbPath != null && File.Exists(thumbPath))
                    return new Bitmap(thumbPath);
                // Thumbnail generation failed — fall through to medium decode.
            }

            int decodeWidth = lod == ImageLod.Full ? MaxFullDecodeWidth : MaxMediumDecodeWidth;
            return LoadBitmapCapped(imagePath, decodeWidth);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Async version of <see cref="LoadBitmap"/>.
    /// </summary>
    public static Task<Bitmap?> LoadBitmapAsync(string? imagePath, ImageLod lod)
        => Task.Run(() => LoadBitmap(imagePath, lod));

    // ───────── LOD determination ─────────

    /// <summary>
    /// Determines the appropriate LOD for a cell given its on-screen pixel width.
    /// </summary>
    /// <param name="cellScreenWidth">The cell's width in screen pixels (cell.PixelWidth × zoom).</param>
    /// <param name="isVisible">Whether the cell's bounding box intersects the viewport.</param>
    /// <remarks>
    /// Tiers:
    ///   • Off-viewport or &lt; 30 px  → Placeholder (colour rect only — no bitmap).
    ///   • 30 – 139 px on screen        → Thumbnail  (~200 px JPEG from disk cache).
    ///   • 140 – 299 px on screen       → Medium     (512 px in-memory decode).
    ///   • ≥ 300 px on screen           → Full       (2048 px in-memory decode).
    ///
    /// Reference breakpoints for a 1×1 cell (160 canvas px):
    ///   Placeholder below ~19 % zoom, Thumbnail 19–87 %, Medium 87–187 %, Full ≥ 187 %.
    /// For a 2×2 cell (320 canvas px):
    ///   Placeholder below ~9 %, Thumbnail 9–44 %, Medium 44–94 %, Full ≥ 94 %.
    /// </remarks>
    public static ImageLod DetermineLod(double cellScreenWidth, bool isVisible)
    {
        if (!isVisible)
            return ImageLod.Placeholder;

        // Below 30 px the image is a few pixels square — the average-colour
        // placeholder gives equivalent information at zero decode cost.
        if (cellScreenWidth < 30)
            return ImageLod.Placeholder;

        if (cellScreenWidth < 140)
            return ImageLod.Thumbnail;

        // Medium LOD (512 px) bridges the gap — avoids loading a 2048 px bitmap
        // for cells that are only 140–299 px wide on screen.
        if (cellScreenWidth < 300)
            return ImageLod.Medium;

        return ImageLod.Full;
    }

    // ───────── fallback bitmap loading ─────────

    /// <summary>
    /// Fallback for loading a bitmap when SkiaSharp fails or image is small enough.
    /// Applies size cap to prevent loading oversized images.
    /// </summary>
    private static Bitmap? LoadBitmapFallback(string path, int maxWidth)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec == null)
                return new Bitmap(path);

            var info = codec.Info;
            if (info.Width <= maxWidth)
                return new Bitmap(path);

            // Scale down using SkiaSharp to respect maxWidth
            int sampleSize = Math.Max(1, info.Width / maxWidth);
            var scaledInfo = new SKImageInfo(
                Math.Max(1, info.Width / sampleSize),
                Math.Max(1, info.Height / sampleSize),
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            using var skBitmap = new SKBitmap(scaledInfo);
            var result = codec.GetPixels(scaledInfo, skBitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                return new Bitmap(path);

            double scale = (double)maxWidth / skBitmap.Width;
            int targetW = maxWidth;
            int targetH = Math.Max(1, (int)(skBitmap.Height * scale));

            using var resized = skBitmap.Resize(
                new SKImageInfo(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul),
                SKSamplingOptions.Default);
            var source = resized ?? skBitmap;

            var wb = new WriteableBitmap(
                new Avalonia.PixelSize(source.Width, source.Height),
                new Avalonia.Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var fb = wb.Lock())
            {
                unsafe
                {
                    int srcRowBytes = source.RowBytes;
                    int dstRowBytes = fb.RowBytes;
                    int copyRowBytes = Math.Min(srcRowBytes, dstRowBytes);
                    nint srcBase = source.GetPixels();
                    nint dstBase = fb.Address;

                    for (int y = 0; y < source.Height; y++)
                    {
                        Buffer.MemoryCopy(
                            (void*)(srcBase + y * srcRowBytes),
                            (void*)(dstBase + y * dstRowBytes),
                            dstRowBytes,
                            copyRowBytes);
                    }
                }
            }

            return wb;
        }
        catch
        {
            try { return new Bitmap(path); }
            catch { return null; }
        }
    }

    // ───────── cleanup ─────────

    /// <summary>
    /// Clears all in-memory caches. Call when switching boards.
    /// </summary>
    public static void ClearCaches()
    {
        _colorCache.Clear();
    }
}
