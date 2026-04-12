using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services;

/// <summary>
/// Manages board file persistence: loading, saving, recent-boards tracking,
/// and workspace directory listing. This service owns file I/O state that was
/// previously embedded in MainWindow code-behind.
/// </summary>
public class BoardFileService
{
    private string _workspaceDir;
    private string _currentBoardFile = "";
    private bool _hasUnsavedChanges;

    public string WorkspaceDir
    {
        get => _workspaceDir;
        set => _workspaceDir = value;
    }

    public string CurrentBoardFile
    {
        get => _currentBoardFile;
        set => _currentBoardFile = value;
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => _hasUnsavedChanges = value;
    }

    /// <summary>Most recently opened board file paths.</summary>
    public ObservableCollection<string> RecentBoards { get; } = new();

    /// <summary>Board files found in the current workspace directory.</summary>
    public ObservableCollection<BoardMenuItemViewModel> BoardFilesInDirectory { get; } = new();

    public bool HasRecentBoards => RecentBoards.Count > 0;
    public bool HasBoardFilesInDirectory => BoardFilesInDirectory.Count > 0;

    public BoardFileService()
    {
        _workspaceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Constants.ConfigDirName, "Assets");
        if (!Directory.Exists(_workspaceDir))
            Directory.CreateDirectory(_workspaceDir);
    }

    /// <summary>
    /// Loads the recent boards list from the config file.
    /// </summary>
    public async Task LoadRecentBoardsAsync()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Constants.ConfigDirName, Constants.RecentBoardsFileName);

        if (File.Exists(path))
        {
            try
            {
                string json = await File.ReadAllTextAsync(path);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    foreach (var b in list.Where(File.Exists))
                        RecentBoards.Add(b);
                }
            }
            catch { /* ignore corrupt recent boards file */ }
        }
    }

    /// <summary>
    /// Adds a board path to the recent boards list and persists it.
    /// </summary>
    public async Task AddRecentBoardAsync(string path)
    {
        if (RecentBoards.Contains(path))
            RecentBoards.Remove(path);
        RecentBoards.Insert(0, path);
        while (RecentBoards.Count > Constants.MaxRecentBoards)
            RecentBoards.RemoveAt(RecentBoards.Count - 1);

        string confDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Constants.ConfigDirName);
        if (!Directory.Exists(confDir))
            Directory.CreateDirectory(confDir);

        string confPath = Path.Combine(confDir, Constants.RecentBoardsFileName);
        try
        { await File.WriteAllTextAsync(confPath, JsonSerializer.Serialize(RecentBoards)); }
        catch { /* non-critical */ }
    }

    /// <summary>
    /// Refreshes the list of board files in the current workspace directory.
    /// </summary>
    public void UpdateBoardDirectoryList()
    {
        BoardFilesInDirectory.Clear();
        if (string.IsNullOrEmpty(_workspaceDir) || !Directory.Exists(_workspaceDir))
            return;

        foreach (var file in Directory.GetFiles(_workspaceDir, $"*{Constants.DefaultBoardExtension}").OrderBy(Path.GetFileName))
        {
            BoardFilesInDirectory.Add(new BoardMenuItemViewModel
            {
                FileName = Path.GetFileName(file),
                IsActive = file == _currentBoardFile
            });
        }
    }

    /// <summary>
    /// Loads a board from a file path. Returns the deserialized cells and annotations.
    /// Updates workspace directory and current board file state.
    /// </summary>
    public async Task<(List<CellViewModel> Cells, List<AnnotationViewModel> Annotations)> LoadBoardAsync(string filePath)
    {
        _currentBoardFile = filePath;
        _workspaceDir = Path.GetDirectoryName(_currentBoardFile)!;
        UpdateBoardDirectoryList();

        string json = await File.ReadAllTextAsync(_currentBoardFile);
        var result = BoardSerializer.Deserialize(json);

        _hasUnsavedChanges = false;
        await AddRecentBoardAsync(_currentBoardFile);

        return result;
    }

    /// <summary>
    /// Saves the current board state to the active file.
    /// Returns the serialized JSON (for undo stack use).
    /// </summary>
    public async Task<string?> SaveBoardAsync(
        IEnumerable<CellViewModel> cells,
        IEnumerable<AnnotationViewModel> annotations)
    {
        if (string.IsNullOrEmpty(_currentBoardFile))
            return null;

        string json = BoardSerializer.Serialize(cells, annotations);
        await File.WriteAllTextAsync(_currentBoardFile, json);

        _hasUnsavedChanges = false;
        await AddRecentBoardAsync(_currentBoardFile);

        return json;
    }

    /// <summary>
    /// Serializes the board state without writing to disk (for undo snapshots).
    /// </summary>
    public string SerializeBoard(
        IEnumerable<CellViewModel> cells,
        IEnumerable<AnnotationViewModel> annotations)
    {
        return BoardSerializer.Serialize(cells, annotations);
    }

    /// <summary>
    /// Marks the board as having unsaved changes.
    /// </summary>
    public void MarkUnsaved()
    {
        _hasUnsavedChanges = true;
    }

    /// <summary>
    /// Builds the window title string based on current state.
    /// </summary>
    public string GetWindowTitle(bool isViewMode)
    {
        string baseName = Constants.AppName;
        if (!string.IsNullOrEmpty(_currentBoardFile))
            baseName += $" - {Path.GetFileName(_currentBoardFile)}";
        if (_hasUnsavedChanges)
            baseName += " *";
        if (isViewMode)
            baseName += " [VIEW MODE]";
        return baseName;
    }

    /// <summary>
    /// Ensures the images and videos subdirectories exist in the workspace.
    /// </summary>
    public void EnsureAssetDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_workspaceDir, "images"));
        Directory.CreateDirectory(Path.Combine(_workspaceDir, "videos"));
    }

    /// <summary>
    /// Copies a source image to the workspace images directory.
    /// Returns the destination path.
    /// </summary>
    public async Task<string> CopyImageToWorkspaceAsync(string sourcePath)
    {
        string destDir = Path.Combine(_workspaceDir, "images");
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        string destPath = Path.Combine(destDir, Path.GetFileName(sourcePath));
        if (sourcePath != destPath && !File.Exists(destPath))
        {
            using var sourceStream = File.OpenRead(sourcePath);
            using var destStream = File.Create(destPath);
            await sourceStream.CopyToAsync(destStream);
        }

        return destPath;
    }
}
