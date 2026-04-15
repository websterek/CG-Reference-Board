using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CGReferenceBoard.Helpers;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace CGReferenceBoard.Services;

/// <summary>
/// Represents the outcome of a yt-dlp video download operation.
/// </summary>
public record VideoDownloadResult(bool Success, string? VideoPath, string? ThumbnailPath, string? ErrorMessage);

/// <summary>
/// Represents the outcome of a yt-dlp media download operation (video or image).
/// </summary>
public record MediaDownloadResult(bool Success, string? MediaPath, string? ThumbnailPath, bool IsVideo, string? ErrorMessage);

/// <summary>
/// Provides functionality to download videos and thumbnails using yt-dlp via YoutubeDLSharp.
/// </summary>
public static class YtDlpService
{
    public static async Task<bool> IsVideoAvailableAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            string ytdlpPath = ResolveYtDlpPath();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ytdlpPath,
                    Arguments = $"--dump-json --no-download --no-playlist {SanitizeArgument(url)}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Ensure the external process is killed if the linked cancellation token fires.
            using var reg = linkedCts.Token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort kill; swallow any exceptions here to avoid throwing from the registration callback.
                }
            });

            var waitTask = process.WaitForExitAsync(linkedCts.Token);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(waitTask, stdoutTask, stderrTask);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdoutTask.Result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Explicit cancellation by the caller — treat as unavailable.
            return false;
        }
        catch
        {
            return false;
        }
    }
    /// <summary>
    /// Downloads a video and its thumbnail from the given URL using yt-dlp via YoutubeDLSharp.
    /// </summary>
    /// <param name="url">The video URL to download.</param>
    /// <param name="videosDirectory">The directory where downloaded files will be stored.</param>
    /// <param name="onProgress">
    /// Optional callback invoked with (percentComplete 0–100, statusString) as download progresses.
    /// The callback is invoked on the thread that YoutubeDLSharp reports progress on;
    /// the caller is responsible for dispatching to the UI thread if needed.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the download.</param>
    /// <returns>A <see cref="VideoDownloadResult"/> indicating success or failure.</returns>
    public static async Task<VideoDownloadResult> DownloadVideoAsync(
        string url,
        string videosDirectory,
        Action<float, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var guid = Guid.NewGuid().ToString();

            if (!Directory.Exists(videosDirectory))
                Directory.CreateDirectory(videosDirectory);

            var ytdl = new YoutubeDL
            {
                YoutubeDLPath = ResolveYtDlpPath(),
                FFmpegPath = ResolveFfmpegPath(),
                OutputFolder = videosDirectory,
                OutputFileTemplate = guid + ".%(ext)s"
            };

            var progressHandler = new Progress<DownloadProgress>(progress =>
            {
                if (onProgress == null)
                    return;

                float percent = progress.Progress * 100f;

                string status = progress.State switch
                {
                    DownloadState.Downloading =>
                        $"Downloading {progress.Progress * 100:F0}% @ {progress.DownloadSpeed} ETA {progress.ETA}",
                    DownloadState.PostProcessing => "Post-processing...",
                    DownloadState.PreProcessing => "Starting...",
                    _ => $"{percent:F0}%"
                };

                onProgress(percent, status);
            });

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var result = await ytdl.RunVideoDownload(
                url,
                mergeFormat: DownloadMergeFormat.Mp4,
                ct: linkedCts.Token,
                progress: progressHandler,
                overrideOptions: new OptionSet
                {
                    NoPlaylist = true,
                    WriteThumbnail = true
                });

            if (result.Success)
            {
                string[] videoExtensions = { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v" };
                string[] thumbExtensions = { ".jpg", ".jpeg", ".webp", ".png" };

                var allFiles = Directory.GetFiles(videosDirectory, guid + ".*");

                var videoFile = allFiles.FirstOrDefault(f =>
                    videoExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
                var thumbnailFile = allFiles.FirstOrDefault(f =>
                    thumbExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

                if (videoFile != null && thumbnailFile != null)
                    return new VideoDownloadResult(true, videoFile, thumbnailFile, null);

                // Downloaded successfully but couldn't locate expected output files.
                return new VideoDownloadResult(false, null, null,
                    "Download reported success but the output files could not be found.");
            }

            var errorMessage = result.ErrorOutput is { Length: > 0 }
                ? string.Join(Environment.NewLine, result.ErrorOutput)
                : "yt-dlp reported failure with no additional output.";

            return new VideoDownloadResult(false, null, null, errorMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new VideoDownloadResult(false, null, null, "Download was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return new VideoDownloadResult(false, null, null, "Download timed out after 5 minutes.");
        }
        catch (Exception ex)
        {
            return new VideoDownloadResult(false, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Downloads media (video or image) from the given URL using yt-dlp.
    /// Handles both video URLs and direct image URLs uniformly.
    /// </summary>
    /// <param name="url">The URL to download (video or image).</param>
    /// <param name="outputDirectory">The directory where downloaded files will be stored.</param>
    /// <param name="onProgress">Optional progress callback (percent 0-100, status string).</param>
    /// <param name="cancellationToken">Token to cancel the download.</param>
    /// <returns>A <see cref="MediaDownloadResult"/> indicating success or failure.</returns>
    public static async Task<MediaDownloadResult> DownloadMediaAsync(
        string url,
        string outputDirectory,
        Action<float, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var guid = Guid.NewGuid().ToString();

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var ytdl = new YoutubeDL
            {
                YoutubeDLPath = ResolveYtDlpPath(),
                FFmpegPath = ResolveFfmpegPath(),
                OutputFolder = outputDirectory,
                OutputFileTemplate = guid + ".%(ext)s"
            };

            var progressHandler = new Progress<DownloadProgress>(progress =>
            {
                if (onProgress == null)
                    return;

                float percent = progress.Progress * 100f;

                string status = progress.State switch
                {
                    DownloadState.Downloading =>
                        $"Downloading {progress.Progress * 100:F0}% @ {progress.DownloadSpeed} ETA {progress.ETA}",
                    DownloadState.PostProcessing => "Post-processing...",
                    DownloadState.PreProcessing => "Starting...",
                    _ => $"{percent:F0}%"
                };

                onProgress(percent, status);
            });

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var result = await ytdl.RunVideoDownload(
                url,
                mergeFormat: DownloadMergeFormat.Mp4,
                ct: linkedCts.Token,
                progress: progressHandler,
                overrideOptions: new OptionSet
                {
                    NoPlaylist = true,
                    WriteThumbnail = true
                });

            if (!result.Success)
            {
                var errorMessage = result.ErrorOutput is { Length: > 0 }
                    ? string.Join(Environment.NewLine, result.ErrorOutput)
                    : "yt-dlp reported failure with no additional output.";
                return new MediaDownloadResult(false, null, null, false, errorMessage);
            }

            string[] videoExtensions = { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v" };
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".avif" };

            var allFiles = Directory.GetFiles(outputDirectory, guid + ".*");

            // Find and fix files with .unknown_video or other incorrect extensions
            foreach (var file in allFiles)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".unknown_video" || ext == ".unknown")
                {
                    string? fixedExt = TryDetectImageExtension(file, url);
                    if (fixedExt != null)
                    {
                        string newPath = Path.Combine(
                            Path.GetDirectoryName(file)!,
                            Path.GetFileNameWithoutExtension(file) + fixedExt);
                        try
                        {
                            File.Move(file, newPath);
                            // Update the file list
                            allFiles = Directory.GetFiles(outputDirectory, guid + ".*");
                        }
                        catch { /* ignore rename failures */ }
                    }
                }
            }

            allFiles = Directory.GetFiles(outputDirectory, guid + ".*");

            var videoFile = allFiles.FirstOrDefault(f =>
                videoExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
            var imageFile = allFiles.FirstOrDefault(f =>
                imageExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
            var thumbnailFile = allFiles.FirstOrDefault(f =>
                imageExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

            if (videoFile != null)
            {
                // It's a video - thumbnail is separate
                var thumb = allFiles.FirstOrDefault(f =>
                    f != videoFile && imageExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
                return new MediaDownloadResult(true, videoFile, thumb, true, null);
            }

            if (imageFile != null)
            {
                // It's an image - the file itself is the media, no separate thumbnail
                return new MediaDownloadResult(true, imageFile, null, false, null);
            }

            return new MediaDownloadResult(false, null, null, false,
                "Download reported success but the output files could not be found.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new MediaDownloadResult(false, null, null, false, "Download was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return new MediaDownloadResult(false, null, null, false, "Download timed out after 5 minutes.");
        }
        catch (Exception ex)
        {
            return new MediaDownloadResult(false, null, null, false, ex.Message);
        }
    }

    private static string? TryDetectImageExtension(string filePath, string originalUrl)
    {
        // Try to get extension from URL
        try
        {
            string urlExt = Path.GetExtension(new Uri(originalUrl).AbsolutePath).ToLowerInvariant();
            string[] validImageExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".avif" };
            if (validImageExts.Contains(urlExt))
                return urlExt;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryDetectImageExtension: failed to parse URL '{originalUrl}': {ex}");
        }

        // Try to detect from file header
        try
        {
            using var fs = File.OpenRead(filePath);
            var buffer = new byte[12];
            int read = fs.Read(buffer, 0, buffer.Length);
            if (read < 4)
                return null;

            // JPEG: FF D8 FF
            if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                return ".jpg";

            // PNG: 89 50 4E 47
            if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                return ".png";

            // GIF: 47 49 46 38
            if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
                return ".gif";

            // WebP: 52 49 46 46 ... 57 45 42 50
            if (buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46)
                return ".webp";

            // BMP: 42 4D
            if (buffer[0] == 0x42 && buffer[1] == 0x4D)
                return ".bmp";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryDetectImageExtension: failed to inspect file header '{filePath}': {ex}");
        }

        return null;
    }

    /// <summary>
    /// Extracts a single frame from a video file as a JPEG thumbnail using ffmpeg.
    /// Returns the path to the generated thumbnail, or null on failure. This method
    /// accepts an optional cancellation token and will kill the ffmpeg process if
    /// the operation is cancelled or times out.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="outputDirectory">Directory where the thumbnail will be saved.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    public static Task<string?> ExtractThumbnailAsync(string videoPath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                string thumbPath = Path.Combine(outputDirectory,
                    Path.GetFileNameWithoutExtension(videoPath) + "_thumb.jpg");

                if (File.Exists(thumbPath))
                    return thumbPath;

                string ffmpegPath = ResolveFfmpegPath();

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        // Extract a frame at 1 second into the video, scale to 400px wide
                        Arguments = $"-y -ss 1 -i {SanitizeArgument(videoPath)} -vframes 1 -vf scale=400:-1 {SanitizeArgument(thumbPath)}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                // Use a 30s timeout for ffmpeg, but allow caller cancellation as well.
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Ensure ffmpeg is killed if cancellation or timeout fires.
                using var _reg = linkedCts.Token.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ExtractThumbnailAsync: failed to kill ffmpeg on cancellation/timeout: {ex}");
                    }
                });

                // Drain both pipes concurrently. Reading them sequentially can deadlock:
                // if ffmpeg fills one pipe's OS buffer while we're blocked on the other,
                // neither side makes progress.
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var waitTask = process.WaitForExitAsync(linkedCts.Token);

                // Wait for process exit and pipe drains; if linked token cancels, WaitForExitAsync will throw.
                await Task.WhenAll(waitTask, stdoutTask, stderrTask);

                // If process exited with non-zero code, treat as failure but still check for thumbnail file.
                if (process.ExitCode != 0)
                {
                    var stderr = stderrTask.IsCompleted ? stderrTask.Result : string.Empty;
                    Debug.WriteLine($"ExtractThumbnailAsync: ffmpeg exited with code {process.ExitCode} for '{videoPath}': {stderr}");
                }

                return File.Exists(thumbPath) ? thumbPath : null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine($"ExtractThumbnailAsync: cancelled by caller for '{videoPath}'");
                return null;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"ExtractThumbnailAsync: timed out while extracting thumbnail for '{videoPath}'");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractThumbnailAsync: unexpected failure for '{videoPath}': {ex}");
                return null;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Resolves the path to the bundled ffmpeg executable, or falls back to a system-wide install.
    /// </summary>
    private static string ResolveFfmpegPath() =>
        PlatformHelper.ResolveBundledBinary(
            windowsRelativePath: Path.Combine("Include", "ffmpeg-windows", "ffmpeg.exe"),
            linuxRelativePath: Path.Combine("Include", "ffmpeg-linux", "ffmpeg"),
            fallbackName: "ffmpeg");

    /// <summary>
    /// Resolves the path to the bundled yt-dlp executable, or falls back to a system-wide install.
    /// </summary>
    private static string ResolveYtDlpPath() =>
        PlatformHelper.ResolveBundledBinary(
            windowsRelativePath: Path.Combine("Include", "yt-dlp-windows", "yt-dlp.exe"),
            linuxRelativePath: Path.Combine("Include", "yt-dlp-linux", "yt-dlp_linux"),
            fallbackName: "yt-dlp");

    /// <summary>
    /// Sanitizes an argument to prevent command injection attacks.
    /// Wraps the argument in quotes and escapes any embedded quotes.
    /// </summary>
    private static string SanitizeArgument(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "\"\"";
        return $"\"{input.Replace("\"", "\\\"")}\"";
    }
}
