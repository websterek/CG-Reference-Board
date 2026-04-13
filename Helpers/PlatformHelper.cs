using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CGReferenceBoard.Helpers;

/// <summary>
/// Helper utilities for file/process operations on Windows and Linux.
/// </summary>
public static class PlatformHelper
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Opens a file or directory with the system's default application.
    /// </summary>
    public static void OpenWithDefaultApp(string path)
    {
        try
        {
            if (IsWindows)
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            else if (IsLinux)
                Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = $"\"{path}\"", UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open with default app: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the system file explorer and selects the specified file.
    /// </summary>
    public static void ShowInFileExplorer(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            if (IsWindows)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            else if (IsLinux)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = $"--session --print-reply --dest=org.freedesktop.FileManager1 " +
                                $"/org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems " +
                                $"array:string:\"file://{path}\" string:\"\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open file explorer: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens a directory in the system file explorer.
    /// </summary>
    public static void OpenDirectory(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;
        OpenWithDefaultApp(directoryPath);
    }

    /// <summary>
    /// Ensures the file at <paramref name="path"/> has the executable bit set on Linux.
    /// No-op on Windows. Safe to call even if the file is already executable.
    /// </summary>
    public static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() || !File.Exists(path))
            return;

        try
        {
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode execBits =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

            if ((mode & execBits) != execBits)
                File.SetUnixFileMode(path, mode | execBits);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set executable bit on '{path}': {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the path to a bundled binary (e.g. ffmpeg, yt-dlp) that ships inside
    /// the application's <c>Include/</c> folder. Checks in order:
    /// <list type="number">
    ///   <item>Next to the executable — correct for published / deployed builds.</item>
    ///   <item>Relative to the current working directory — works with <c>dotnet run</c>.</item>
    ///   <item>Three levels up from the executable — Visual Studio / Rider F5 debug runs
    ///         where BaseDirectory is <c>bin/Debug/net10.0/</c>.</item>
    /// </list>
    /// Falls back to <paramref name="fallbackName"/> (assumed to be on <c>PATH</c>) when
    /// none of the bundled locations exist.
    /// On Linux the resolved file's executable bit is set automatically.
    /// </summary>
    /// <param name="windowsRelativePath">
    /// Path of the Windows binary relative to the project / publish root,
    /// e.g. <c>Include\ffmpeg-windows\ffmpeg.exe</c>.
    /// </param>
    /// <param name="linuxRelativePath">
    /// Path of the Linux binary relative to the project / publish root,
    /// e.g. <c>Include/ffmpeg-linux/ffmpeg</c>.
    /// </param>
    /// <param name="fallbackName">
    /// Name of the system-wide binary to use when the bundled one is not found,
    /// e.g. <c>"ffmpeg"</c>.
    /// </param>
    public static string ResolveBundledBinary(
        string windowsRelativePath,
        string linuxRelativePath,
        string fallbackName)
    {
        string relativePath = IsWindows ? windowsRelativePath : linuxRelativePath;

        // 1. Next to the executable — the correct location for published / deployed apps
        //    where CopyToOutputDirectory has placed the Include/ tree beside the binary.
        string appDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        if (File.Exists(appDirPath))
        {
            EnsureExecutable(appDirPath);
            return appDirPath;
        }

        // 2. Relative to the current working directory — works when running via
        //    `dotnet run` from the project root, or when the app is launched from
        //    its own directory (common on Windows Explorer double-click).
        if (File.Exists(relativePath))
        {
            EnsureExecutable(relativePath);
            return relativePath;
        }

        // 3. Three levels up from BaseDirectory — covers Visual Studio / Rider F5
        //    debug runs where BaseDirectory ends with bin/Debug/net10.0/.
        string devPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", relativePath));
        if (File.Exists(devPath))
        {
            EnsureExecutable(devPath);
            return devPath;
        }

        // 4. Fall back to a system-wide install on PATH.
        return fallbackName;
    }
}
