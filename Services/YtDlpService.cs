using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace CGReferenceBoard.Services;

/// <summary>
/// Represents the outcome of a yt-dlp video download operation.
/// </summary>
public record VideoDownloadResult(bool Success, string? VideoPath, string? ThumbnailPath, string? ErrorMessage);

/// <summary>
/// Provides functionality to download videos and thumbnails using yt-dlp via YoutubeDLSharp.
/// </summary>
public static class YtDlpService
{
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
    /// Extracts a single frame from a video file as a JPEG thumbnail using ffmpeg.
    /// Returns the path to the generated thumbnail, or null on failure.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="outputDirectory">Directory where the thumbnail will be saved.</param>
    public static Task<string?> ExtractThumbnailAsync(string videoPath, string outputDirectory)
    {
        return Task.Run(() =>
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
                        Arguments = $"-y -ss 1 -i \"{videoPath}\" -vframes 1 -vf scale=400:-1 \"{thumbPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();

                if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
                {
                    try
                    { process.Kill(entireProcessTree: true); }
                    catch { }
                    return null;
                }

                return File.Exists(thumbPath) ? thumbPath : null;
            }
            catch
            {
                return null;
            }
        });
    }

    /// <summary>
    /// Resolves the path to the bundled ffmpeg executable, or falls back to a system-wide install.
    /// </summary>
    private static string ResolveFfmpegPath()
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        string relativePath = isWindows
            ? Path.Combine("Include", "ffmpeg-windows", "ffmpeg.exe")
            : Path.Combine("Include", "ffmpeg-linux", "ffmpeg");

        if (File.Exists(relativePath))
            return relativePath;

        string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", relativePath);
        if (File.Exists(baseDirPath))
            return baseDirPath;

        // Fall back to system-wide ffmpeg
        return "ffmpeg";
    }

    /// <summary>
    /// Resolves the path to the bundled yt-dlp executable, or falls back to a system-wide install.
    /// </summary>
    private static string ResolveYtDlpPath()
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        string relativePath = isWindows
            ? Path.Combine("Include", "yt-dlp-windows", "yt-dlp.exe")
            : Path.Combine("Include", "yt-dlp-linux", "yt-dlp_linux");

        if (File.Exists(relativePath))
            return relativePath;

        string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", relativePath);
        if (File.Exists(baseDirPath))
            return baseDirPath;

        // Fall back to system-wide yt-dlp
        return "yt-dlp";
    }
}
