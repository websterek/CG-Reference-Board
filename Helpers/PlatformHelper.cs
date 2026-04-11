using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CGReferenceBoard.Helpers;

/// <summary>
/// Cross-platform helper utilities for file/process operations.
/// </summary>
public static class PlatformHelper
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

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
            else if (IsMacOS)
                Process.Start(new ProcessStartInfo { FileName = "open", Arguments = $"\"{path}\"", UseShellExecute = false });
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
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

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
            else if (IsMacOS)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"-R \"{path}\"",
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
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return;
        OpenWithDefaultApp(directoryPath);
    }
}
