using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CGReferenceBoard.Controls;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Layers.Infrastructure;
using CGReferenceBoard.Modes;
using CGReferenceBoard.Services;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Primary ViewModel for <see cref="CGReferenceBoard.Views.MainWindow"/>.
///
/// Owns all observable state that was previously scattered across the MainWindow
/// partial class files. The View subscribes to events (<see cref="AlwaysOnTopChanged"/>,
/// <see cref="ToastRequested"/>, etc.) for operations that require direct UI access.
///
/// This class is intentionally a <c>partial</c> class so that CommunityToolkit.Mvvm
/// source generators can emit the backing property/command code into a companion file.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    // ── Inner types ───────────────────────────────────────────────────────────

    /// <summary>Persisted user preference bag (serialised to user_settings.json).</summary>
    private sealed class UserSettings
    {
        public string AnnotationEffect { get; set; } = "None";
        public string GridBackground { get; set; } = "Dots";
    }

    // ── Services (injected / owned) ───────────────────────────────────────────

    /// <summary>Manages the active interaction mode (Grid / Annotation).</summary>
    public ModeService ModeService { get; }

    /// <summary>Manages the current item selection across cells and annotations.</summary>
    public SelectionService SelectionService { get; }

    /// <summary>
    /// Tracks the union bounding box of the current selection and exposes a
    /// mode-aware ApplyTransform method consumed by the TransformBoxControl overlay.
    /// </summary>
    public TransformAdornerViewModel Adorner { get; private set; } = null!;

    /// <summary>
    /// Layer manager that owns the visual layer hierarchy.
    /// Exposed publicly so the View can wire up ItemsSource bindings and
    /// layer-visibility toggles that still live in the code-behind during Step 7.
    /// </summary>
    public LayerManager LayerManager { get; } = new();

    // ── Undo / Redo infrastructure ────────────────────────────────────────────

    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private bool _isRestoringState;
    private string? _lastStateHash;

    /// <summary>Serialises concurrent <see cref="SaveBoardData"/> calls so writes never interleave.</summary>
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);

    // ── Board file state ──────────────────────────────────────────────────────

    /// <summary>Absolute path to the workspace directory (parent of the active .cgrb file).</summary>
    public string WorkspaceDir { get; private set; } = "";

    /// <summary>Absolute path to the currently open .cgrb file, or empty string when no file is open.</summary>
    public string CurrentBoardFile { get; private set; } = "";

    /// <summary>True when there are changes not yet written to disk.</summary>
    public bool HasUnsavedChanges { get; private set; }

    /// <summary>True when the application was launched in read-only view mode (--view flag).</summary>
    public bool IsViewMode { get; }

    /// <summary>
    /// Set to true once the user has confirmed they want to close/discard unsaved changes.
    /// Prevents the double-prompt on the second close attempt.
    /// </summary>
    public bool ClosingConfirmed { get; set; }

    // ── Observable collections ────────────────────────────────────────────────

    /// <summary>All grid cells currently on the board.</summary>
    public ObservableCollection<CellViewModel> GridCells { get; } = new();

    /// <summary>All annotation shapes currently on the board.</summary>
    public ObservableCollection<AnnotationViewModel> Annotations { get; } = new();

    /// <summary>Recently opened board file paths (most-recent first, max <see cref="Constants.MaxRecentBoards"/>).</summary>
    public ObservableCollection<string> RecentBoards { get; } = new();

    /// <summary>Board files found in the current workspace directory (for the Board menu).</summary>
    public ObservableCollection<BoardMenuItemViewModel> BoardFilesInDirectory { get; } = new();

    // ── Source-generated observable properties ────────────────────────────────

    /// <summary>Display name of the currently open board (derived from the file name).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _currentBoardName = Constants.AppName;

    /// <summary>Active brush colour in ARGB hex format, e.g. "#FFFF4444".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentBrushColorBrush))]
    private string _currentBrushColor = "#FFFF4444";

    /// <summary>Active brush stroke thickness in pixels.</summary>
    [ObservableProperty]
    private double _currentBrushThickness = 4.0;

    /// <summary>Whether the annotation layer is currently visible.</summary>
    [ObservableProperty]
    private bool _isAnnotationsVisible = true;

    /// <summary>Whether the window is pinned always-on-top.</summary>
    [ObservableProperty]
    private bool _isAlwaysOnTop;

    /// <summary>Whether the pointer is currently hovering over the canvas area.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCursorIconVisible))]
    private bool _isPointerOverCanvas;

    /// <summary>Grid background style: "Dots", "Grid", or "None".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridBackgroundDots))]
    [NotifyPropertyChangedFor(nameof(IsGridBackgroundGrid))]
    [NotifyPropertyChangedFor(nameof(IsGridBackgroundNone))]
    private string _gridBackgroundMode = "Dots";

    /// <summary>Annotation rendering effect: "None", "Shadow", or "Outline".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnnotationEffectNone))]
    [NotifyPropertyChangedFor(nameof(IsAnnotationEffectShadow))]
    [NotifyPropertyChangedFor(nameof(IsAnnotationEffectOutline))]
    private string _annotationEffectMode = "None";

    // ── View-integration events ───────────────────────────────────────────────

    /// <summary>
    /// Fired when <see cref="IsAlwaysOnTop"/> changes. The View subscribes and sets
    /// <c>Window.Topmost</c> accordingly (requires direct Window access).
    /// </summary>
    public event Action<bool>? AlwaysOnTopChanged;

    /// <summary>Fired when a toast notification should be displayed.</summary>
    public event Action<string>? ToastRequested;

    /// <summary>Fired when the viewport LOD system should schedule an update.</summary>
    public event Action? ViewportUpdateRequested;

    /// <summary>
    /// Fired when the startup overlay should be hidden (after a board is loaded).
    /// </summary>
    public event Action? StartupOverlayHideRequested;

    // ── Computed / derived properties ─────────────────────────────────────────

    /// <summary>
    /// Backward-compatibility alias: true when the application is in Annotation mode.
    /// Existing XAML bindings to <c>IsDrawMode</c> continue to work unchanged.
    /// </summary>
    public bool IsDrawMode => ModeService.IsAnnotationMode;

    /// <summary>The active annotation tool name, or empty string when in Grid mode.</summary>
    public string CurrentTool => ModeService.IsAnnotationMode
        ? ModeService.AnnotationMode.CurrentTool
        : "";

    /// <summary>Window title composed from workspace, board name, and current mode.</summary>
    public string WindowTitle
    {
        get
        {
            string dir = string.IsNullOrEmpty(WorkspaceDir)
                ? "No Workspace"
                : Path.GetFileName(WorkspaceDir);
            string board = string.IsNullOrEmpty(CurrentBoardName) ? "Untitled" : CurrentBoardName;
            string mode = ModeService.CurrentMode.DisplayName;
            return $"{dir} - {board} - {mode}";
        }
    }

    /// <summary>Short mode label for the status bar, e.g. "Grid Mode".</summary>
    public string CurrentModeText => ModeService.CurrentMode.DisplayName;

    /// <summary>Status-bar mode indicator colour: red for Annotation, blue for Grid.</summary>
    public string ModeIndicatorColor => ModeService.IsAnnotationMode ? "#FF4444" : "#44AAFF";

    /// <summary>True when the custom canvas cursor icon should be shown.</summary>
    public bool IsCursorIconVisible => ModeService.IsAnnotationMode && IsPointerOverCanvas;

    /// <summary>
    /// Emoji/character displayed in the floating cursor icon overlay when in annotation mode.
    /// Maps the active tool to a representative Unicode character.
    /// </summary>
    public string CanvasCursor => CurrentTool switch
    {
        "Brush"     => "✏️",
        "Text"      => "T",
        "Arrow"     => "→",
        "Rectangle" => "▭",
        "Ellipse"   => "○",
        "Eraser"    => "◻",
        "Move"      => "✥",
        _           => "✏️"
    };

    // Grid background flags
    public bool IsGridBackgroundDots => GridBackgroundMode == "Dots";
    public bool IsGridBackgroundGrid => GridBackgroundMode == "Grid";
    public bool IsGridBackgroundNone => GridBackgroundMode == "None";

    // Annotation effect flags
    public bool IsAnnotationEffectNone => AnnotationEffectMode == "None";
    public bool IsAnnotationEffectShadow => AnnotationEffectMode == "Shadow";
    public bool IsAnnotationEffectOutline => AnnotationEffectMode == "Outline";

    // Tool selection flags (derived from CurrentTool via ModeService.AnnotationMode)
    public bool IsBrushSelected => CurrentTool == "Brush";
    public bool IsTextSelected => CurrentTool == "Text";
    public bool IsArrowSelected => CurrentTool == "Arrow";
    public bool IsRectangleSelected => CurrentTool == "Rectangle";
    public bool IsEllipseSelected => CurrentTool == "Ellipse";
    public bool IsEraserSelected => CurrentTool == "Eraser";
    public bool IsMoveSelected => CurrentTool == "Move";

    /// <summary>True when the eraser tool is active (convenience alias).</summary>
    public bool IsEraserMode => CurrentTool == "Eraser";

    /// <summary>True when the move tool is active (convenience alias).</summary>
    public bool IsMoveMode => CurrentTool == "Move";

    /// <summary>True when the recent boards list is non-empty.</summary>
    public bool HasRecentBoards => RecentBoards.Count > 0;

    /// <summary>True when the workspace directory contains at least one .cgrb file.</summary>
    public bool HasBoardFilesInDirectory => BoardFilesInDirectory.Count > 0;

    /// <summary>Application version string for the status bar.</summary>
    public string VersionText => $"v{Constants.AppVersion}";

    /// <summary>Current brush colour as a <see cref="SolidColorBrush"/> for XAML bindings.</summary>
    public SolidColorBrush CurrentBrushColorBrush => SolidColorBrush.Parse(CurrentBrushColor);

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="MainWindowViewModel"/>.
    /// </summary>
    /// <param name="isViewMode">
    /// Pass <c>true</c> when the application is launched with the <c>--view</c> flag
    /// to disable all mutating operations (undo, save, drag, etc.).
    /// </param>
    public MainWindowViewModel(bool isViewMode = false)
    {
        IsViewMode = isViewMode;
        ModeService = new ModeService();
        SelectionService = new SelectionService();
        Adorner = new TransformAdornerViewModel(SelectionService, ModeService);

        WorkspaceDir = Path.Combine(Constants.ConfigDirectory, "Assets");

        // ── Wire mode-change reactions ────────────────────────────────────────
        ModeService.ModeChanged += OnModeChanged;

        // When the annotation tool changes, re-notify all tool-derived properties.
        ModeService.AnnotationMode.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AnnotationMode.CurrentTool))
            {
                OnPropertyChanged(nameof(CurrentTool));
                OnPropertyChanged(nameof(CanvasCursor));
                OnPropertyChanged(nameof(IsBrushSelected));
                OnPropertyChanged(nameof(IsTextSelected));
                OnPropertyChanged(nameof(IsArrowSelected));
                OnPropertyChanged(nameof(IsRectangleSelected));
                OnPropertyChanged(nameof(IsEllipseSelected));
                OnPropertyChanged(nameof(IsEraserSelected));
                OnPropertyChanged(nameof(IsMoveSelected));
                OnPropertyChanged(nameof(IsEraserMode));
                OnPropertyChanged(nameof(IsMoveMode));
            }
        };

        // ── Wire collection → derived-property notifications ──────────────────
        RecentBoards.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentBoards));
        BoardFilesInDirectory.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasBoardFilesInDirectory));
    }

    // ── Mode-change handler ───────────────────────────────────────────────────

    private void OnModeChanged(object? sender, ModeChangedEventArgs e)
    {
        // Entering annotation mode: ensure the annotation layer is visible.
        if (e.NewMode is AnnotationMode)
            IsAnnotationsVisible = true;

        // Sync IsInDrawMode flag on all annotations so hit-testing is correct.
        bool isAnnotationMode = e.NewMode is AnnotationMode;
        foreach (var ann in Annotations)
            ann.IsInDrawMode = isAnnotationMode;

        // Clear selection when switching modes to avoid stale references.
        SelectionService.ClearSelection();

        // Notify all mode-derived computed properties.
        OnPropertyChanged(nameof(IsDrawMode));
        OnPropertyChanged(nameof(CurrentTool));
        OnPropertyChanged(nameof(CanvasCursor));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentModeText));
        OnPropertyChanged(nameof(ModeIndicatorColor));
        OnPropertyChanged(nameof(IsCursorIconVisible));
    }

    // ── CommunityToolkit.Mvvm partial method callbacks ────────────────────────

    /// <summary>
    /// Called by the source-generated <c>IsAnnotationsVisible</c> setter.
    /// Keeps the annotation layer's <see cref="LayerManager.Annotations"/> visibility in sync.
    /// </summary>
    partial void OnIsAnnotationsVisibleChanged(bool value)
    {
        LayerManager.Annotations.IsVisible = value;
    }

    /// <summary>
    /// Called by the source-generated <c>IsAlwaysOnTop</c> setter.
    /// Fires <see cref="AlwaysOnTopChanged"/> so the View can set <c>Window.Topmost</c>.
    /// </summary>
    partial void OnIsAlwaysOnTopChanged(bool value)
    {
        AlwaysOnTopChanged?.Invoke(value);
    }

    /// <summary>
    /// Called by the source-generated <c>GridBackgroundMode</c> setter.
    /// Persists the new value to user settings.
    /// </summary>
    partial void OnGridBackgroundModeChanged(string value)
    {
        SaveUserSettings();
    }

    /// <summary>
    /// Called by the source-generated <c>AnnotationEffectMode</c> setter.
    /// Applies the effect to the <see cref="AnnotationShape"/> renderer and persists settings.
    /// </summary>
    partial void OnAnnotationEffectModeChanged(string value)
    {
        AnnotationShape.SetEffectMode(value switch
        {
            "Shadow" => AnnotationEffect.Shadow,
            "Outline" => AnnotationEffect.Outline,
            _ => AnnotationEffect.None
        });
        SaveUserSettings();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Pops the most recent state from the undo stack and restores it.</summary>
    [RelayCommand]
    private void Undo()
    {
        if (_undoStack.Count <= 1 || IsViewMode) return;

        _isRestoringState = true;
        string current = _undoStack.Pop();
        _redoStack.Push(current);
        RestoreBoardState(_undoStack.Peek());
        SaveBoardData();
        ViewportUpdateRequested?.Invoke();
        ToastRequested?.Invoke("↩ Undo");
        _isRestoringState = false;
    }

    /// <summary>Re-applies the most recently undone state.</summary>
    [RelayCommand]
    private void Redo()
    {
        if (_redoStack.Count == 0 || IsViewMode) return;

        _isRestoringState = true;
        string next = _redoStack.Pop();
        _undoStack.Push(next);
        RestoreBoardState(next);
        SaveBoardData();
        ViewportUpdateRequested?.Invoke();
        ToastRequested?.Invoke("↪ Redo");
        _isRestoringState = false;
    }

    /// <summary>Switches the application to Grid layout mode.</summary>
    [RelayCommand]
    private void SwitchToGridMode() => ModeService.SetMode("Grid");

    /// <summary>Switches the application to Annotation drawing mode.</summary>
    [RelayCommand]
    private void SwitchToAnnotationMode() => ModeService.SetMode("Annotation");

    /// <summary>Sets the active annotation tool by name.</summary>
    [RelayCommand]
    private void SetAnnotationTool(string tool) => ModeService.AnnotationMode.CurrentTool = tool;

    /// <summary>Toggles the window always-on-top state.</summary>
    [RelayCommand]
    private void ToggleAlwaysOnTop() => IsAlwaysOnTop = !IsAlwaysOnTop;

    /// <summary>Toggles annotation layer visibility.</summary>
    [RelayCommand]
    private void ToggleAnnotationsVisible() => IsAnnotationsVisible = !IsAnnotationsVisible;

    /// <summary>Sets the grid background rendering mode ("Dots", "Grid", or "None").</summary>
    [RelayCommand]
    private void SetGridBackground(string mode) => GridBackgroundMode = mode;

    /// <summary>Sets the annotation rendering effect ("None", "Shadow", or "Outline").</summary>
    [RelayCommand]
    private void SetAnnotationEffect(string effect) => AnnotationEffectMode = effect;

    // ── Board state helpers ───────────────────────────────────────────────────

    /// <summary>Marks the board as having unsaved changes (idempotent).</summary>
    public void MarkUnsaved()
    {
        if (HasUnsavedChanges) return;
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Computes a fast integer hash of the current board state for change detection.
    /// Used to avoid pushing duplicate entries onto the undo stack.
    /// </summary>
    private string ComputeStateHash(
        IEnumerable<CellViewModel> cells,
        IEnumerable<AnnotationViewModel> annotations)
    {
        unchecked
        {
            int hash = 19;
            foreach (var c in cells)
            {
                hash = hash * 31 + c.CanvasX.GetHashCode();
                hash = hash * 31 + c.CanvasY.GetHashCode();
                hash = hash * 31 + c.ColSpan;
                hash = hash * 31 + c.RowSpan;
                hash = hash * 31 + c.Type.GetHashCode();
                hash = hash * 31 + (c.TextContent?.GetHashCode() ?? 0);
            }
            foreach (var a in annotations)
            {
                hash = hash * 31 + (a.Type?.GetHashCode() ?? 0);
                hash = hash * 31 + a.CanvasX.GetHashCode();
                hash = hash * 31 + a.CanvasY.GetHashCode();
                hash = hash * 31 + a.Points.Count;
                hash = hash * 31 + (a.Text?.GetHashCode() ?? 0);
            }
            return hash.ToString("X8");
        }
    }

    // ── Save / Undo stack ─────────────────────────────────────────────────────

    /// <summary>
    /// Serialises the current board state to disk and pushes it onto the undo stack.
    /// Concurrent calls are serialised via <see cref="_saveSemaphore"/> so writes never interleave.
    /// Uses a write-to-temp-then-rename pattern for atomic file replacement.
    /// </summary>
    public async void SaveBoardData()
    {
        if (string.IsNullOrEmpty(CurrentBoardFile)) return;

        string json = BoardSerializer.Serialize(GridCells, Annotations, CurrentBoardFile);

        // ── Undo stack management (synchronous, before async I/O) ─────────────
        if (!_isRestoringState && !IsViewMode)
        {
            string currentHash = ComputeStateHash(GridCells, Annotations);
            bool stackIsEmpty = _undoStack.Count == 0;
            bool jsonMatchesStack = !stackIsEmpty && _undoStack.Peek() == json;

            if (!jsonMatchesStack)
            {
                _undoStack.Push(json);
                _lastStateHash = currentHash;

                // Trim to prevent unbounded memory growth.
                if (_undoStack.Count > Constants.MaxUndoDepth)
                {
                    var items = _undoStack.ToArray(); // [newest … oldest]
                    _undoStack.Clear();
                    for (int i = Constants.MaxUndoDepth - 1; i >= 0; i--)
                        _undoStack.Push(items[i]);
                }

                _redoStack.Clear();
            }
            else
            {
                _lastStateHash = currentHash;
            }
        }
        else
        {
            _lastStateHash = ComputeStateHash(GridCells, Annotations);
        }

        // ── Atomic file write ─────────────────────────────────────────────────
        await _saveSemaphore.WaitAsync();
        try
        {
            string tempFile = CurrentBoardFile + ".tmp";
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, CurrentBoardFile, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Save error: {ex.Message}");
            ToastRequested?.Invoke("⚠️ Save failed — check disk space");
            return;
        }
        finally
        {
            _saveSemaphore.Release();
        }

        HasUnsavedChanges = false;
        AddRecentBoard(CurrentBoardFile);
    }

    // ── Board state restore (undo/redo) ───────────────────────────────────────

    /// <summary>
    /// Replaces the current board contents with the state described by <paramref name="json"/>.
    /// Updates cells/annotations in-place where possible to preserve image caches.
    /// </summary>
    public void RestoreBoardState(string json)
    {
        var (newCells, newAnnotations) = BoardSerializer.Deserialize(json, CurrentBoardFile);
        UpdateCellsInPlace(newCells);
        UpdateAnnotationsInPlace(newAnnotations);
        SelectionService.ClearSelection();
    }

    private void UpdateCellsInPlace(List<CellViewModel> newCells)
    {
        var toRemove = new List<CellViewModel>();

        foreach (var existing in GridCells)
        {
            var match = newCells.FirstOrDefault(c =>
                Math.Abs(c.CanvasX - existing.CanvasX) < 0.1 &&
                Math.Abs(c.CanvasY - existing.CanvasY) < 0.1 &&
                c.Type == existing.Type);

            if (match == null)
            {
                toRemove.Add(existing);
            }
            else
            {
                UpdateCellProperties(existing, match);
                newCells.Remove(match);
            }
        }

        foreach (var cell in toRemove) { cell.UnloadImage(); GridCells.Remove(cell); }
        foreach (var cell in newCells) GridCells.Add(cell);
    }

    private static void UpdateCellProperties(CellViewModel target, CellViewModel source)
    {
        target.ColSpan = source.ColSpan;
        target.RowSpan = source.RowSpan;
        target.TextContent = source.TextContent;
        target.BackgroundColor = source.BackgroundColor;
        target.ForegroundColor = source.ForegroundColor;
        target.FontSize = source.FontSize;
        target.ImageStretch = source.ImageStretch;
        target.PlaceholderColor = source.PlaceholderColor;

        string? oldFilePath = target.FilePath;
        string? newFilePath = source.FilePath;
        if (oldFilePath != newFilePath && !string.IsNullOrEmpty(newFilePath))
            target.SetImageDeferred(newFilePath);

        string? oldVideoPath = target.VideoPath;
        string? newVideoPath = source.VideoPath;
        if ((oldVideoPath != newVideoPath || oldFilePath != newFilePath) &&
            !string.IsNullOrEmpty(newVideoPath) && !string.IsNullOrEmpty(newFilePath))
            target.SetVideoDeferred(newVideoPath, newFilePath);
    }

    private void UpdateAnnotationsInPlace(List<AnnotationViewModel> newAnnotations)
    {
        var toRemove = new List<AnnotationViewModel>();

        foreach (var existing in Annotations)
        {
            var match = newAnnotations.FirstOrDefault(a =>
                Math.Abs(a.CanvasX - existing.CanvasX) < 0.1 &&
                Math.Abs(a.CanvasY - existing.CanvasY) < 0.1 &&
                a.Type == existing.Type &&
                a.Points.Count == existing.Points.Count);

            if (match == null)
            {
                toRemove.Add(existing);
            }
            else
            {
                existing.Text = match.Text;
                existing.Color = match.Color;
                existing.Thickness = match.Thickness;
                existing.Points.Clear();
                foreach (var p in match.Points) existing.Points.Add(p);
                newAnnotations.Remove(match);
            }
        }

        foreach (var ann in toRemove) Annotations.Remove(ann);
        foreach (var ann in newAnnotations)
        {
            ann.IsInDrawMode = ModeService.IsAnnotationMode;
            Annotations.Add(ann);
        }
    }

    // ── Board I/O ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a board from a .cgrb file and replaces the current board state.
    /// Reads the file before touching any state so that a read failure leaves
    /// the current board intact.
    /// </summary>
    public async void LoadBoardFromFile(string filePath)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load error (read): {ex.Message}");
            ToastRequested?.Invoke("⚠️ Could not open board file");
            return;
        }

        // Commit new file identity only after a successful read.
        CurrentBoardFile = filePath;
        WorkspaceDir = Path.GetDirectoryName(CurrentBoardFile)!;
        CurrentBoardName = Path.GetFileNameWithoutExtension(CurrentBoardFile);
        OnPropertyChanged(nameof(WindowTitle));
        UpdateBoardDirectoryList();

        // Signal the View to hide the startup overlay.
        StartupOverlayHideRequested?.Invoke();

        // Dispose existing bitmaps and clear caches before replacing the collection.
        foreach (var c in GridCells) c.UnloadImage();
        ImageManager.ClearCaches();

        SelectionService.ClearSelection();
        GridCells.Clear();
        Annotations.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        _lastStateHash = null;

        try
        {
            var (cells, annotations) = BoardSerializer.Deserialize(json, CurrentBoardFile);
            foreach (var cell in cells) GridCells.Add(cell);
            foreach (var ann in annotations)
            {
                ann.IsInDrawMode = ModeService.IsAnnotationMode;
                Annotations.Add(ann);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load error (deserialize): {ex.Message}");
            ToastRequested?.Invoke("⚠️ Board file is corrupt or unreadable");
            HasUnsavedChanges = false;
            return;
        }

        HasUnsavedChanges = false;
        AddRecentBoard(CurrentBoardFile);
        SaveBoardData();

        // Kick off background average-colour computation for cells that lack a saved colour.
        foreach (var cell in GridCells)
            if (cell.NeedsImage && cell.PlaceholderColor == "#FF2A2A2A")
                _ = cell.EnsurePlaceholderColorAsync();

        ViewportUpdateRequested?.Invoke();
    }

    // ── Recent boards ─────────────────────────────────────────────────────────

    /// <summary>Loads the recent boards list from disk into <see cref="RecentBoards"/>.</summary>
    public async void LoadRecentBoards()
    {
        string path = Path.Combine(Constants.ConfigDirectory, Constants.RecentBoardsFileName);
        if (!File.Exists(path)) return;

        try
        {
            string json = await File.ReadAllTextAsync(path);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null)
                foreach (var b in list.Where(File.Exists))
                    RecentBoards.Add(b);
        }
        catch { /* ignore corrupt recent boards file */ }
    }

    /// <summary>
    /// Adds <paramref name="path"/> to the top of <see cref="RecentBoards"/> and persists the list.
    /// Deduplicates and trims to <see cref="Constants.MaxRecentBoards"/>.
    /// </summary>
    public async void AddRecentBoard(string path)
    {
        if (RecentBoards.Contains(path)) RecentBoards.Remove(path);
        RecentBoards.Insert(0, path);
        while (RecentBoards.Count > Constants.MaxRecentBoards)
            RecentBoards.RemoveAt(RecentBoards.Count - 1);

        string confDir = Constants.ConfigDirectory;
        if (!Directory.Exists(confDir)) Directory.CreateDirectory(confDir);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(confDir, Constants.RecentBoardsFileName),
                JsonSerializer.Serialize(RecentBoards));
        }
        catch { /* non-critical */ }
    }

    /// <summary>
    /// Refreshes <see cref="BoardFilesInDirectory"/> from the current workspace directory.
    /// </summary>
    public async void UpdateBoardDirectoryList()
    {
        BoardFilesInDirectory.Clear();
        if (string.IsNullOrEmpty(WorkspaceDir) || !Directory.Exists(WorkspaceDir)) return;

        var currentFile = CurrentBoardFile;
        var workspaceDir = WorkspaceDir;
        var extension = Constants.DefaultBoardExtension;

        var files = await Task.Run(() =>
            Directory.GetFiles(workspaceDir, $"*{extension}")
                     .OrderBy(Path.GetFileName)
                     .ToList());

        foreach (var file in files)
        {
            BoardFilesInDirectory.Add(new BoardMenuItemViewModel
            {
                FileName = Path.GetFileName(file),
                IsActive = !string.IsNullOrEmpty(currentFile) &&
                           Path.GetFullPath(file) == Path.GetFullPath(currentFile)
            });
        }
    }

    // ── User settings ─────────────────────────────────────────────────────────

    /// <summary>Loads user preferences from disk and applies them to the ViewModel.</summary>
    public void LoadUserSettings()
    {
        try
        {
            string path = Path.Combine(Constants.ConfigDirectory, Constants.UserSettingsFileName);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            if (settings != null)
            {
                AnnotationEffectMode = settings.AnnotationEffect ?? "None";
                GridBackgroundMode = settings.GridBackground ?? "Dots";
            }
        }
        catch { /* ignore corrupt settings */ }
    }

    /// <summary>Persists current user preferences to disk asynchronously.</summary>
    public async void SaveUserSettings()
    {
        try
        {
            string confDir = Constants.ConfigDirectory;
            if (!Directory.Exists(confDir)) Directory.CreateDirectory(confDir);

            var settings = new UserSettings
            {
                AnnotationEffect = AnnotationEffectMode,
                GridBackground = GridBackgroundMode
            };

            await File.WriteAllTextAsync(
                Path.Combine(confDir, Constants.UserSettingsFileName),
                JsonSerializer.Serialize(settings));
        }
        catch { /* non-critical */ }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Releases unmanaged resources. Call from <c>Window.OnClosed</c>.
    /// </summary>
    /// <summary>
    /// Sets the active board file path and workspace directory after a "Save As" operation.
    /// Updates <see cref="CurrentBoardFile"/>, <see cref="WorkspaceDir"/>,
    /// <see cref="CurrentBoardName"/>, and refreshes the board directory list.
    /// </summary>
    public void SetBoardFilePath(string filePath)
    {
        CurrentBoardFile = filePath;
        WorkspaceDir = Path.GetDirectoryName(filePath)!;
        CurrentBoardName = Path.GetFileNameWithoutExtension(filePath);
        OnPropertyChanged(nameof(WindowTitle));
        UpdateBoardDirectoryList();
    }

    public void Cleanup()
    {
        _saveSemaphore.Dispose();
    }
}
