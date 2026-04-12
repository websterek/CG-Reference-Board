using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

/// <summary>
/// Main application window. Acts as its own DataContext (View-as-ViewModel pattern)
/// for the reference board, supporting grid-based image/video/text layout with
/// an annotation overlay, undo/redo, and pan/zoom navigation.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    #region INPC Support

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    #region Undo / Redo

    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private bool _isRestoringState;

    private void Undo()
    {
        if (_undoStack.Count <= 1 || _isViewMode)
            return;
        _isRestoringState = true;

        string current = _undoStack.Pop();
        _redoStack.Push(current);
        RestoreBoardState(_undoStack.Peek());
        SaveBoardData();

        _isRestoringState = false;
    }

    private void Redo()
    {
        if (_redoStack.Count == 0 || _isViewMode)
            return;
        _isRestoringState = true;

        string next = _redoStack.Pop();
        _undoStack.Push(next);
        RestoreBoardState(next);
        SaveBoardData();

        _isRestoringState = false;
    }

    /// <summary>
    /// Replaces the current board contents with the state described by the given JSON string.
    /// </summary>
    private void RestoreBoardState(string json)
    {
        GridCells.Clear();
        Annotations.Clear();
        _selectedAnnotations.Clear();
        _currentAnnotation = null;
        _editingTextAnnotation = null;

        var (cells, annotations) = BoardSerializer.Deserialize(json);
        foreach (var cell in cells)
            GridCells.Add(cell);
        foreach (var ann in annotations)
            Annotations.Add(ann);
    }

    #endregion

    #region Observable Collections

    public ObservableCollection<CellViewModel> GridCells { get; } = new();
    public ObservableCollection<AnnotationViewModel> Annotations { get; } = new();
    public ObservableCollection<string> RecentBoards { get; } = new();
    public ObservableCollection<BoardMenuItemViewModel> BoardFilesInDirectory { get; } = new();

    #endregion

    #region Bindable Properties

    private bool _isDrawMode;
    public bool IsDrawMode
    {
        get => _isDrawMode;
        set
        {
            if (_isDrawMode == value)
                return;
            _isDrawMode = value;
            // Clear any lingering selection when switching modes to avoid confusion
            ClearSelection();
            OnPropertyChanged(nameof(IsDrawMode));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(CurrentModeText));
            OnPropertyChanged(nameof(ModeIndicatorColor));
            OnPropertyChanged(nameof(IsCursorIconVisible));

            if (value)
                IsAnnotationsVisible = true;
        }
    }

    private bool _isMoveMode;
    public bool IsMoveMode
    {
        get => _isMoveMode;
        set { if (_isMoveMode != value) { _isMoveMode = value; OnPropertyChanged(nameof(IsMoveMode)); } }
    }

    private bool _isEraserMode;
    public bool IsEraserMode
    {
        get => _isEraserMode;
        set { if (_isEraserMode != value) { _isEraserMode = value; OnPropertyChanged(nameof(IsEraserMode)); } }
    }

    private bool _isAnnotationsVisible = true;
    public bool IsAnnotationsVisible
    {
        get => _isAnnotationsVisible;
        set { _isAnnotationsVisible = value; OnPropertyChanged(nameof(IsAnnotationsVisible)); }
    }

    private bool _isPointerOverCanvas;
    public bool IsPointerOverCanvas
    {
        get => _isPointerOverCanvas;
        set
        {
            if (_isPointerOverCanvas != value)
            {
                _isPointerOverCanvas = value;
                OnPropertyChanged(nameof(IsPointerOverCanvas));
                OnPropertyChanged(nameof(IsCursorIconVisible));
            }
        }
    }

    public bool IsCursorIconVisible => IsDrawMode && IsPointerOverCanvas;

    private bool _isAlwaysOnTop;
    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (_isAlwaysOnTop != value)
            {
                _isAlwaysOnTop = value;
                Topmost = value;
                OnPropertyChanged(nameof(IsAlwaysOnTop));
            }
        }
    }

    private string _currentBrushColor = "#FFFF4444";
    public string CurrentBrushColor
    {
        get => _currentBrushColor;
        set { _currentBrushColor = value; OnPropertyChanged(nameof(CurrentBrushColor)); }
    }

    private double _currentBrushThickness = 4.0;
    public double CurrentBrushThickness
    {
        get => _currentBrushThickness;
        set { _currentBrushThickness = value; OnPropertyChanged(nameof(CurrentBrushThickness)); }
    }

    private string _currentTool = "Pencil";
    public string CurrentTool
    {
        get => _currentTool;
        set
        {
            _currentTool = value;
            OnPropertyChanged(nameof(CurrentTool));
            OnPropertyChanged(nameof(CanvasCursor));
            OnPropertyChanged(nameof(IsPencilSelected));
            OnPropertyChanged(nameof(IsTextSelected));
            OnPropertyChanged(nameof(IsArrowSelected));
            OnPropertyChanged(nameof(IsRectangleSelected));
            OnPropertyChanged(nameof(IsEllipseSelected));
            OnPropertyChanged(nameof(IsEraserSelected));
            OnPropertyChanged(nameof(IsMoveSelected));
        }
    }

    private string _currentBoardName = Constants.AppName;
    public string CurrentBoardName
    {
        get => _currentBoardName;
        set { _currentBoardName = value; OnPropertyChanged(nameof(CurrentBoardName)); OnPropertyChanged(nameof(WindowTitle)); }
    }

    public string WindowTitle
    {
        get
        {
            string dir = string.IsNullOrEmpty(_workspaceDir) ? "No Workspace" : Path.GetFileName(_workspaceDir);
            string board = string.IsNullOrEmpty(CurrentBoardName) ? "Untitled" : CurrentBoardName;
            string mode = IsDrawMode ? "Annotation" : "Grid";
            return $"{dir} - {board} - {mode}";
        }
    }

    public bool IsViewMode => _isViewMode;
    public bool HasRecentBoards => RecentBoards.Count > 0;
    public bool HasBoardFilesInDirectory => BoardFilesInDirectory.Count > 0;

    /// <summary>Current zoom level as percentage string for the status bar.</summary>
    public string ZoomLevelText => $"{_scale.ScaleX * 100:F0}%";

    /// <summary>Application version string for the status bar.</summary>
    public string VersionText => $"v{Constants.AppVersion}";

    /// <summary>Number of currently selected items for the status bar.</summary>
    public string SelectionCountText
    {
        get
        {
            int count = _selectedCells.Count + _selectedAnnotations.Count;
            return count > 0 ? $"{count} selected" : "";
        }
    }

    /// <summary>Current mode display text for the status bar.</summary>
    public string CurrentModeText => IsDrawMode ? "Annotation" : "Grid";

    /// <summary>Mode indicator color for the status bar - red for Annotation, blue for Grid.</summary>
    public string ModeIndicatorColor => IsDrawMode ? "#FF4444" : "#44AAFF";

    /// <summary>Material icon kind for the canvas cursor based on the current tool.</summary>
    public string CanvasCursor
    {
        get
        {
            return CurrentTool switch
            {
                "Pencil" => "🖌️",      // Paintbrush emoji
                "Move" => "✥",         // Four teardrop-spoked asterisk
                "Text" => "T",         // Text
                "Arrow" => "→",        // Arrow
                "Rectangle" => "▭",    // Rectangle
                "Ellipse" => "○",      // Circle
                "Eraser" => "⌫",       // Erase symbol
                _ => "✏️"             // Pencil emoji default
            };
        }
    }

    /// <summary>Tool selection properties for menu checkmarks.</summary>
    public bool IsPencilSelected => CurrentTool == "Pencil";
    public bool IsTextSelected => CurrentTool == "Text";
    public bool IsArrowSelected => CurrentTool == "Arrow";
    public bool IsRectangleSelected => CurrentTool == "Rectangle";
    public bool IsEllipseSelected => CurrentTool == "Ellipse";
    public bool IsEraserSelected => CurrentTool == "Eraser";
    public bool IsMoveSelected => CurrentTool == "Move";

    #endregion

    #region Private State

    // Annotation drawing
    private AnnotationViewModel? _currentAnnotation;
    private readonly List<AnnotationViewModel> _selectedAnnotations = new();
    private bool _isDraggingAnnotations;
    private bool _isSelectingAnnotations;
    private Point _annotationSelectionStart;
    private Point _annotationDragStart;
    private AnnotationViewModel? _editingTextAnnotation;
    private string? _editingTextAnnotationOriginalText; // null = new annotation; string = original text of existing

    // Board file state
    private string _workspaceDir;
    private string _currentBoardFile = "";
    private bool _hasUnsavedChanges;
    private readonly bool _isViewMode;

    // Cell interaction
    private CellViewModel? _hoveredCell;
    private CellViewModel? _editingTextCell;
    private CellViewModel? _draggingCell;

    // Pan/Zoom
    private bool _isPanning;
    private Point _panStartPoint;
    private readonly TranslateTransform _translate = new(0, 0);
    private readonly ScaleTransform _scale = new(1, 1);

    // Middle-button drag-to-zoom (Nuke-style: middle + left click)
    private double _middleZoomStartY;
    private double _middleZoomOriginY;
    private bool _middleZoomActive;
    private Point _middleZoomAnchor;
    private bool _middleZoomAnchorSet;

    // Multi-selection
    private readonly List<CellViewModel> _selectedCells = new();
    private bool _isSelectingCells;
    private Point _cellSelectionStart;

    // Cell drag (single or group)
    private bool _isPointerDown;
    private Point _pointerDownPos;
    private PointerPressedEventArgs? _lastPressedEventArgs;
    private bool _isDraggingCell;
    private double _dragOffsetX;
    private double _dragOffsetY;
    private double _dragStartX;
    private double _dragStartY;
    private List<(CellViewModel Cell, double StartX, double StartY)>? _groupDragStarts;
    private List<(AnnotationViewModel Ann, double StartX, double StartY)>? _groupAnnotationDragStarts;

    // Cell resize
    private bool _isResizing;
    private Point _resizeStartPos;
    private CellViewModel? _resizingCell;

    #endregion

    #region Constructor

    /// <summary>Parameterless constructor required by Avalonia designer.</summary>
    public MainWindow() : this(false, null) { }

    public MainWindow(bool isViewMode, string? startFile)
    {
        DataContext = this;
        _isViewMode = isViewMode;
        InitializeComponent();

        LoadRecentBoards();
        RecentBoardsList.ItemsSource = RecentBoards;

        _workspaceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Constants.ConfigDirName, "Assets");
        if (!Directory.Exists(_workspaceDir))
            Directory.CreateDirectory(_workspaceDir);

        OnPropertyChanged(nameof(WindowTitle));
        CanvasGrid.ItemsSource = GridCells;

        // Set up pan/zoom transform
        var tg = new TransformGroup();
        tg.Children.Add(_translate);
        tg.Children.Add(_scale);

        var mainCanvas = this.FindControl<Canvas>("MainCanvas");
        if (mainCanvas != null)
            mainCanvas.RenderTransform = tg;

        // Initialize custom cursor icon position off-screen
        var cursorIcon = this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            Canvas.SetLeft(cursorIcon, -100);
            Canvas.SetTop(cursorIcon, -100);
        }

        // Wire up drag-drop
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

        // Auto-load a board passed via command line
        if (!string.IsNullOrEmpty(startFile) && File.Exists(startFile))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => LoadBoardFromFile(startFile));
        }
    }

    #endregion

    #region Board File I/O

    /// <summary>
    /// Loads a board from a .cgrb/.json file and replaces the current board state.
    /// </summary>
    private async void LoadBoardFromFile(string filePath)
    {
        try
        {
            _currentBoardFile = filePath;
            _workspaceDir = Path.GetDirectoryName(_currentBoardFile)!;
            CurrentBoardName = Path.GetFileNameWithoutExtension(_currentBoardFile);
            OnPropertyChanged(nameof(WindowTitle));
            UpdateBoardDirectoryList();

            var startupOverlay = this.FindControl<Border>("StartupOverlay");
            if (startupOverlay != null)
                startupOverlay.IsVisible = false;

            string json = await File.ReadAllTextAsync(_currentBoardFile);

            GridCells.Clear();
            Annotations.Clear();
            _selectedAnnotations.Clear();
            _currentAnnotation = null;
            _editingTextAnnotation = null;

            var (cells, annotations) = BoardSerializer.Deserialize(json);
            foreach (var cell in cells)
                GridCells.Add(cell);
            foreach (var ann in annotations)
                Annotations.Add(ann);

            _hasUnsavedChanges = false;
            Title = $"{Constants.AppName} - {Path.GetFileName(_currentBoardFile)}" + (_isViewMode ? " [VIEW MODE]" : "");
            AddRecentBoard(_currentBoardFile);

            _undoStack.Clear();
            _redoStack.Clear();
            SaveBoardData();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load error: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current board state to the active .cgrb file and pushes to undo stack.
    /// </summary>
    private async void SaveBoardData()
    {
        if (string.IsNullOrEmpty(_currentBoardFile))
            return;

        string json = BoardSerializer.Serialize(GridCells, Annotations);

        // Push to undo stack (unless restoring)
        if (!_isRestoringState && !_isViewMode)
        {
            if (_undoStack.Count == 0 || _undoStack.Peek() != json)
            {
                _undoStack.Push(json);
                _redoStack.Clear();
            }
        }

        await File.WriteAllTextAsync(_currentBoardFile, json);

        _hasUnsavedChanges = false;
        Title = $"{Constants.AppName} - {Path.GetFileName(_currentBoardFile)}" + (_isViewMode ? " [VIEW MODE]" : "");
        AddRecentBoard(_currentBoardFile);
    }

    private void MarkUnsaved()
    {
        if (_hasUnsavedChanges)
            return;
        _hasUnsavedChanges = true;
        Title = Constants.AppName
            + (string.IsNullOrEmpty(_currentBoardFile) ? "" : $" - {Path.GetFileName(_currentBoardFile)}")
            + " *";
    }

    #endregion

    #region Recent Boards

    private async void LoadRecentBoards()
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
        OnPropertyChanged(nameof(HasRecentBoards));
    }

    private async void AddRecentBoard(string path)
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

        OnPropertyChanged(nameof(HasRecentBoards));
    }

    private void UpdateBoardDirectoryList()
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
        OnPropertyChanged(nameof(HasBoardFilesInDirectory));
    }

    #endregion

    #region Grid Cell Helpers

    /// <summary>
    /// Finds or creates a cell at the grid position nearest to the given canvas point.
    /// </summary>
    private CellViewModel GetOrCreateCellAt(Point canvasPoint)
    {
        int gridX = (int)(Math.Floor(canvasPoint.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(canvasPoint.Y / Constants.GridSize) * Constants.GridSize);

        var existing = GridCells.FirstOrDefault(c => (int)c.CanvasX == gridX && (int)c.CanvasY == gridY);
        if (existing != null)
            return existing;

        var newCell = new CellViewModel { CanvasX = gridX, CanvasY = gridY };
        GridCells.Add(newCell);
        MarkUnsaved();
        return newCell;
    }

    /// <summary>
    /// Finds an existing content-layer cell (Image/Video/Text/None) at the grid position,
    /// or creates one. Board elements (Backdrop, Label) at the same position are ignored,
    /// allowing content to be placed on top of them.
    /// </summary>
    private CellViewModel GetOrCreateContentCellAt(Point canvasPoint)
    {
        int gridX = (int)(Math.Floor(canvasPoint.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(canvasPoint.Y / Constants.GridSize) * Constants.GridSize);

        var existing = GridCells.FirstOrDefault(c =>
            (int)c.CanvasX == gridX && (int)c.CanvasY == gridY && !c.IsBoardElement);
        if (existing != null)
            return existing;

        var newCell = new CellViewModel { CanvasX = gridX, CanvasY = gridY };
        GridCells.Add(newCell);
        MarkUnsaved();
        return newCell;
    }

    /// <summary>
    /// Returns the cell under the hover highlight, or creates one at the viewport center.
    /// </summary>
    private CellViewModel GetHighlightedCell()
    {
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight != null && hoverHighlight.IsVisible)
        {
            double left = Canvas.GetLeft(hoverHighlight);
            double top = Canvas.GetTop(hoverHighlight);
            if (!double.IsNaN(left) && !double.IsNaN(top))
                return GetOrCreateCellAt(new Point(left + 80, top + 80));
        }

        var bounds = CanvasGrid.Bounds;
        var centerPos = new Point(
            bounds.Width / 2 / _scale.ScaleX - _translate.X,
            bounds.Height / 2 / _scale.ScaleY - _translate.Y);
        return GetOrCreateCellAt(centerPos);
    }

    #endregion

    #region Image / Video Loading

    private async void LoadImageToCell(CellViewModel cell, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return;

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

        cell.SetImage(destPath);
        MarkUnsaved();
        SaveBoardData();
    }

    private async Task DownloadVideoToCell(CellViewModel cell, string url)
    {
        cell.SetText($"Downloading Video...\n{url}");
        cell.IsDownloading = true;

        string videoDir = Path.Combine(_workspaceDir, "videos");
        var result = await YtDlpService.DownloadVideoAsync(url, videoDir);

        cell.IsDownloading = false;

        if (result.Success)
        {
            cell.SetVideo(result.VideoPath!, result.ThumbnailPath!);
            MarkUnsaved();
            SaveBoardData();
        }
        else
        {
            cell.SetText($"Download Failed: {result.ErrorMessage}");
        }
    }

    #endregion

    #region Window Chrome Handlers

    private void TopBar_DoubleTapped(object? sender, TappedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            this.BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

    // Window resize grips
    private void TopLeft_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.NorthWest, e);
    private void Top_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.North, e);
    private void TopRight_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.NorthEast, e);
    private void Right_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.East, e);
    private void BottomRight_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.SouthEast, e);
    private void Bottom_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.South, e);
    private void BottomLeft_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.SouthWest, e);
    private void Left_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.West, e);

    #endregion

    #region Menu Click Handlers

    private void Undo_Click(object? sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object? sender, RoutedEventArgs e) => Redo();
    private void GridMode_Click(object? sender, RoutedEventArgs e) => IsDrawMode = false;
    private void AnnotationMode_Click(object? sender, RoutedEventArgs e) => IsDrawMode = true;
    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

    private async void SaveBoard_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var file = await topLevel!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {Constants.AppName}",
            DefaultExtension = Constants.DefaultBoardExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType($"{Constants.AppName} Data")
                {
                    Patterns = new[] { $"*{Constants.DefaultBoardExtension}", "*.json" }
                }
            }
        });

        if (file == null)
            return;

        _currentBoardFile = file.Path.LocalPath;
        _workspaceDir = Path.GetDirectoryName(_currentBoardFile)!;
        CurrentBoardName = Path.GetFileNameWithoutExtension(_currentBoardFile);
        OnPropertyChanged(nameof(WindowTitle));
        UpdateBoardDirectoryList();

        Directory.CreateDirectory(Path.Combine(_workspaceDir, "images"));
        Directory.CreateDirectory(Path.Combine(_workspaceDir, "videos"));

        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (startupOverlay != null)
            startupOverlay.IsVisible = false;

        SaveBoardData();
    }

    private async void LoadBoard_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Open {Constants.AppName}",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType($"{Constants.AppName} Data")
                {
                    Patterns = new[] { $"*{Constants.DefaultBoardExtension}", "*.json" }
                }
            }
        });

        if (files is { Count: > 0 })
            LoadBoardFromFile(files[0].Path.LocalPath);
    }

    private void NewBoard_Click(object? sender, RoutedEventArgs e)
    {
        _currentBoardFile = "";
        GridCells.Clear();
        Annotations.Clear();
        CurrentBoardName = "New Board";
        _hasUnsavedChanges = false;
        Title = Constants.AppName;

        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (startupOverlay != null)
            startupOverlay.IsVisible = false;
    }

    private async void ImportMedia_Click(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Import Media",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images & Videos")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp",
                                       "*.mp4", "*.webm", "*.avi", "*.mov", "*.mkv" }
                }
            }
        };

        var files = await this.StorageProvider.OpenFilePickerAsync(options);
        if (files == null || files.Count == 0)
            return;

        string[] videoExtensions = { ".mp4", ".webm", ".avi", ".mov", ".mkv" };
        int startX = 0, startY = 0;

        foreach (var file in files)
        {
            int x = startX, y = startY;

            // Find next available grid slot
            while (GridCells.Any(c => (int)c.CanvasX == x && (int)c.CanvasY == y))
            {
                x += (int)Constants.GridSize;
                if (x > 1600)
                { x = startX; y += (int)Constants.GridSize; }
            }

            var cell = new CellViewModel { CanvasX = x, CanvasY = y };
            GridCells.Add(cell);

            string ext = Path.GetExtension(file.Path.LocalPath).ToLowerInvariant();
            if (videoExtensions.Contains(ext))
            {
                string destDir = Path.Combine(_workspaceDir, "videos");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Path.GetFileName(file.Path.LocalPath));
                if (file.Path.LocalPath != destPath && !File.Exists(destPath))
                    File.Copy(file.Path.LocalPath, destPath);
                cell.SetVideo(destPath, destPath);
                MarkUnsaved();
                SaveBoardData();
            }
            else
            {
                LoadImageToCell(cell, file.Path.LocalPath);
            }
        }
    }

    private void BoardDir_Click(object? sender, RoutedEventArgs e)
        => PlatformHelper.OpenDirectory(_workspaceDir);

    private void BoardMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is MenuItem item && item.DataContext is BoardMenuItemViewModel vm)
        {
            var path = Path.Combine(_workspaceDir, vm.FileName);
            if (File.Exists(path))
            {
                _currentBoardFile = path;
                CurrentBoardName = Path.GetFileNameWithoutExtension(path);
                LoadBoardFromFile(path);
            }
        }
    }

    private void RecentBoard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is string path)
            LoadBoardFromFile(path);
    }

    #endregion

    #region Annotation Tool Mode Handlers

    private void PencilMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Pencil"; IsEraserMode = false; IsMoveMode = false; }

    private void TextMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Text"; IsEraserMode = false; IsMoveMode = false; }

    private void ArrowMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Arrow"; IsEraserMode = false; IsMoveMode = false; }

    private void SquareMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Rectangle"; IsEraserMode = false; IsMoveMode = false; }

    private void CircleMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Ellipse"; IsEraserMode = false; IsMoveMode = false; }

    private void EraserMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Eraser"; IsEraserMode = true; IsMoveMode = false; }

    private void MoveMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Move"; IsMoveMode = true; IsEraserMode = false; }

    #endregion

    #region Cell Context Menu Handlers

    private async void CopyImage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel { FilePath: not null } cell })
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null || !System.IO.File.Exists(cell.FilePath))
            return;

        try
        {
            using var stream = System.IO.File.OpenRead(cell.FilePath);
            var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            var dt = new DataTransfer();
            var item = new DataTransferItem();
            item.SetBitmap(bitmap);
            dt.Add(item);
            await clipboard.SetDataAsync(dt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy image: {ex.Message}");
        }
    }

    private async void CopyText_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel cell } && !string.IsNullOrEmpty(cell.TextContent))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            var dt = new DataTransfer();
            var item = new DataTransferItem();
            item.SetText(cell.TextContent);
            dt.Add(item);
            await clipboard.SetDataAsync(dt);
        }
    }

    private async void CopyPath_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { FilePath: not null } cell })
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            var dt = new DataTransfer();
            var item = new DataTransferItem();
            item.SetText(cell.FilePath);
            dt.Add(item);
            await clipboard.SetDataAsync(dt);
        }
    }

    private void ShowInExplorer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel cell })
            PlatformHelper.ShowInFileExplorer(cell.VideoPath ?? cell.FilePath ?? "");
    }

    private void ChangeColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell })
            return;

        if (cell.IsBackdrop)
        {
            string[] bgColors = { "#88222222", "#885A3A10", "#881A3A4A", "#881A4A2A", "#884A1A2A", "#88444444" };
            string[] fgColors = { "#AAFFFFFF", "#FFFFA500", "#FF44AAFF", "#FF66FF66", "#FFFF6666", "#FFFFFF66" };
            int idx = Array.IndexOf(bgColors, cell.BackgroundColor);
            int next = (idx + 1) % bgColors.Length;
            cell.BackgroundColor = bgColors[next];
            cell.ForegroundColor = fgColors[next];
        }
        else if (cell.IsLabel)
        {
            string[] colors = { "#FFFFA500", "#FFFFFFFF", "#FF44AAFF", "#FFFF6666", "#FF66FF66", "#FFFFFF66" };
            int idx = Array.IndexOf(colors, cell.ForegroundColor);
            cell.ForegroundColor = colors[(idx + 1) % colors.Length];
        }

        MarkUnsaved();
        SaveBoardData();
    }

    private void ToggleImageFit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsImage: true } cell })
        {
            cell.ImageStretch = cell.ImageStretch == "UniformToFill" ? "Uniform" : "UniformToFill";
            MarkUnsaved();
            SaveBoardData();
        }
    }

    private void IncreaseFontSize_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsLabel: true } cell })
        {
            cell.FontSize += 8;
            MarkUnsaved();
            SaveBoardData();
        }
    }

    private void DecreaseFontSize_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsLabel: true } cell } && cell.FontSize > 16)
        {
            cell.FontSize -= 8;
            MarkUnsaved();
            SaveBoardData();
        }
    }

    private void FitToContent_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell })
            return;
        if (!cell.IsImage && !cell.IsVideo)
            return; // Only works for images/videos
        if (string.IsNullOrEmpty(cell.FilePath))
            return;

        // Get image dimensions
        var dimensions = GetImageDimensions(cell.FilePath);
        if (dimensions == null)
            return;

        // Calculate optimal size
        var (newColSpan, newRowSpan) = CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);

        // Check if new size would cause collision
        if (!IsSpaceEmpty(cell.CanvasX, cell.CanvasY, newColSpan, newRowSpan, cell.CollisionLayer, excludeCell: cell))
        {
            ShakeScreen();
            return;
        }

        // Resize cell
        cell.ColSpan = newColSpan;
        cell.RowSpan = newRowSpan;

        MarkUnsaved();
        SaveBoardData();
    }

    private void DeleteCell_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;
        if (sender is not MenuItem { DataContext: CellViewModel cell })
            return;

        // Clean up files on disk
        if (cell.FilePath != null && File.Exists(cell.FilePath))
            try
            { File.Delete(cell.FilePath); }
            catch { /* non-critical */ }
        if (cell.VideoPath != null && File.Exists(cell.VideoPath))
            try
            { File.Delete(cell.VideoPath); }
            catch { /* non-critical */ }

        cell.Clear();
        GridCells.Remove(cell);
        MarkUnsaved();
        SaveBoardData();
    }

    #endregion

    #region Add Content (Context Menu)

    private void AddText_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight == null)
            return;

        double x = Canvas.GetLeft(hoverHighlight);
        double y = Canvas.GetTop(hoverHighlight);

        var newCell = new CellViewModel { CanvasX = x, CanvasY = y, ColSpan = 2, RowSpan = 2 };
        newCell.Type = CellType.Text;
        newCell.SetText("New Text Block");

        GridCells.Add(newCell);
        MarkUnsaved();
        SaveBoardData();
    }

    private void AddLabel_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight == null)
            return;

        double x = Canvas.GetLeft(hoverHighlight);
        double y = Canvas.GetTop(hoverHighlight);

        var newCell = new CellViewModel { CanvasX = x, CanvasY = y, ColSpan = 4, RowSpan = 2 };
        newCell.Type = CellType.Label;
        newCell.SetText("New Label");

        GridCells.Add(newCell);
        MarkUnsaved();
        SaveBoardData();
    }

    private void AddBackdrop_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;

        if (_selectedCells.Count > 0)
        {
            // Create backdrop around selected cells
            double minX = _selectedCells.Min(c => c.CanvasX);
            double minY = _selectedCells.Min(c => c.CanvasY);
            double maxX = _selectedCells.Max(c => c.CanvasX + c.PixelWidth);
            double maxY = _selectedCells.Max(c => c.CanvasY + c.PixelHeight);

            // Snap to grid
            int gridX = (int)(Math.Floor(minX / Constants.GridSize) * Constants.GridSize);
            int gridY = (int)(Math.Floor(minY / Constants.GridSize) * Constants.GridSize);

            // Calculate size (add padding)
            double width = maxX - gridX + Constants.BackdropPadding;
            double height = maxY - gridY + Constants.BackdropPadding;

            int colSpan = (int)Math.Ceiling(width / Constants.GridSize);
            int rowSpan = (int)Math.Ceiling(height / Constants.GridSize);

            var backdrop = new CellViewModel
            {
                CanvasX = gridX,
                CanvasY = gridY,
                ColSpan = colSpan,
                RowSpan = rowSpan,
                Type = CellType.Backdrop,
                TextContent = "Backdrop"
            };

            GridCells.Add(backdrop);
            MarkUnsaved();
            SaveBoardData();
        }
        else
        {
            // Original behavior: create empty backdrop at mouse position
            var hoverHighlight = this.FindControl<Border>("HoverHighlight");
            if (hoverHighlight == null)
                return;

            double x = Canvas.GetLeft(hoverHighlight);
            double y = Canvas.GetTop(hoverHighlight);

            var newCell = new CellViewModel { CanvasX = x, CanvasY = y, ColSpan = 6, RowSpan = 4 };
            newCell.Type = CellType.Backdrop;
            newCell.SetText("New Backdrop");

            GridCells.Add(newCell);
            MarkUnsaved();
            SaveBoardData();
        }
    }

    private void ArrangeSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCells.Count == 0)
            return;

        // Find the topmost-leftmost position of selected cells
        double minX = _selectedCells.Min(c => c.CanvasX);
        double minY = _selectedCells.Min(c => c.CanvasY);

        // Sort cells by position (top-to-bottom, left-to-right)
        var sortedCells = _selectedCells.OrderBy(c => c.CanvasY).ThenBy(c => c.CanvasX).ToList();

        // Track old positions for moving annotations
        var oldPositions = new Dictionary<CellViewModel, Point>();
        foreach (var cell in sortedCells)
        {
            oldPositions[cell] = new Point(cell.CanvasX, cell.CanvasY);
        }

        // Arrange in a compact grid starting at (minX, minY)
        int itemsPerRow = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(sortedCells.Count)));
        double currentX = minX;
        double currentY = minY;
        double maxRowHeight = 0;
        int itemsInCurrentRow = 0;

        foreach (var cell in sortedCells)
        {
            // Find empty space near desired position
            var emptySpace = FindEmptySpace(currentX, currentY, cell.ColSpan, cell.RowSpan, cell.CollisionLayer, excludeCell: cell);

            if (emptySpace != null)
            {
                // Move cell to new position
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
            }

            // Track row height
            maxRowHeight = Math.Max(maxRowHeight, cell.PixelHeight);

            // Move to next position
            currentX += cell.PixelWidth; // No spacing - tidy layout
            itemsInCurrentRow++;

            // Move to next row if needed
            if (itemsInCurrentRow >= itemsPerRow)
            {
                currentX = minX;
                currentY += maxRowHeight; // No spacing - tidy layout
                maxRowHeight = 0;
                itemsInCurrentRow = 0;
            }
        }

        // Move annotations with their cells
        MoveAnnotationsWithCells(oldPositions);

        MarkUnsaved();
        SaveBoardData();
    }

    private void ArrangeHorizontal_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCells.Count == 0)
            return;

        // Find the leftmost position and tallest cell
        double minX = _selectedCells.Min(c => c.CanvasX);
        double minY = _selectedCells.Min(c => c.CanvasY);
        double maxHeight = _selectedCells.Max(c => c.PixelHeight);

        // Sort cells by X position (left to right)
        var sortedCells = _selectedCells.OrderBy(c => c.CanvasX).ThenBy(c => c.CanvasY).ToList();

        // Track old positions for moving annotations
        var oldPositions = new Dictionary<CellViewModel, Point>();
        foreach (var cell in sortedCells)
        {
            oldPositions[cell] = new Point(cell.CanvasX, cell.CanvasY);
        }

        // Arrange in a horizontal row, matching height to tallest
        double currentX = minX;

        foreach (var cell in sortedCells)
        {
            // Find empty space at desired position
            var emptySpace = FindEmptySpace(currentX, minY, cell.ColSpan, cell.RowSpan, cell.CollisionLayer, cell);

            if (emptySpace != null)
            {
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
            }

            // Move to next position
            currentX += cell.PixelWidth; // No spacing - tidy layout
        }

        // Move annotations with their cells
        MoveAnnotationsWithCells(oldPositions);

        MarkUnsaved();
        SaveBoardData();
    }

    private void ArrangeVertical_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCells.Count == 0)
            return;

        // Find the topmost position and widest cell
        double minX = _selectedCells.Min(c => c.CanvasX);
        double minY = _selectedCells.Min(c => c.CanvasY);
        double maxWidth = _selectedCells.Max(c => c.PixelWidth);

        // Sort cells by Y position (top to bottom)
        var sortedCells = _selectedCells.OrderBy(c => c.CanvasY).ThenBy(c => c.CanvasX).ToList();

        // Track old positions for moving annotations
        var oldPositions = new Dictionary<CellViewModel, Point>();
        foreach (var cell in sortedCells)
        {
            oldPositions[cell] = new Point(cell.CanvasX, cell.CanvasY);
        }

        // Arrange in a vertical column
        double currentY = minY;

        foreach (var cell in sortedCells)
        {
            // Find empty space at desired position
            var emptySpace = FindEmptySpace(minX, currentY, cell.ColSpan, cell.RowSpan, cell.CollisionLayer, cell);

            if (emptySpace != null)
            {
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
            }

            // Move to next position
            currentY += cell.PixelHeight; // No spacing - tidy layout
        }

        // Move annotations with their cells
        MoveAnnotationsWithCells(oldPositions);

        MarkUnsaved();
        SaveBoardData();
    }

    /// <summary>
    /// Helper method to move annotations that were inside cells after the cells moved.
    /// </summary>
    private void MoveAnnotationsWithCells(Dictionary<CellViewModel, Point> oldPositions)
    {
        foreach (var cell in oldPositions.Keys)
        {
            var oldPos = oldPositions[cell];
            double deltaX = cell.CanvasX - oldPos.X;
            double deltaY = cell.CanvasY - oldPos.Y;

            // Skip if cell didn't move
            if (Math.Abs(deltaX) < 0.1 && Math.Abs(deltaY) < 0.1)
                continue;

            // Find annotations that were inside this cell's OLD bounds
            var cellRect = new Rect(oldPos.X, oldPos.Y, cell.PixelWidth, cell.PixelHeight);

            foreach (var annotation in Annotations.ToList())
            {
                // Check if annotation's first point was inside the cell
                if (annotation.Points.Count > 0)
                {
                    var pt = annotation.Points[0];
                    if (cellRect.Contains(pt))
                    {
                        // Update annotation canvas position (only modify CanvasX/Y, not Points)
                        annotation.CanvasX += deltaX;
                        annotation.CanvasY += deltaY;
                    }
                }
            }
        }
    }

    #endregion

    /// <summary>
    /// Checks whether a rectangle on a given collision layer overlaps any existing cell
    /// on that same layer, optionally excluding one cell (the one being moved/resized).
    /// Different layers may freely overlap (e.g. content over backdrops, labels over content).
    /// </summary>
    private bool HasLayerCollision(int layer, CellViewModel? exclude,
        double x, double y, int cols, int rows)
    {
        double right = x + cols * Constants.GridSize;
        double bottom = y + rows * Constants.GridSize;

        return GridCells.Any(c =>
        {
            if (c == exclude || !c.HasContent || c.CollisionLayer != layer)
                return false;

            // Backdrops get a half-grid margin to create visual spacing
            double margin = c.IsBackdrop ? Constants.GridSize / 2.0 : 0;
            double cellLeft = c.CanvasX - margin;
            double cellRight = c.CanvasX + c.ColSpan * Constants.GridSize + margin;
            double cellTop = c.CanvasY - margin;
            double cellBottom = c.CanvasY + c.RowSpan * Constants.GridSize + margin;

            return cellLeft < right && cellRight > x && cellTop < bottom && cellBottom > y;
        });
    }

    /// <summary>
    /// Overload that excludes a set of cells (for group-move collision checks).
    /// </summary>
    private bool HasLayerCollision(int layer, IEnumerable<CellViewModel> excludeSet,
        double x, double y, int cols, int rows)
    {
        double right = x + cols * Constants.GridSize;
        double bottom = y + rows * Constants.GridSize;

        return GridCells.Any(c =>
        {
            if (excludeSet.Contains(c) || !c.HasContent || c.CollisionLayer != layer)
                return false;

            // Backdrops get a half-grid margin to create visual spacing
            double margin = c.IsBackdrop ? Constants.GridSize / 2.0 : 0;
            double cellLeft = c.CanvasX - margin;
            double cellRight = c.CanvasX + c.ColSpan * Constants.GridSize + margin;
            double cellTop = c.CanvasY - margin;
            double cellBottom = c.CanvasY + c.RowSpan * Constants.GridSize + margin;

            return cellLeft < right && cellRight > x && cellTop < bottom && cellBottom > y;
        });
    }

    /// <summary>Deselects all cells and annotations, clears both selection lists.</summary>
    private void ClearSelection()
    {
        foreach (var c in _selectedCells)
            c.IsSelected = false;
        _selectedCells.Clear();
        foreach (var a in _selectedAnnotations)
            a.IsSelected = false;
        _selectedAnnotations.Clear();
        OnPropertyChanged(nameof(SelectionCountText));
    }

    /// <summary>Checks if moving an entire group by (dx, dy) grid-pixels causes any same-layer collision.</summary>
    private bool HasGroupCollision(IReadOnlyList<CellViewModel> group, double dx, double dy)
    {
        foreach (var cell in group)
        {
            double newX = cell.CanvasX + dx;
            double newY = cell.CanvasY + dy;
            bool collision = HasLayerCollision(cell.CollisionLayer, group, newX, newY, cell.ColSpan, cell.RowSpan);
            if (collision)
                return true;
        }
        return false;
    }

    #region Cell Pointer Handlers (Drag & Resize)

    private void Cell_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: CellViewModel cell })
            _hoveredCell = cell;
    }

    private void Cell_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: CellViewModel cell } && _hoveredCell == cell)
            _hoveredCell = null;
    }

    private void Cell_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsDrawMode || e.Handled || _isViewMode)
            return;

        if (sender is not Border { DataContext: CellViewModel cell })
            return;
        var props = e.GetCurrentPoint(this).Properties;

        // Alt+Drag: Duplicate cell
        if (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            // Find empty space near the cell
            var emptySpace = FindEmptySpace(
                cell.CanvasX + Constants.GridSize,
                cell.CanvasY + Constants.GridSize,
                cell.ColSpan,
                cell.RowSpan,
                cell.CollisionLayer
            );

            if (emptySpace == null)
            {
                ShakeScreen();
                e.Handled = true;
                return;
            }

            // Create duplicate
            var duplicate = new CellViewModel
            {
                CanvasX = emptySpace.Value.X,
                CanvasY = emptySpace.Value.Y,
                ColSpan = cell.ColSpan,
                RowSpan = cell.RowSpan,
                Type = cell.Type,
                BackgroundColor = cell.BackgroundColor,
                ForegroundColor = cell.ForegroundColor,
                ImageStretch = cell.ImageStretch,
                FontSize = cell.FontSize,
                TextContent = cell.TextContent
            };

            // Copy file content if applicable
            if (cell.IsImage || cell.IsVideo)
            {
                duplicate.FilePath = cell.FilePath;
                duplicate.VideoPath = cell.VideoPath;
                duplicate.Image = cell.Image;
            }

            GridCells.Add(duplicate);
            MarkUnsaved();
            SaveBoardData();

            e.Handled = true;
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && sender is Control { DataContext: CellViewModel { HasContent: true } })
        {
            // If clicking a backdrop, handle selection behavior
            if (cell.IsBackdrop)
            {
                bool isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);

                if (isCtrlPressed)
                {
                    // Ctrl+Click on backdrop: just toggle the backdrop itself
                    cell.IsSelected = !cell.IsSelected;
                    if (cell.IsSelected)
                        _selectedCells.Add(cell);
                    else
                        _selectedCells.Remove(cell);

                    OnPropertyChanged(nameof(SelectionCountText));
                }
                else if (!cell.IsSelected)
                {
                    // Plain click on unselected backdrop: clear selection and select only the backdrop
                    ClearSelection();
                    cell.IsSelected = true;
                    _selectedCells.Add(cell);
                    OnPropertyChanged(nameof(SelectionCountText));
                }
                // else: Plain click on already-selected backdrop - keep current selection (for dragging)

                _isPointerDown = true;
                _pointerDownPos = e.GetPosition(this);
                _lastPressedEventArgs = e;
                e.Handled = true;
                return;
            }

            // If clicking a regular cell (not backdrop) that's not already selected and not Ctrl+clicking
            if (!cell.IsBackdrop && !cell.IsSelected && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Clear current selection
                ClearSelection();

                // Select the clicked cell
                cell.IsSelected = true;
                _selectedCells.Add(cell);

                OnPropertyChanged(nameof(SelectionCountText));
            }

            bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (isCtrl)
            {
                // Ctrl+Click: toggle this cell's selection
                cell.IsSelected = !cell.IsSelected;
                if (cell.IsSelected)
                    _selectedCells.Add(cell);
                else
                    _selectedCells.Remove(cell);

                OnPropertyChanged(nameof(SelectionCountText));
            }
            else
            {
                // Plain click: if the cell is already selected (part of a group),
                // keep the whole selection so the user can drag the group.
                // Otherwise, select only this cell.
                if (!cell.IsSelected)
                {
                    ClearSelection();
                    cell.IsSelected = true;
                    _selectedCells.Add(cell);
                }
            }

            _isPointerDown = true;
            _pointerDownPos = e.GetPosition(this);
            _lastPressedEventArgs = e;
            e.Handled = true;
        }
    }

    private void Cell_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (IsDrawMode || e.Handled || _isViewMode)
            return;

        if (!_isPointerDown || _lastPressedEventArgs == null)
            return;
        if (sender is not Control { DataContext: CellViewModel { HasContent: true } cell })
            return;

        if (!_isDraggingCell)
        {
            var pt = e.GetPosition(this);
            if (Math.Abs(pt.X - _pointerDownPos.X) > Constants.DragThreshold
                || Math.Abs(pt.Y - _pointerDownPos.Y) > Constants.DragThreshold)
            {
                _isDraggingCell = true;
                _draggingCell = cell;

                // Gather all contents of selected backdrops for dragging
                var cellsToMove = new List<CellViewModel>(_selectedCells);
                var annotationsToMove = new List<AnnotationViewModel>();

                // First, gather cells from backdrops
                foreach (var backdrop in _selectedCells.Where(c => c.IsBackdrop).ToList())
                {
                    double left = backdrop.CanvasX;
                    double top = backdrop.CanvasY;
                    double right = left + backdrop.ColSpan * Constants.GridSize;
                    double bottom = top + backdrop.RowSpan * Constants.GridSize;

                    // Add all cells within this backdrop's bounds
                    foreach (var c in GridCells)
                    {
                        if (!c.HasContent || cellsToMove.Contains(c))
                            continue;

                        double cx = c.CanvasX;
                        double cy = c.CanvasY;
                        double cw = c.ColSpan * Constants.GridSize;
                        double ch = c.RowSpan * Constants.GridSize;

                        bool intersects = cx < right && cx + cw > left
                                       && cy < bottom && cy + ch > top;
                        if (intersects)
                            cellsToMove.Add(c);
                    }
                }

                // Now gather all annotations that are within ANY cell being moved
                foreach (var cellToMove in cellsToMove)
                {
                    double left = cellToMove.CanvasX;
                    double top = cellToMove.CanvasY;
                    double right = left + cellToMove.ColSpan * Constants.GridSize;
                    double bottom = top + cellToMove.RowSpan * Constants.GridSize;

                    foreach (var ann in Annotations)
                    {
                        if (annotationsToMove.Contains(ann))
                            continue;

                        bool inRect = ann.Points.Any(p =>
                        {
                            double px = p.X + ann.CanvasX;
                            double py = p.Y + ann.CanvasY;
                            return px >= left && px <= right && py >= top && py <= bottom;
                        });

                        if (inRect)
                            annotationsToMove.Add(ann);
                    }
                }

                // Record start positions for ALL cells and annotations to move (group drag)
                bool isGroupDrag = (cellsToMove.Count + annotationsToMove.Count) > 1
                                   && cellsToMove.Contains(cell);
                if (isGroupDrag)
                {
                    _groupDragStarts = cellsToMove
                        .Select(c => (c, c.CanvasX, c.CanvasY)).ToList();
                    _groupAnnotationDragStarts = annotationsToMove
                        .Select(a => (a, a.CanvasX, a.CanvasY)).ToList();
                }
                else
                {
                    _groupDragStarts = null;
                    _groupAnnotationDragStarts = null;
                }

                _dragStartX = cell.CanvasX;
                _dragStartY = cell.CanvasY;

                var canvasPt = e.GetPosition(CanvasGrid);
                _dragOffsetX = canvasPt.X - cell.CanvasX;
                _dragOffsetY = canvasPt.Y - cell.CanvasY;
                e.Pointer.Capture(sender as Control);
            }
        }
        else if (_groupDragStarts != null && (_groupDragStarts.Count + (_groupAnnotationDragStarts?.Count ?? 0)) > 1)
        {
            // Group drag: compute incremental grid-snapped delta from the primary cell's
            // CURRENT position (not original), so movement is always one grid step at a time.
            var canvasPt = e.GetPosition(CanvasGrid);
            double targetX = Math.Round((canvasPt.X - _dragOffsetX) / Constants.GridSize) * Constants.GridSize;
            double targetY = Math.Round((canvasPt.Y - _dragOffsetY) / Constants.GridSize) * Constants.GridSize;
            double currentX = _draggingCell?.CanvasX ?? _dragStartX;
            double currentY = _draggingCell?.CanvasY ?? _dragStartY;
            double dx = targetX - currentX;
            double dy = targetY - currentY;

            // Clamp to at most one grid step per frame in each axis
            if (Math.Abs(dx) > Constants.GridSize)
                dx = Math.Sign(dx) * Constants.GridSize;
            if (Math.Abs(dy) > Constants.GridSize)
                dy = Math.Sign(dy) * Constants.GridSize;

            // Only move if there's actual delta and no collision
            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
            {
                // Get all cells that will be moved (from _groupDragStarts)
                var cellsToMove = _groupDragStarts.Select(s => s.Cell).ToList();
                bool collision = HasGroupCollision(cellsToMove, dx, dy);

                if (!collision)
                {
                    // Move all cells in the group (including backdrop contents)
                    foreach (var (c, _, _) in _groupDragStarts)
                    {
                        c.CanvasX += dx;
                        c.CanvasY += dy;
                    }
                    // Move all annotations in the group
                    if (_groupAnnotationDragStarts != null)
                    {
                        foreach (var (a, _, _) in _groupAnnotationDragStarts)
                        {
                            a.CanvasX += dx;
                            a.CanvasY += dy;
                        }
                    }
                }
            }
        }
        else
        {
            // Single cell drag
            var canvasPt = e.GetPosition(CanvasGrid);
            double newX = Math.Round((canvasPt.X - _dragOffsetX) / Constants.GridSize) * Constants.GridSize;
            double newY = Math.Round((canvasPt.Y - _dragOffsetY) / Constants.GridSize) * Constants.GridSize;

            if (!HasLayerCollision(cell.CollisionLayer, cell, newX, newY, cell.ColSpan, cell.RowSpan))
            {
                cell.CanvasX = newX;
                cell.CanvasY = newY;
            }
        }
    }

    private void Cell_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsDrawMode)
            return;

        if (_isDraggingCell && sender is Control)
        {
            // Safety rollback for single drag
            if (_draggingCell != null && HasLayerCollision(_draggingCell.CollisionLayer, _draggingCell,
                    _draggingCell.CanvasX, _draggingCell.CanvasY,
                    _draggingCell.ColSpan, _draggingCell.RowSpan))
            {
                _draggingCell.CanvasX = _dragStartX;
                _draggingCell.CanvasY = _dragStartY;
            }

            e.Pointer.Capture(null);
            _isDraggingCell = false;
            _draggingCell = null;
            _groupDragStarts = null;
            _groupAnnotationDragStarts = null;
            MarkUnsaved();
            SaveBoardData();
        }
        _isPointerDown = false;
        OnPropertyChanged(nameof(SelectionCountText));
    }

    private void ResizeThumb_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsDrawMode || _isViewMode)
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && sender is Control c && c.DataContext is CellViewModel cell)
        {
            _isResizing = true;
            _resizeStartPos = e.GetPosition(CanvasGrid);
            _resizingCell = cell;
            e.Pointer.Capture(c);
            e.Handled = true;
        }
    }

    private void ResizeThumb_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing || _resizingCell == null)
            return;
        e.Handled = true;

        var pt = e.GetPosition(CanvasGrid);
        int newCols = Math.Max(1, (int)Math.Round((pt.X - _resizingCell.CanvasX) / Constants.GridSize));
        int newRows = Math.Max(1, (int)Math.Round((pt.Y - _resizingCell.CanvasY) / Constants.GridSize));

        // Check for same-layer collisions
        bool collision = HasLayerCollision(_resizingCell.CollisionLayer, _resizingCell,
            _resizingCell.CanvasX, _resizingCell.CanvasY, newCols, newRows);

        if (!collision)
        {
            _resizingCell.ColSpan = newCols;
            _resizingCell.RowSpan = newRows;
        }
    }

    private void ResizeThumb_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizing && sender is Control)
        {
            e.Pointer.Capture(null);
            _isResizing = false;
            _resizingCell = null;
            e.Handled = true;
            SaveBoardData();
        }
    }

    #endregion

    #region Canvas Pointer Handlers (Pan, Draw, Hover)

    private void MainCanvas_PointerEntered(object? sender, PointerEventArgs e)
    {
        IsPointerOverCanvas = true;
    }

    private void MainCanvas_PointerExited(object? sender, PointerEventArgs e)
    {
        IsPointerOverCanvas = false;
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        var mainCanvas = this.FindControl<Canvas>("MainCanvas");

        // Update custom cursor icon position
        var cursorIcon = this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            var pt = e.GetPosition(mainCanvas);
            Canvas.SetLeft(cursorIcon, pt.X + 15);
            Canvas.SetTop(cursorIcon, pt.Y + 15);
        }

        // Annotation mode: Eraser
        // Skip when the middle button is also held — that combination is the Nuke-style drag-to-zoom gesture.
        if (IsDrawMode && IsEraserMode && !e.Handled && props.IsLeftButtonPressed && !props.IsMiddleButtonPressed)
        {
            EraseIntersectingAnnotations(e.GetPosition(mainCanvas));
            e.Pointer.Capture(sender as IInputElement);
            return;
        }

        // Annotation mode: Move/Select
        if (IsDrawMode && IsMoveMode && !e.Handled && props.IsLeftButtonPressed)
        {
            _isSelectingAnnotations = true;
            _annotationSelectionStart = e.GetPosition(mainCanvas);

            var marquee = this.FindControl<Border>("SelectionMarquee");
            if (marquee != null)
            {
                Canvas.SetLeft(marquee, _annotationSelectionStart.X);
                Canvas.SetTop(marquee, _annotationSelectionStart.Y);
                marquee.Width = 0;
                marquee.Height = 0;
                marquee.IsVisible = true;
            }

            _selectedAnnotations.Clear();
            foreach (var a in Annotations)
                a.IsSelected = false;
            e.Pointer.Capture(sender as IInputElement);
            return;
        }

        // Annotation mode: Draw new annotation
        if (IsDrawMode && !IsEraserMode && !IsMoveMode && !e.Handled && props.IsLeftButtonPressed)
        {
            _currentAnnotation = new AnnotationViewModel
            {
                Type = CurrentTool,
                Color = CurrentBrushColor,
                Thickness = CurrentBrushThickness
            };

            var pt = e.GetPosition(mainCanvas);
            _currentAnnotation.Points.Add(pt);

            if (CurrentTool == "Text")
            {
                _currentAnnotation.Text = "";
                _editingTextAnnotation = _currentAnnotation;
                _editingTextAnnotationOriginalText = null; // null marks this as a brand-new annotation

                var editor = this.FindControl<TextBox>("AnnotationTextEditor");
                if (editor != null)
                {
                    editor.Text = _currentAnnotation.Text;
                    Canvas.SetLeft(editor, pt.X);
                    Canvas.SetTop(editor, pt.Y);
                    editor.IsVisible = true;
                    editor.Focus();

                    editor.TextChanged -= AnnotationTextEditor_TextChanged;
                    editor.TextChanged += AnnotationTextEditor_TextChanged;
                    editor.LostFocus -= AnnotationTextEditor_LostFocus;
                    editor.LostFocus += AnnotationTextEditor_LostFocus;
                    editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
                    editor.AddHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown, RoutingStrategies.Tunnel);
                }
            }

            Annotations.Add(_currentAnnotation);
            e.Pointer.Capture(sender as IInputElement);
            return;
        }

        // Grid mode: Middle button starts pan; adding left button activates drag-to-zoom (Nuke-style).
        // Selection is preserved so the user can pan/zoom without losing their selection.
        if (props.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(this);
            _middleZoomStartY = e.GetPosition(this).Y;
        }
        // Left-click on empty canvas space: start cell marquee selection
        else if (!e.Handled && props.IsLeftButtonPressed && !IsDrawMode)
        {
            ClearSelection();
            _isSelectingCells = true;
            _cellSelectionStart = e.GetPosition(mainCanvas);

            var cellMarquee = this.FindControl<Border>("CellSelectionMarquee");
            if (cellMarquee != null)
            {
                Canvas.SetLeft(cellMarquee, _cellSelectionStart.X);
                Canvas.SetTop(cellMarquee, _cellSelectionStart.Y);
                cellMarquee.Width = 0;
                cellMarquee.Height = 0;
                cellMarquee.IsVisible = true;
            }

            e.Pointer.Capture(sender as IInputElement);
            foreach (var a in Annotations)
                a.IsSelected = false;
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var mainCanvas = this.FindControl<Canvas>("MainCanvas");
        var pt = e.GetPosition(mainCanvas);

        // Update custom cursor icon position
        var cursorIcon = this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            Canvas.SetLeft(cursorIcon, pt.X + 15);
            Canvas.SetTop(cursorIcon, pt.Y + 15);
        }

        // Eraser drag — but not when the middle button is also held (that's the Nuke-style zoom gesture)
        if (IsDrawMode && IsEraserMode
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && !e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            EraseIntersectingAnnotations(pt);
            return;
        }

        // Marquee selection drag
        if (_isSelectingAnnotations)
        {
            var marquee = this.FindControl<Border>("SelectionMarquee");
            if (marquee != null)
            {
                double left = Math.Min(_annotationSelectionStart.X, pt.X);
                double top = Math.Min(_annotationSelectionStart.Y, pt.Y);
                Canvas.SetLeft(marquee, left);
                Canvas.SetTop(marquee, top);
                marquee.Width = Math.Abs(pt.X - _annotationSelectionStart.X);
                marquee.Height = Math.Abs(pt.Y - _annotationSelectionStart.Y);
            }
            return;
        }

        // Annotation drag
        if (_isDraggingAnnotations && _selectedAnnotations.Count > 0)
        {
            if (IsDrawMode)
            {
                // Annotation mode: free movement
                double dx = pt.X - _annotationDragStart.X;
                double dy = pt.Y - _annotationDragStart.Y;
                foreach (var ann in _selectedAnnotations)
                {
                    ann.CanvasX += dx;
                    ann.CanvasY += dy;
                }
                _annotationDragStart = pt;
            }
            else
            {
                // Grid mode: grid-snapped movement
                // Calculate grid-snapped target position
                double targetX = Math.Round(pt.X / Constants.GridSize) * Constants.GridSize;
                double targetY = Math.Round(pt.Y / Constants.GridSize) * Constants.GridSize;
                double startX = Math.Round(_annotationDragStart.X / Constants.GridSize) * Constants.GridSize;
                double startY = Math.Round(_annotationDragStart.Y / Constants.GridSize) * Constants.GridSize;

                double dx = targetX - startX;
                double dy = targetY - startY;

                // Only move if there's actual delta
                if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
                {
                    foreach (var ann in _selectedAnnotations)
                    {
                        ann.CanvasX += dx;
                        ann.CanvasY += dy;
                    }
                    _annotationDragStart = new Point(targetX, targetY);
                }
            }
            return;
        }

        // Drawing in progress
        if (_currentAnnotation != null)
        {
            if (_currentAnnotation.Type == "Pencil")
            {
                if (_currentAnnotation.Points.Count == 0
                    || Math.Abs(pt.X - _currentAnnotation.Points.Last().X) > 2
                    || Math.Abs(pt.Y - _currentAnnotation.Points.Last().Y) > 2)
                {
                    _currentAnnotation.Points.Add(pt);
                }
            }
            else if (_currentAnnotation.Type != "Text")
            {
                if (_currentAnnotation.Points.Count < 2)
                    _currentAnnotation.Points.Add(pt);
                else
                    _currentAnnotation.Points[1] = pt;
            }
            return;
        }

        // Cell marquee selection drag
        if (_isSelectingCells)
        {
            var cellMarquee = this.FindControl<Border>("CellSelectionMarquee");
            if (cellMarquee != null)
            {
                double left = Math.Min(_cellSelectionStart.X, pt.X);
                double top = Math.Min(_cellSelectionStart.Y, pt.Y);
                Canvas.SetLeft(cellMarquee, left);
                Canvas.SetTop(cellMarquee, top);
                cellMarquee.Width = Math.Abs(pt.X - _cellSelectionStart.X);
                cellMarquee.Height = Math.Abs(pt.Y - _cellSelectionStart.Y);
            }
            // Don't process hover highlight while selecting
            return;
        }

        // Hover highlight for grid cells
        var gridPt = e.GetPosition(CanvasGrid);
        int gridX = (int)(Math.Floor(gridPt.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(gridPt.Y / Constants.GridSize) * Constants.GridSize);

        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight != null)
        {
            // Only hide hover highlight for content-layer cells; board elements
            // (backdrops, labels) sit on different layers and allow content on top.
            var existingContent = GridCells.FirstOrDefault(c =>
                !c.IsBoardElement && c.HasContent
                && c.CanvasX <= gridPt.X && c.CanvasX + c.PixelWidth > gridPt.X
                && c.CanvasY <= gridPt.Y && c.CanvasY + c.PixelHeight > gridPt.Y);

            Canvas.SetLeft(hoverHighlight, gridX);
            Canvas.SetTop(hoverHighlight, gridY);
            hoverHighlight.Width = Constants.GridSize;
            hoverHighlight.Height = Constants.GridSize;
            hoverHighlight.IsVisible = !(_isPanning || _isDraggingCell || _isResizing
                                         || _isPointerDown || existingContent != null || IsDrawMode);
        }

        // Nuke-style drag-to-zoom: middle + left buttons held together.
        // Vertical movement controls zoom (up = in, down = out), centered on current cursor.
        var currentProps = e.GetCurrentPoint(this).Properties;
        bool bothButtons = currentProps.IsMiddleButtonPressed && currentProps.IsLeftButtonPressed;

        if (bothButtons)
        {
            // Lock the anchor and origin on the first frame both buttons are held.
            if (!_middleZoomAnchorSet)
            {
                _middleZoomAnchor = sender is Visual v ? e.GetPosition(v) : e.GetPosition(this);
                _middleZoomOriginY = e.GetPosition(this).Y;
                _middleZoomStartY = _middleZoomOriginY;
                _middleZoomActive = false;
                _middleZoomAnchorSet = true;
            }

            var screenPt = e.GetPosition(this);

            // Dead zone: don't zoom until the cursor has moved far enough from the
            // initial press point. Prevents jitter when the tablet pen first touches.
            if (!_middleZoomActive)
            {
                if (Math.Abs(screenPt.Y - _middleZoomOriginY) < Constants.MiddleZoomDeadZone)
                {
                    _panStartPoint = screenPt;
                    return;
                }
                // Crossed the dead zone — start zooming from this point
                _middleZoomActive = true;
                _middleZoomStartY = screenPt.Y;
            }

            double deltaY = _middleZoomStartY - screenPt.Y; // positive = moved up = zoom in

            double oldScale = _scale.ScaleX;
            double zoomAmount = Math.Clamp(
                deltaY * Constants.MiddleZoomSensitivity,
                -Constants.MiddleZoomMaxDelta,
                Constants.MiddleZoomMaxDelta);
            double newScale = Math.Clamp(oldScale + zoomAmount, Constants.MinZoom, Constants.MaxZoom);

            if (Math.Abs(newScale - oldScale) > 0.0001)
            {
                _translate.X += _middleZoomAnchor.X * (1.0 / newScale - 1.0 / oldScale);
                _translate.Y += _middleZoomAnchor.Y * (1.0 / newScale - 1.0 / oldScale);
                _scale.ScaleX = newScale;
                _scale.ScaleY = newScale;
                OnPropertyChanged(nameof(ZoomLevelText));
            }

            _middleZoomStartY = screenPt.Y;
            _panStartPoint = screenPt;
        }
        // Panning: middle button only, or left button on empty space
        else if (_isPanning && (currentProps.IsMiddleButtonPressed || currentProps.IsLeftButtonPressed))
        {
            var screenPt = e.GetPosition(this);
            _translate.X += (screenPt.X - _panStartPoint.X) / _scale.ScaleX;
            _translate.Y += (screenPt.Y - _panStartPoint.Y) / _scale.ScaleY;
            _panStartPoint = screenPt;
        }
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsEraserMode)
            e.Pointer.Capture(null);

        // Finish marquee selection
        if (_isSelectingAnnotations)
        {
            _isSelectingAnnotations = false;
            e.Pointer.Capture(null);

            var marquee = this.FindControl<Border>("SelectionMarquee");
            if (marquee != null)
            {
                marquee.IsVisible = false;
                double left = Canvas.GetLeft(marquee);
                double top = Canvas.GetTop(marquee);
                double right = left + marquee.Width;
                double bottom = top + marquee.Height;

                _selectedAnnotations.Clear();
                foreach (var ann in Annotations)
                {
                    ann.IsSelected = false;
                    bool inRect = ann.Points.Any(p =>
                    {
                        double px = p.X + ann.CanvasX;
                        double py = p.Y + ann.CanvasY;
                        return px >= left && px <= right && py >= top && py <= bottom;
                    });

                    if (inRect)
                    {
                        ann.IsSelected = true;
                        _selectedAnnotations.Add(ann);
                    }
                }
            }
            return;
        }

        // Finish annotation drag
        if (_isDraggingAnnotations)
        {
            _isDraggingAnnotations = false;
            e.Pointer.Capture(null);
            MarkUnsaved();
            SaveBoardData();
            return;
        }

        // Finish drawing
        if (_currentAnnotation != null)
        {
            _currentAnnotation = null;
            e.Pointer.Capture(null);
            MarkUnsaved();
            SaveBoardData();
            return;
        }

        // Finish cell marquee selection
        if (_isSelectingCells)
        {
            _isSelectingCells = false;
            e.Pointer.Capture(null);

            var cellMarquee = this.FindControl<Border>("CellSelectionMarquee");
            if (cellMarquee != null)
            {
                cellMarquee.IsVisible = false;
                double left = Canvas.GetLeft(cellMarquee);
                double top = Canvas.GetTop(cellMarquee);
                double right = left + cellMarquee.Width;
                double bottom = top + cellMarquee.Height;

                // Only select if the marquee has meaningful size (not just a click)
                if (cellMarquee.Width > 4 || cellMarquee.Height > 4)
                {
                    _selectedCells.Clear();
                    foreach (var cell in GridCells)
                    {
                        cell.IsSelected = false;
                        if (!cell.HasContent)
                            continue;

                        // Select cells whose visual area intersects the marquee
                        double cx = cell.CanvasX;
                        double cy = cell.CanvasY;
                        double cw = cell.ColSpan * Constants.GridSize;
                        double ch = cell.RowSpan * Constants.GridSize;

                        bool intersects = cx < right && cx + cw > left
                                       && cy < bottom && cy + ch > top;
                        if (intersects)
                        {
                            cell.IsSelected = true;
                            _selectedCells.Add(cell);
                        }
                    }


                }
            }
            return;
        }

        _isPanning = false;
        _middleZoomAnchorSet = false;
        _middleZoomActive = false;
        OnPropertyChanged(nameof(SelectionCountText));
    }

    private void CanvasBorder_PointerExited(object? sender, PointerEventArgs e)
    {
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight != null)
            hoverHighlight.IsVisible = false;
    }

    private void Canvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled)
            return;

        double oldScale = _scale.ScaleX;
        double newScale = oldScale;

        if (e.Delta.Y > 0)
            newScale += Constants.ZoomStep;
        else if (e.Delta.Y < 0)
            newScale = Math.Max(Constants.MinZoom, oldScale - Constants.ZoomStep);

        if (Math.Abs(newScale - oldScale) < 0.001)
            return;
        newScale = Math.Clamp(newScale, Constants.MinZoom, Constants.MaxZoom);

        if (sender is Visual visual)
        {
            var pointerPos = e.GetPosition(visual);
            _translate.X += pointerPos.X * (1.0 / newScale - 1.0 / oldScale);
            _translate.Y += pointerPos.Y * (1.0 / newScale - 1.0 / oldScale);
        }

        _scale.ScaleX = newScale;
        _scale.ScaleY = newScale;
        OnPropertyChanged(nameof(ZoomLevelText));
    }

    #endregion

    #region Annotation Interaction

    private void EraseIntersectingAnnotations(Point pt)
    {
        var toRemove = Annotations.Where(ann =>
            ann.Points.Any(p =>
                Math.Abs(p.X + ann.CanvasX - pt.X) < 15
                && Math.Abs(p.Y + ann.CanvasY - pt.Y) < 15))
            .ToList();

        if (toRemove.Count == 0)
            return;

        foreach (var ann in toRemove)
            Annotations.Remove(ann);
        MarkUnsaved();
        SaveBoardData();
    }

    private void Annotation_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isViewMode || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Grid mode: click annotation to select it and enable grid-snapped dragging
        if (!IsDrawMode && sender is Control { DataContext: AnnotationViewModel annGrid })
        {
            bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (isCtrl)
            {
                annGrid.IsSelected = !annGrid.IsSelected;
                if (annGrid.IsSelected)
                    _selectedAnnotations.Add(annGrid);
                else
                    _selectedAnnotations.Remove(annGrid);
            }
            else
            {
                if (!annGrid.IsSelected)
                {
                    ClearSelection();
                    annGrid.IsSelected = true;
                    _selectedAnnotations.Add(annGrid);
                }

                // Start grid-snapped annotation drag
                _isDraggingAnnotations = true;
                var mainCanvas = this.FindControl<Canvas>("MainCanvas");
                if (mainCanvas != null)
                {
                    _annotationDragStart = e.GetPosition(mainCanvas);
                }
                e.Pointer.Capture(sender as Control);
            }

            e.Handled = true;
            OnPropertyChanged(nameof(SelectionCountText));
            return;
        }

        // Draw mode only from here
        if (!IsDrawMode)
            return;

        // Move mode: select and drag annotation
        if (IsMoveMode && sender is Control { DataContext: AnnotationViewModel annMove })
        {
            if (!_selectedAnnotations.Contains(annMove))
            {
                _selectedAnnotations.Clear();
                foreach (var a in Annotations)
                    a.IsSelected = false;
                _selectedAnnotations.Add(annMove);
                annMove.IsSelected = true;
            }

            _isDraggingAnnotations = true;
            _annotationDragStart = e.GetPosition(this.FindControl<Canvas>("MainCanvas"));
            e.Handled = true;
            return;
        }

        // Eraser mode: delete clicked annotation
        if (IsEraserMode && sender is Control { DataContext: AnnotationViewModel ann })
        {
            Annotations.Remove(ann);
            MarkUnsaved();
            SaveBoardData();
            e.Handled = true;
            return;
        }

        // Text tool: edit existing text annotation
        if (CurrentTool == "Text"
            && sender is Control { DataContext: AnnotationViewModel { Type: "Text" } annText })
        {
            _editingTextAnnotation = annText;
            _editingTextAnnotationOriginalText = annText.Text; // remember so Escape can revert
            var editor = this.FindControl<TextBox>("AnnotationTextEditor");
            if (editor != null)
            {
                editor.Text = annText.Text;
                Canvas.SetLeft(editor, annText.Points[0].X + annText.CanvasX);
                Canvas.SetTop(editor, annText.Points[0].Y + annText.CanvasY);
                editor.IsVisible = true;
                editor.Focus();

                editor.TextChanged -= AnnotationTextEditor_TextChanged;
                editor.TextChanged += AnnotationTextEditor_TextChanged;
                editor.LostFocus -= AnnotationTextEditor_LostFocus;
                editor.LostFocus += AnnotationTextEditor_LostFocus;
                editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
                editor.AddHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown, RoutingStrategies.Tunnel);
            }
            e.Handled = true;
        }
    }

    private void AnnotationTextEditor_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_editingTextAnnotation != null && sender is TextBox editor)
            _editingTextAnnotation.Text = editor.Text ?? "";
    }

    private void AnnotationTextEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelTextAnnotationEditing();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            // Plain Enter commits the text — Shift+Enter or Ctrl+Enter inserts a newline.
            CommitTextAnnotationEditing();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Cancels an in-progress text annotation edit:
    /// - If it was a brand-new annotation (original text is null) the annotation is discarded entirely.
    /// - If it was an existing annotation the text is reverted to what it was before editing started.
    /// Unsubscribes all editor events before hiding to prevent LostFocus from double-processing.
    /// </summary>
    private void CancelTextAnnotationEditing()
    {
        if (_editingTextAnnotation == null)
            return;

        var editor = this.FindControl<TextBox>("AnnotationTextEditor");
        if (editor != null)
        {
            // Unsubscribe before hiding so LostFocus doesn't fire our commit handler.
            editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
            editor.TextChanged -= AnnotationTextEditor_TextChanged;
            editor.LostFocus -= AnnotationTextEditor_LostFocus;
            editor.IsVisible = false;
        }

        if (_editingTextAnnotationOriginalText == null)
            Annotations.Remove(_editingTextAnnotation);   // new annotation — discard
        else
            _editingTextAnnotation.Text = _editingTextAnnotationOriginalText; // existing — revert

        _editingTextAnnotation = null;
        _editingTextAnnotationOriginalText = null;

        this.FindControl<Border>("CanvasBorder")?.Focus();
    }

    /// <summary>
    /// Commits the current text annotation edit (user clicked away or pressed Enter).
    /// Unsubscribes all editor events before hiding to prevent LostFocus from double-processing.
    /// </summary>
    private void CommitTextAnnotationEditing()
    {
        if (_editingTextAnnotation == null)
            return;

        var editor = this.FindControl<TextBox>("AnnotationTextEditor");
        if (editor != null)
        {
            editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
            editor.TextChanged -= AnnotationTextEditor_TextChanged;
            editor.LostFocus -= AnnotationTextEditor_LostFocus;
            editor.IsVisible = false;
        }

        if (string.IsNullOrWhiteSpace(_editingTextAnnotation.Text))
            Annotations.Remove(_editingTextAnnotation);

        _editingTextAnnotation = null;
        _editingTextAnnotationOriginalText = null;
        MarkUnsaved();
        SaveBoardData();

        this.FindControl<Border>("CanvasBorder")?.Focus();
    }

    private void AnnotationTextEditor_LostFocus(object? sender, RoutedEventArgs e)
    {
        // User clicked away — treat as a commit.
        CommitTextAnnotationEditing();
    }

    #endregion

    #region Drag & Drop

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy | DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_isViewMode)
        { e.Handled = true; return; }
        e.Handled = true;

        var dropPt = e.GetPosition(CanvasGrid);
        int gridX = (int)(Math.Floor(dropPt.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(dropPt.Y / Constants.GridSize) * Constants.GridSize);

        // Handle multi-file drop from OS
        var files = e.DataTransfer.TryGetFiles();
        if (files != null && files.Count() > 1)
        {
            // Multi-file drop: arrange in grid layout
            int filesPerRow = 4;
            int currentRow = 0;
            int currentCol = 0;

            foreach (var file in files)
            {
                string path = file.Path.LocalPath;
                if (!File.Exists(path))
                    continue;

                // Calculate optimal size based on image dimensions
                var dimensions = GetImageDimensions(path);
                var (colSpan, rowSpan) = dimensions.HasValue
                    ? CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height)
                    : (2, 2);

                // Calculate preferred position in grid layout
                // Space items 3 cells apart for better visual separation
                double preferredX = gridX + (currentCol * Constants.GridSize * 3);
                double preferredY = gridY + (currentRow * Constants.GridSize * 3);

                // Find actual empty space (uses spiral search from preferred position)
                var emptySpace = FindEmptySpace(preferredX, preferredY, colSpan, rowSpan, collisionLayer: 1);

                if (emptySpace != null)
                {
                    // Copy file to workspace
                    string destDir = Path.Combine(_workspaceDir, "images");
                    Directory.CreateDirectory(destDir);
                    string destPath = Path.Combine(destDir, Path.GetFileName(path));
                    if (path != destPath && !File.Exists(destPath))
                        File.Copy(path, destPath);

                    // Create cell with proper size and position
                    var cell = new CellViewModel
                    {
                        CanvasX = emptySpace.Value.X,
                        CanvasY = emptySpace.Value.Y,
                        ColSpan = colSpan,
                        RowSpan = rowSpan
                    };
                    cell.SetImage(destPath);
                    GridCells.Add(cell);
                }

                // Move to next position in grid layout
                currentCol++;
                if (currentCol >= filesPerRow)
                {
                    currentCol = 0;
                    currentRow++;
                }
            }

            MarkUnsaved();
            SaveBoardData();
            e.Handled = true;
            return;
        }

        // For internal cell moves, find the cell on any layer; for external file drops,
        // skip board elements so content lands on the content layer.
        CellViewModel targetCell;
        if (_draggingCell != null)
        {
            targetCell = GridCells.FirstOrDefault(c =>
                    (int)c.CanvasX == gridX && (int)c.CanvasY == gridY
                    && c.CollisionLayer == _draggingCell.CollisionLayer)
                ?? GetOrCreateCellAt(dropPt);
        }
        else
        {
            // External file drop → content layer; skip board elements at this position
            targetCell = GridCells.FirstOrDefault(c =>
                    (int)c.CanvasX == gridX && (int)c.CanvasY == gridY && !c.IsBoardElement)
                ?? GetOrCreateContentCellAt(dropPt);
        }

        int neededCols = _draggingCell?.ColSpan ?? 1;
        int neededRows = _draggingCell?.RowSpan ?? 1;

        // Check for same-layer collisions (external file drops target the content layer)
        int dropLayer = _draggingCell?.CollisionLayer ?? 1;
        bool collision = HasLayerCollision(dropLayer, _draggingCell,
            targetCell.CanvasX, targetCell.CanvasY, neededCols, neededRows);

        if (collision)
        { ShakeScreen(); return; }

        // Handle single file drop from OS
        if (files != null && files.Any())
        {
            try
            { LoadImageToCell(targetCell, files.First().Path.LocalPath); }
            catch { /* non-critical */ }
            return;
        }

        // Handle internal cell move
        if (_draggingCell != null && _draggingCell != targetCell)
        {
            if (_draggingCell.IsVideo)
                targetCell.SetVideo(_draggingCell.VideoPath!, _draggingCell.FilePath!);
            else if (_draggingCell.IsImage)
                targetCell.SetImage(_draggingCell.FilePath!);
            else if (_draggingCell.IsText)
                targetCell.SetText(_draggingCell.TextContent ?? "");

            targetCell.ColSpan = _draggingCell.ColSpan;
            targetCell.RowSpan = _draggingCell.RowSpan;

            _draggingCell.Clear();
            GridCells.Remove(_draggingCell);
            MarkUnsaved();
            SaveBoardData();
        }

        _draggingCell = null;
        _isPointerDown = false;
        _lastPressedEventArgs = null;
    }

    #endregion

    #region Keyboard Shortcuts

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (FullMediaOverlay.IsVisible || (startupOverlay?.IsVisible == true))
            return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool noModifiers = e.KeyModifiers == KeyModifiers.None;

        // Ctrl+N: New board
        if (e.Key == Key.N && isCtrl)
        { NewBoard_Click(null, null!); return; }

        // Ctrl+O: Open board
        if (e.Key == Key.O && isCtrl)
        { LoadBoard_Click(null, null!); return; }

        // Ctrl+S: Save
        if (e.Key == Key.S && isCtrl)
        {
            if (!string.IsNullOrEmpty(_currentBoardFile))
                SaveBoardData();
            else
                SaveBoard_Click(null, null!);
            return;
        }

        // Don't intercept keys while typing in a visible TextBox.
        // The IsVisible guard is a safety net: if the AnnotationTextEditor was somehow
        // still tracked by the FocusManager after being hidden, we must not block shortcuts.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox { IsVisible: true })
            return;

        // Ctrl+Shift+Z or Ctrl+Y: Redo
        if (e.Key == Key.Z && isCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        { Redo(); return; }
        if (e.Key == Key.Y && isCtrl)
        { Redo(); return; }

        // Ctrl+Z: Undo
        if (e.Key == Key.Z && isCtrl)
        { Undo(); return; }

        // Ctrl+I: Import media
        if (e.Key == Key.I && isCtrl)
        { ImportMedia_Click(null, null!); return; }

        // Ctrl+V: Paste
        if (e.Key == Key.V && isCtrl)
        {
            if (_isViewMode)
                return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            var data = await clipboard.TryGetDataAsync();
            if (data == null)
                return;

            // Determine preferred position for paste
            double preferredX, preferredY;
            if (_hoveredCell is { IsBoardElement: true } || _hoveredCell == null)
            {
                var hoverHighlight = this.FindControl<Border>("HoverHighlight");
                if (hoverHighlight != null && hoverHighlight.IsVisible)
                {
                    double left = Canvas.GetLeft(hoverHighlight);
                    double top = Canvas.GetTop(hoverHighlight);
                    if (!double.IsNaN(left) && !double.IsNaN(top))
                    {
                        preferredX = left + 80;
                        preferredY = top + 80;
                    }
                    else
                    {
                        preferredX = Bounds.Width / 2 / _scale.ScaleX - _translate.X;
                        preferredY = Bounds.Height / 2 / _scale.ScaleY - _translate.Y;
                    }
                }
                else
                {
                    preferredX = Bounds.Width / 2 / _scale.ScaleX - _translate.X;
                    preferredY = Bounds.Height / 2 / _scale.ScaleY - _translate.Y;
                }
            }
            else
            {
                preferredX = _hoveredCell.CanvasX;
                preferredY = _hoveredCell.CanvasY;
            }

            // Try text (URL or plain text)
            var text = await data.TryGetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                var cell = GetOrCreateContentCellAt(new Point(preferredX, preferredY));
                if (cell.HasContent && !cell.IsBoardElement)
                { ShakeScreen(); return; }

                if (text.Contains("youtube.com") || text.Contains("youtu.be") || text.StartsWith("http"))
                    await DownloadVideoToCell(cell, text);
                else
                    cell.SetText(text);
                SaveBoardData();
                return;
            }

            // Try file (use smart sizing for images)
            var pastedFiles = await data.TryGetFilesAsync();
            if (pastedFiles != null && pastedFiles.Any())
            {
                try
                {
                    string imagePath = pastedFiles.First().Path.LocalPath;

                    // Get image dimensions and calculate optimal cell size
                    var dimensions = GetImageDimensions(imagePath);
                    int colSpan, rowSpan;
                    if (dimensions == null)
                    {
                        // Fallback to default 2x2 if we can't read the image
                        (colSpan, rowSpan) = (2, 2);
                    }
                    else
                    {
                        (colSpan, rowSpan) = CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);
                    }

                    // Find empty space near the preferred position
                    Point? emptySpace = FindEmptySpace(preferredX, preferredY, colSpan, rowSpan, collisionLayer: 1);

                    if (emptySpace == null)
                    {
                        ShakeScreen();
                        return;
                    }

                    // Copy image to workspace
                    string destDir = Path.Combine(_workspaceDir, "images");
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                    string destPath = Path.Combine(destDir, Path.GetFileName(imagePath));
                    if (imagePath != destPath && !File.Exists(destPath))
                        File.Copy(imagePath, destPath);

                    // Create cell at the found position with calculated span
                    var newCell = new CellViewModel
                    {
                        CanvasX = emptySpace.Value.X,
                        CanvasY = emptySpace.Value.Y,
                        ColSpan = colSpan,
                        RowSpan = rowSpan
                    };
                    newCell.SetImage(destPath);
                    GridCells.Add(newCell);
                    MarkUnsaved();
                    SaveBoardData();
                }
                catch { /* non-critical */ }
                return;
            }

            // Try bitmap (use smart sizing)
            var bitmap = await data.TryGetBitmapAsync();
            if (bitmap != null)
            {
                string destDir = Path.Combine(_workspaceDir, "images");
                Directory.CreateDirectory(destDir);
                string path = Path.Combine(destDir, Guid.NewGuid() + ".png");
                bitmap.Save(path);

                // Get image dimensions and calculate optimal cell size
                var dimensions = GetImageDimensions(path);
                int colSpan, rowSpan;
                if (dimensions == null)
                {
                    // Fallback to default 2x2 if we can't read the image
                    (colSpan, rowSpan) = (2, 2);
                }
                else
                {
                    (colSpan, rowSpan) = CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);
                }

                // Find empty space near the preferred position
                Point? emptySpace = FindEmptySpace(preferredX, preferredY, colSpan, rowSpan, collisionLayer: 1);

                if (emptySpace == null)
                {
                    ShakeScreen();
                    return;
                }

                // Create cell at the found position with calculated span
                var newCell = new CellViewModel
                {
                    CanvasX = emptySpace.Value.X,
                    CanvasY = emptySpace.Value.Y,
                    ColSpan = colSpan,
                    RowSpan = rowSpan
                };
                newCell.SetImage(path);
                GridCells.Add(newCell);
                MarkUnsaved();
                SaveBoardData();
                return;
            }

            SaveBoardData();
            return;
        }

        // Escape: Clear all selection
        if (e.Key == Key.Escape)
        {
            ClearSelection();
            return;
        }

        // Ctrl+1: Grid mode
        if (e.Key == Key.D1 && isCtrl)
        { IsDrawMode = false; return; }

        // Ctrl+2: Annotation mode
        if (e.Key == Key.D2 && isCtrl)
        { IsDrawMode = true; return; }

        // F: Show All (fit to view)
        if (e.Key == Key.F && noModifiers)
        { ShowAll_Click(null, null!); return; }

        // Home: Center view
        if (e.Key == Key.Home && noModifiers)
        { CenterView_Click(null, null!); return; }

        // Ctrl+Shift+F: Fit to Content
        if (e.Key == Key.F && isCtrl && isShift)
        {
            // Get selected cells
            if (_selectedCells.Count > 0)
            {
                foreach (var cell in _selectedCells.ToList())
                {
                    if (!cell.IsImage && !cell.IsVideo)
                        continue;
                    if (string.IsNullOrEmpty(cell.FilePath))
                        continue;

                    var dimensions = GetImageDimensions(cell.FilePath);
                    if (dimensions == null)
                        continue;

                    var (newColSpan, newRowSpan) = CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);

                    if (IsSpaceEmpty(cell.CanvasX, cell.CanvasY, newColSpan, newRowSpan, cell.CollisionLayer, excludeCell: cell))
                    {
                        cell.ColSpan = newColSpan;
                        cell.RowSpan = newRowSpan;
                    }
                }

                MarkUnsaved();
                SaveBoardData();
            }
            return;
        }

        // Ctrl+Shift+T: Toggle Always on Top
        if (e.Key == Key.T && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            IsAlwaysOnTop = !IsAlwaysOnTop;
            e.Handled = true;
            return;
        }

        // Ctrl+T: Quick add text
        if (e.Key == Key.T && isCtrl)
        {
            if (_isViewMode)
                return;
            var cell = _hoveredCell ?? GetHighlightedCell();
            cell.SetText("New Description...");
            SaveBoardData();
            return;
        }

        // Delete/Backspace: Remove all selected items (cells + annotations), or hovered cell
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            if (_isViewMode)
                return;

            bool anyDeleted = false;

            // Delete all selected cells
            if (_selectedCells.Count > 0)
            {
                foreach (var cell in _selectedCells.ToList())
                {
                    if (cell.FilePath != null && File.Exists(cell.FilePath))
                        try
                        { File.Delete(cell.FilePath); }
                        catch { /* non-critical */ }
                    if (cell.VideoPath != null && File.Exists(cell.VideoPath))
                        try
                        { File.Delete(cell.VideoPath); }
                        catch { /* non-critical */ }
                    cell.Clear();
                    GridCells.Remove(cell);
                }
                _selectedCells.Clear();
                _hoveredCell = null;
                anyDeleted = true;
            }

            // Delete all selected annotations
            if (_selectedAnnotations.Count > 0)
            {
                foreach (var ann in _selectedAnnotations.ToList())
                    Annotations.Remove(ann);
                _selectedAnnotations.Clear();
                anyDeleted = true;
            }

            if (anyDeleted)
            {
                MarkUnsaved();
                SaveBoardData();
            }
            else if (_hoveredCell != null)
            {
                if (_hoveredCell.FilePath != null && File.Exists(_hoveredCell.FilePath))
                    try
                    { File.Delete(_hoveredCell.FilePath); }
                    catch { /* non-critical */ }
                if (_hoveredCell.VideoPath != null && File.Exists(_hoveredCell.VideoPath))
                    try
                    { File.Delete(_hoveredCell.VideoPath); }
                    catch { /* non-critical */ }

                _hoveredCell.Clear();
                GridCells.Remove(_hoveredCell);
                _hoveredCell = null;
                MarkUnsaved();
                SaveBoardData();
            }
        }
    }

    #endregion

    #region Fullscreen Media Overlay

    private void CanvasImage_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: CellViewModel cell })
            return;

        string? pathToOpen = cell.IsImage ? cell.FilePath
                           : cell.IsVideo ? cell.VideoPath
                           : null;

        if (!string.IsNullOrEmpty(pathToOpen) && File.Exists(pathToOpen))
            PlatformHelper.OpenWithDefaultApp(pathToOpen);
    }

    private void CanvasText_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: CellViewModel cell })
            return;
        if (!cell.IsText && !cell.IsBoardElement)
            return;

        FullImage.IsVisible = false;
        FullText.IsVisible = true;
        FullText.Text = cell.TextContent;
        _editingTextCell = cell;
        FullMediaOverlay.IsVisible = true;
    }

    private void FullText_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_editingTextCell == null)
            return;
        _editingTextCell.TextContent = FullText.Text;
        MarkUnsaved();
        SaveBoardData();
    }

    private void CloseFullMedia_Click(object? sender, RoutedEventArgs e)
    {
        FullMediaOverlay.IsVisible = false;
        FullText.IsVisible = false;
        _editingTextCell = null;
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is TextBox)
            return;
        FullMediaOverlay.IsVisible = false;
        FullText.IsVisible = false;
        _editingTextCell = null;
    }

    #endregion

    #region View Navigation

    private void CenterView_Click(object? sender, RoutedEventArgs e)
    {
        _translate.X = 0;
        _translate.Y = 0;
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
        OnPropertyChanged(nameof(ZoomLevelText));
    }

    private void ShowAll_Click(object? sender, RoutedEventArgs e)
    {
        if (GridCells.Count == 0)
        { CenterView_Click(sender, e); return; }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var cell in GridCells)
        {
            if (cell.CanvasX < minX)
                minX = cell.CanvasX;
            if (cell.CanvasY < minY)
                minY = cell.CanvasY;
            if (cell.CanvasX + cell.PixelWidth > maxX)
                maxX = cell.CanvasX + cell.PixelWidth;
            if (cell.CanvasY + cell.PixelHeight > maxY)
                maxY = cell.CanvasY + cell.PixelHeight;
        }

        double contentWidth = maxX - minX;
        double contentHeight = maxY - minY;
        double viewportWidth = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double viewportHeight = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        const double padding = 100;
        double scaleX = viewportWidth / (contentWidth + padding);
        double scaleY = viewportHeight / (contentHeight + padding);
        double scale = Math.Clamp(Math.Min(scaleX, scaleY), Constants.MinZoom, 2.0);

        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        _translate.X = (viewportWidth - contentWidth * scale) / 2 - minX * scale;
        _translate.Y = (viewportHeight - contentHeight * scale) / 2 - minY * scale;
        OnPropertyChanged(nameof(ZoomLevelText));
    }

    #endregion

    #region Visual Feedback

    /// <summary>
    /// Briefly shakes the window to signal an invalid operation (e.g. paste onto occupied cell).
    /// </summary>
    private async void ShakeScreen()
    {
        var startPos = Position;
        for (int i = 0; i < 5; i++)
        {
            Position = new PixelPoint(startPos.X + 10, startPos.Y);
            await Task.Delay(30);
            Position = new PixelPoint(startPos.X - 10, startPos.Y);
            await Task.Delay(30);
        }
        Position = startPos;
    }

    #endregion

    #region Grid Helper Methods

    /// <summary>
    /// Finds all cells that are visually contained within a backdrop.
    /// </summary>
    private List<CellViewModel> GetBackdropChildren(CellViewModel backdrop)
    {
        if (!backdrop.IsBackdrop)
            return new List<CellViewModel>();

        var children = new List<CellViewModel>();
        var backdropRect = new Rect(backdrop.CanvasX, backdrop.CanvasY, backdrop.PixelWidth, backdrop.PixelHeight);

        foreach (var cell in GridCells)
        {
            if (cell == backdrop)
                continue;
            if (cell.IsBackdrop)
                continue; // Don't nest backdrops

            // Check if cell is inside backdrop
            var cellRect = new Rect(cell.CanvasX, cell.CanvasY, cell.PixelWidth, cell.PixelHeight);
            if (backdropRect.Contains(cellRect))
            {
                children.Add(cell);
            }
        }

        return children;
    }

    /// <summary>
    /// Finds all annotations that are visually contained within a backdrop.
    /// </summary>
    private List<AnnotationViewModel> GetBackdropAnnotations(CellViewModel backdrop)
    {
        if (!backdrop.IsBackdrop)
            return new List<AnnotationViewModel>();

        var annotations = new List<AnnotationViewModel>();
        var backdropRect = new Rect(backdrop.CanvasX, backdrop.CanvasY, backdrop.PixelWidth, backdrop.PixelHeight);

        foreach (var annotation in Annotations)
        {
            // Check if annotation's first point is inside backdrop
            if (annotation.Points.Count > 0 && backdropRect.Contains(annotation.Points[0]))
            {
                annotations.Add(annotation);
            }
        }

        return annotations;
    }

    /// <summary>
    /// Checks if a rectangular area is free (no collision with existing cells on the same layer).
    /// </summary>
    /// <param name="x">Grid-snapped X position</param>
    /// <param name="y">Grid-snapped Y position</param>
    /// <param name="colSpan">Number of columns</param>
    /// <param name="rowSpan">Number of rows</param>
    /// <param name="collisionLayer">Collision layer to check (0=backdrop, 1=content, 2=label)</param>
    /// <param name="excludeCell">Optional cell to exclude from collision check (for resize operations)</param>
    /// <returns>True if the space is empty</returns>
    private bool IsSpaceEmpty(double x, double y, int colSpan, int rowSpan, int collisionLayer, CellViewModel? excludeCell = null)
    {
        var rect = new Rect(x, y, colSpan * Constants.GridSize, rowSpan * Constants.GridSize);

        foreach (var cell in GridCells)
        {
            if (cell == excludeCell)
                continue;
            if (cell.CollisionLayer != collisionLayer)
                continue;

            // Backdrops get a half-grid margin to create visual spacing
            Rect cellRect;
            if (cell.IsBackdrop)
            {
                double margin = Constants.GridSize / 2.0;
                cellRect = new Rect(
                    cell.CanvasX - margin,
                    cell.CanvasY - margin,
                    cell.ColSpan * Constants.GridSize + 2 * margin,
                    cell.RowSpan * Constants.GridSize + 2 * margin
                );
            }
            else
            {
                cellRect = new Rect(cell.CanvasX, cell.CanvasY, cell.ColSpan * Constants.GridSize, cell.RowSpan * Constants.GridSize);
            }

            if (rect.Intersects(cellRect))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the nearest empty grid position that can fit the specified size.
    /// </summary>
    /// <param name="preferredX">Preferred X position (will snap to grid)</param>
    /// <param name="preferredY">Preferred Y position (will snap to grid)</param>
    /// <param name="colSpan">Number of columns needed</param>
    /// <param name="rowSpan">Number of rows needed</param>
    /// <param name="collisionLayer">Collision layer (0=backdrop, 1=content, 2=label)</param>
    /// <returns>Point with grid-snapped coordinates, or null if no space found in reasonable area</returns>
    private Point? FindEmptySpace(double preferredX, double preferredY, int colSpan, int rowSpan, int collisionLayer)
    {
        // Snap to grid
        int gridX = (int)(Math.Floor(preferredX / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(preferredY / Constants.GridSize) * Constants.GridSize);

        // Try the preferred position first
        if (IsSpaceEmpty(gridX, gridY, colSpan, rowSpan, collisionLayer))
            return new Point(gridX, gridY);

        // Spiral search outward from preferred position
        int maxDistance = 20; // Search up to 20 grid cells away
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            // Try positions at this distance in a spiral pattern
            for (int dx = -distance; dx <= distance; dx++)
            {
                for (int dy = -distance; dy <= distance; dy++)
                {
                    // Only check the "ring" at this distance, not interior
                    if (Math.Abs(dx) != distance && Math.Abs(dy) != distance)
                        continue;

                    int testX = gridX + dx * (int)Constants.GridSize;
                    int testY = gridY + dy * (int)Constants.GridSize;

                    if (IsSpaceEmpty(testX, testY, colSpan, rowSpan, collisionLayer))
                        return new Point(testX, testY);
                }
            }
        }

        return null; // No space found
    }

    /// <summary>
    /// Finds an empty grid-aligned space near a preferred position, excluding a specific cell from collision checks.
    /// </summary>
    private Point? FindEmptySpace(double preferredX, double preferredY, int colSpan, int rowSpan, int collisionLayer, CellViewModel? excludeCell)
    {
        // Snap to grid
        int gridX = (int)(Math.Floor(preferredX / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(preferredY / Constants.GridSize) * Constants.GridSize);

        // Try the preferred position first
        if (IsSpaceEmpty(gridX, gridY, colSpan, rowSpan, collisionLayer, excludeCell))
            return new Point(gridX, gridY);

        // Spiral search outward from preferred position
        int maxDistance = 20; // Search up to 20 grid cells away
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            // Try positions at this distance in a spiral pattern
            for (int dx = -distance; dx <= distance; dx++)
            {
                for (int dy = -distance; dy <= distance; dy++)
                {
                    // Only check the "ring" at this distance, not interior
                    if (Math.Abs(dx) != distance && Math.Abs(dy) != distance)
                        continue;

                    int testX = gridX + dx * (int)Constants.GridSize;
                    int testY = gridY + dy * (int)Constants.GridSize;

                    if (IsSpaceEmpty(testX, testY, colSpan, rowSpan, collisionLayer, excludeCell))
                        return new Point(testX, testY);
                }
            }
        }

        return null; // No space found
    }

    /// <summary>
    /// Gets the actual image dimensions from a file path.
    /// </summary>
    /// <param name="imagePath">Path to image file</param>
    /// <returns>Size with width and height, or null if cannot read</returns>
    private Size? GetImageDimensions(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            using var bitmap = new Bitmap(stream);
            return new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Calculates optimal ColSpan and RowSpan for an image based on its aspect ratio.
    /// Default is 2x2, but wide images get 3x1, 4x1, etc., and tall images get 1x3, 1x4, etc.
    /// </summary>
    /// <param name="imageWidth">Image width in pixels</param>
    /// <param name="imageHeight">Image height in pixels</param>
    /// <returns>Tuple of (colSpan, rowSpan)</returns>
    private (int colSpan, int rowSpan) CalculateOptimalCellSize(double imageWidth, double imageHeight)
    {
        // Default 2x2
        int colSpan = 2;
        int rowSpan = 2;

        if (imageWidth == 0 || imageHeight == 0)
            return (colSpan, rowSpan);

        double aspectRatio = imageWidth / imageHeight;

        // Very wide images (panoramas, UI elements)
        if (aspectRatio >= 3.0)
        {
            colSpan = 4;
            rowSpan = 1;
        }
        else if (aspectRatio >= 2.0)
        {
            colSpan = 3;
            rowSpan = 1;
        }
        else if (aspectRatio >= 1.5)
        {
            colSpan = 3;
            rowSpan = 2;
        }
        // Very tall images (character portraits, etc.)
        else if (aspectRatio <= 0.33)
        {
            colSpan = 1;
            rowSpan = 4;
        }
        else if (aspectRatio <= 0.5)
        {
            colSpan = 1;
            rowSpan = 3;
        }
        else if (aspectRatio <= 0.66)
        {
            colSpan = 2;
            rowSpan = 3;
        }
        // Near-square images remain 2x2

        return (colSpan, rowSpan);
    }

    #endregion
}
