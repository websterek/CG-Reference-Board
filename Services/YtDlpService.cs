using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CGReferenceBoard.Services;

/// <summary>
/// Represents the outcome of a yt-dlp video download operation.
/// </summary>
public record VideoDownloadResult(bool Success, string? VideoPath, string? ThumbnailPath, string? ErrorMessage);

/// <summary>
/// Provides functionality to download videos and thumbnails using yt-dlp.
/// </summary>
public static class YtDlpService
{
    /// <summary>
    /// Downloads a video and its thumbnail from the given URL using yt-dlp.
    /// The work is performed on a background thread.
    /// </summary>
    /// <param name="url">The video URL to download.</param>
    /// <param name="videosDirectory">The directory where downloaded files will be stored.</param>
    /// <returns>A <see cref="VideoDownloadResult"/> indicating success or failure.</returns>
    public static Task<VideoDownloadResult> DownloadVideoAsync(string url, string videosDirectory)
    {
        return Task.Run(() =>
        {
            try
            {
                var guid = Guid.NewGuid().ToString();

                if (!Directory.Exists(videosDirectory))
                    Directory.CreateDirectory(videosDirectory);

                var ytDlpPath = ResolveYtDlpPath();
                var outputTemplate = Path.Combine(videosDirectory, guid + ".%(ext)s");

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = $"--write-thumbnail -f mp4 -o \"{outputTemplate}\" \"{url}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                // Read redirected streams before WaitForExit to avoid deadlocks
                // when the process writes more than the OS buffer can hold.
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var videoFile = Directory.GetFiles(videosDirectory, guid + ".mp4").FirstOrDefault();
                var thumbnailFile = Directory.GetFiles(videosDirectory, guid + ".*")
                    .FirstOrDefault(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                     || f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));

                if (videoFile != null && thumbnailFile != null)
                {
                    return new VideoDownloadResult(true, videoFile, thumbnailFile, null);
                }

                return new VideoDownloadResult(false, null, null,
                    $"Download completed but expected files were not found. yt-dlp stderr: {stderr}");
            }
            catch (Exception ex)
            {
                return new VideoDownloadResult(false, null, null, ex.Message);
            }
        });
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
