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
                        Arguments = $"--no-playlist --write-thumbnail --merge-output-format mp4 -o \"{outputTemplate}\" \"{url}\"",
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

                if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
                {
                    try
                    { process.Kill(entireProcessTree: true); }
                    catch { }
                    return new VideoDownloadResult(false, null, null, "Download timed out after 5 minutes.");
                }

                string[] videoExtensions = { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v" };
                string[] thumbExtensions = { ".jpg", ".jpeg", ".webp", ".png" };

                var allFiles = Directory.GetFiles(videosDirectory, guid + ".*");

                var videoFile = allFiles.FirstOrDefault(f =>
                    videoExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
                var thumbnailFile = allFiles.FirstOrDefault(f =>
                    thumbExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

                if (videoFile != null && thumbnailFile != null)
                {
                    return new VideoDownloadResult(true, videoFile, thumbnailFile, null);
                }

                return new VideoDownloadResult(false, null, null,
                    $"yt-dlp exited with code {process.ExitCode}. " +
                    (string.IsNullOrWhiteSpace(stderr) ? "No error output." : $"stderr: {stderr}"));
            }
            catch (Exception ex)
            {
                return new VideoDownloadResult(false, null, null, ex.Message);
            }
        });
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
