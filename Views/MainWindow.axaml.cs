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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Controls;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

/// <summary>
/// Main application window. Acts as its own DataContext (View-as-ViewModel pattern)
/// for the reference board, supporting grid-based image/video/text layout with
/// an annotation overlay, undo/redo, and pan/zoom navigation.
///
/// UI event handlers are split across partial class files:
///   MainWindow.Canvas.cs      – canvas pointer handlers (pan, zoom, hover, marquee)
///   MainWindow.Cells.cs       – cell drag, resize, enter/exit
///   MainWindow.Annotations.cs – annotation drawing, editing, erasing
///   MainWindow.Commands.cs    – menu clicks, keyboard shortcuts, drag-drop, overlay
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private class UserSettings
    {
        public string AnnotationEffect { get; set; } = "None";
    }

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
        ScheduleViewportUpdate();

        ShowToast("↩ Undo");
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
        ScheduleViewportUpdate();

        ShowToast("↪ Redo");
        _isRestoringState = false;
    }

    /// <summary>
    /// Replaces the current board contents with the state described by the given JSON string.
    /// </summary>
    private void RestoreBoardState(string json)
    {
        // Dispose existing cell bitmaps before discarding the view-models
        foreach (var c in GridCells)
            c.UnloadImage();

        GridCells.Clear();
        Annotations.Clear();
        _selectedAnnotations.Clear();
        _currentAnnotation = null;
        _editingTextAnnotation = null;

        var (cells, annotations) = BoardSerializer.Deserialize(json, _currentBoardFile);
        foreach (var cell in cells)
            GridCells.Add(cell);
        foreach (var ann in annotations)
        {
            ann.IsInDrawMode = IsDrawMode;
            Annotations.Add(ann);
        }
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

            // Update hit-test state on all annotations
            foreach (var ann in Annotations)
                ann.IsInDrawMode = value;

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
        set { _currentBrushColor = value; OnPropertyChanged(nameof(CurrentBrushColor)); OnPropertyChanged(nameof(CurrentBrushColorBrush)); }
    }

    /// <summary>Current brush color as a SolidColorBrush for use in XAML bindings.</summary>
    public SolidColorBrush CurrentBrushColorBrush => SolidColorBrush.Parse(_currentBrushColor);

    private double _currentBrushThickness = 4.0;
    public double CurrentBrushThickness
    {
        get => _currentBrushThickness;
        set { _currentBrushThickness = value; OnPropertyChanged(nameof(CurrentBrushThickness)); }
    }

    private string _currentTool = "Brush";
    public string CurrentTool
    {
        get => _currentTool;
        set
        {
            _currentTool = value;
            _isEraserMode = value == "Eraser";
            _isMoveMode = value == "Move";
            OnPropertyChanged(nameof(CurrentTool));
            OnPropertyChanged(nameof(IsEraserMode));
            OnPropertyChanged(nameof(IsMoveMode));
            OnPropertyChanged(nameof(CanvasCursor));
            OnPropertyChanged(nameof(IsBrushSelected));
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
            var startupOverlay = this.FindControl<Border>("StartupOverlay");
            if (startupOverlay != null && startupOverlay.IsVisible)
                return Constants.AppName;

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

    /// <summary>Inverse of current zoom scale for zoom-independent UI elements.</summary>
    public double ZoomInverseFactor => 1.0 / _scale.ScaleX;

    /// <summary>Border thickness that remains constant regardless of zoom level.</summary>
    public Thickness ZoomIndependentBorderThickness => new Thickness(2.0 / _scale.ScaleX);

    /// <summary>Corner radius that remains constant regardless of zoom level.</summary>
    public CornerRadius ZoomIndependentCornerRadius => new CornerRadius(0);

    /// <summary>Application version string for the status bar.</summary>
    public string VersionText => $"v{Constants.AppVersion}";

    /// <summary>Memory usage summary for the status bar (loaded image count + working set).</summary>
    public string MemoryUsageText
    {
        get
        {
            int loaded = GridCells.Count(c => c.NeedsImage && c.Image != null);
            int total = GridCells.Count(c => c.NeedsImage);
            long mb = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            return total > 0 ? $"IMG {loaded}/{total} | {mb} MB" : $"{mb} MB";
        }
    }

    /// <summary>Number of currently selected items for the status bar.</summary>
    public string SelectionCountText
    {
        get
        {
            int count = _selectedCells.Count + _selectedAnnotations.Count;
            return count > 0 ? $"{count} selected" : "";
        }
    }

    public bool HasMultipleSelection => (_selectedCells.Count + _selectedAnnotations.Count) > 1;
    public bool HasSingleSelection => (_selectedCells.Count + _selectedAnnotations.Count) == 1;

    /// <summary>Current mode display text for the status bar.</summary>
    public string CurrentModeText => IsDrawMode ? "Annotation" : "Grid";

    /// <summary>Mode indicator color for the status bar — red for Annotation, blue for Grid.</summary>
    public string ModeIndicatorColor => IsDrawMode ? "#FF4444" : "#44AAFF";

    /// <summary>Material icon kind for the canvas cursor based on the current tool.</summary>
    public string CanvasCursor => CurrentTool switch
    {
        "Brush" => "🖌️",
        "Move" => "✥",
        "Text" => "T",
        "Arrow" => "→",
        "Rectangle" => "▭",
        "Ellipse" => "○",
        "Eraser" => "⌫",
        _ => "✏️"
    };

    /// <summary>Notifies the UI that all zoom-dependent properties have changed.</summary>
    private void NotifyZoomChanged()
    {
        OnPropertyChanged(nameof(ZoomLevelText));
        OnPropertyChanged(nameof(ZoomInverseFactor));
        OnPropertyChanged(nameof(ZoomIndependentBorderThickness));
        OnPropertyChanged(nameof(ZoomIndependentCornerRadius));
    }

    /// <summary>Tool selection properties for menu checkmarks.</summary>
    public bool IsBrushSelected => CurrentTool == "Brush";
    public bool IsTextSelected => CurrentTool == "Text";
    public bool IsArrowSelected => CurrentTool == "Arrow";
    public bool IsRectangleSelected => CurrentTool == "Rectangle";
    public bool IsEllipseSelected => CurrentTool == "Ellipse";
    public bool IsEraserSelected => CurrentTool == "Eraser";
    public bool IsMoveSelected => CurrentTool == "Move";

    // ───────── Annotation effect ─────────
    private string _annotationEffect = "None";
    public string AnnotationEffectMode
    {
        get => _annotationEffect;
        set
        {
            if (_annotationEffect == value)
                return;
            _annotationEffect = value;
            AnnotationShape.SetEffectMode(value switch
            {
                "Shadow" => AnnotationEffect.Shadow,
                "Outline" => AnnotationEffect.Outline,
                _ => AnnotationEffect.None
            });
            OnPropertyChanged(nameof(AnnotationEffectMode));
            OnPropertyChanged(nameof(IsAnnotationEffectNone));
            OnPropertyChanged(nameof(IsAnnotationEffectShadow));
            OnPropertyChanged(nameof(IsAnnotationEffectOutline));
            SaveUserSettings();
        }
    }
    public bool IsAnnotationEffectNone => _annotationEffect == "None";
    public bool IsAnnotationEffectShadow => _annotationEffect == "Shadow";
    public bool IsAnnotationEffectOutline => _annotationEffect == "Outline";

    #endregion

    #region Private State

    // Annotation drawing
    private AnnotationViewModel? _currentAnnotation;
    private readonly List<AnnotationViewModel> _selectedAnnotations = new();
    private bool _isDraggingAnnotations;
    private bool _isSelectingAnnotations;
    private Point _annotationSelectionStart;
    private Point _annotationDragStart;
    private List<(CellViewModel Cell, double StartX, double StartY)>? _annotationDragCellOriginals;
    private AnnotationViewModel? _editingTextAnnotation;
    private string? _editingTextAnnotationOriginalText;

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

    // Zoom toggle state (PureRef-style: double-click to zoom in, double-click again to restore)
    private double _savedTranslateX;
    private double _savedTranslateY;
    private double _savedScale;
    private CellViewModel? _zoomedToCell;
    private bool _canRestoreView;

    // Middle-button drag-to-zoom (Nuke-style)
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
    private bool _isAltDuplicateDrag;

    // Cell resize
    private bool _isResizing;
    private Point _resizeStartPos;
    private CellViewModel? _resizingCell;
    private int _resizeStartColSpan;
    private int _resizeStartRowSpan;

    // Placement preview (for backdrop creation)
    private bool _isShowingPlacementPreview;
    private double _previewX;
    private double _previewY;
    private int _previewColSpan;
    private int _previewRowSpan;
    private bool _previewIsValid;
    private CellViewModel? _pendingBackdrop;

    // Auto-scroll when dragging near edges
    private const double EdgeScrollThreshold = 50.0; // pixels from edge
    private const double EdgeScrollSpeed = 25.0; // pixels per tick
    private System.Timers.Timer? _edgeScrollTimer;
    private Point _lastPointerPosition;
    private bool _isEdgeScrolling;

    // Toast notification
    private System.Threading.CancellationTokenSource? _toastCts;

    // Viewport-aware LOD management (polls transform state to detect pan/zoom changes)
    private Avalonia.Threading.DispatcherTimer? _viewportLodTimer;
    private double _lastViewportTx = double.NaN;
    private double _lastViewportTy = double.NaN;
    private double _lastViewportScale = double.NaN;
    private double _lastViewportW = double.NaN;
    private double _lastViewportH = double.NaN;
    private int _lastViewportCellCount = -1;
    private bool _lodUpdateInProgress;

    #endregion


    #region Constructor

    /// <summary>Parameterless constructor required by Avalonia designer.</summary>
    public MainWindow() : this(false, null) { }

    public MainWindow(bool isViewMode, string? startFile)
    {
        // Compact the Large Object Heap on next GC to reduce memory fragmentation
        // from repeatedly allocated/disposed bitmap pixel buffers.
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
            System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

        DataContext = this;
        _isViewMode = isViewMode;
        InitializeComponent();

        LoadRecentBoards();
        LoadUserSettings();
        RecentBoardsList.ItemsSource = RecentBoards;

        _workspaceDir = Path.Combine(Constants.ConfigDirectory, "Assets");
        if (!Directory.Exists(_workspaceDir))
            Directory.CreateDirectory(_workspaceDir);

        GridCells.CollectionChanged += (s, e) => UpdateSelectionState();

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

        // Start the viewport LOD polling timer (recalculates image detail levels)
        InitViewportLodTimer();

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

            OnPropertyChanged(nameof(WindowTitle));

            string json = await File.ReadAllTextAsync(_currentBoardFile);

            // Dispose existing cell bitmaps and clear colour caches
            foreach (var c in GridCells)
                c.UnloadImage();
            ImageManager.ClearCaches();

            GridCells.Clear();
            Annotations.Clear();
            _selectedAnnotations.Clear();
            _currentAnnotation = null;
            _editingTextAnnotation = null;

            var (cells, annotations) = BoardSerializer.Deserialize(json, _currentBoardFile);
            foreach (var cell in cells)
                GridCells.Add(cell);
            foreach (var ann in annotations)
            {
                ann.IsInDrawMode = IsDrawMode;
                Annotations.Add(ann);
            }

            _hasUnsavedChanges = false;
            Title = $"{Constants.AppName} - {Path.GetFileName(_currentBoardFile)}" + (_isViewMode ? " [VIEW MODE]" : "");
            AddRecentBoard(_currentBoardFile);

            _undoStack.Clear();
            _redoStack.Clear();
            SaveBoardData();

            // For cells loaded from older .cgrb files without a saved PlaceholderColor,
            // kick off background average-colour computation so the placeholder rects
            // show a meaningful colour instead of default dark grey.
            foreach (var cell in GridCells)
            {
                if (cell.NeedsImage && cell.PlaceholderColor == "#FF2A2A2A")
                    _ = cell.EnsurePlaceholderColorAsync();
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ShowAll_Click(null, null!);
                ScheduleViewportUpdate();
            });
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

        string json = BoardSerializer.Serialize(GridCells, Annotations, _currentBoardFile);

        if (!_isRestoringState && !_isViewMode)
        {
            if (_undoStack.Count == 0 || _undoStack.Peek() != json)
            {
                _undoStack.Push(json);

                // Trim undo stack to prevent unbounded memory growth
                if (_undoStack.Count > Constants.MaxUndoDepth)
                {
                    var items = _undoStack.ToArray(); // [newest, ..., oldest]
                    _undoStack.Clear();
                    for (int i = Constants.MaxUndoDepth - 1; i >= 0; i--)
                        _undoStack.Push(items[i]);
                }

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
        string path = Path.Combine(Constants.ConfigDirectory, Constants.RecentBoardsFileName);

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

        string confDir = Constants.ConfigDirectory;
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
                IsActive = !string.IsNullOrEmpty(_currentBoardFile) &&
                           Path.GetFullPath(file) == Path.GetFullPath(_currentBoardFile)
            });
        }
        OnPropertyChanged(nameof(HasBoardFilesInDirectory));
    }

    #endregion

    #region User Settings

    private void LoadUserSettings()
    {
        try
        {
            string path = Path.Combine(Constants.ConfigDirectory, Constants.UserSettingsFileName);

            if (!File.Exists(path))
                return;

            string json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            if (settings != null)
            {
                AnnotationEffectMode = settings.AnnotationEffect ?? "None";

            }
        }
        catch { /* ignore corrupt settings */ }
    }

    private async void SaveUserSettings()
    {
        try
        {
            string confDir = Constants.ConfigDirectory;
            if (!Directory.Exists(confDir))
                Directory.CreateDirectory(confDir);

            string confPath = Path.Combine(confDir, Constants.UserSettingsFileName);
            var settings = new UserSettings
            {
                AnnotationEffect = _annotationEffect
            };
            await File.WriteAllTextAsync(confPath, JsonSerializer.Serialize(settings));
        }
        catch { /* non-critical */ }
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
    /// Finds an existing content-layer cell at the grid position, or creates one.
    /// Board elements (Backdrop, Label) at the same position are ignored.
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

    #region Selection Helpers

    /// <summary>Deselects all cells and annotations, clears both selection lists.</summary>
    private void ClearSelection()
    {
        foreach (var c in _selectedCells)
            c.IsSelected = false;
        _selectedCells.Clear();
        foreach (var a in _selectedAnnotations)
            a.IsSelected = false;
        _selectedAnnotations.Clear();
        UpdateSelectionState();
    }

    public void UpdateSelectionState()
    {
        OnPropertyChanged(nameof(SelectionCountText));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(HasSingleSelection));

        bool multi = HasMultipleSelection;
        bool single = HasSingleSelection;
        foreach (var cell in GridCells)
        {
            cell.HasMultipleSelection = multi;
            cell.HasSingleSelection = single;
        }
    }

    #endregion

    #region Highlight Helpers

    /// <summary>Briefly highlights a cell to draw attention to it (e.g. after paste).</summary>
    private async void HighlightCell(CellViewModel cell)
    {
        cell.IsHighlighted = true;
        await Task.Delay(800);
        cell.IsHighlighted = false;
    }

    #endregion

    #region Placement Preview Helpers

    /// <summary>Shows the placement preview rectangle at the specified grid-aligned position.</summary>
    private void ShowPlacementPreview(double x, double y, int colSpan, int rowSpan, int collisionLayer)
    {
        var previewBorder = this.FindControl<Border>("PlacementPreviewBorder");
        if (previewBorder == null)
            return;

        _isShowingPlacementPreview = true;
        _previewX = x;
        _previewY = y;
        _previewColSpan = colSpan;
        _previewRowSpan = rowSpan;

        // Check if placement is valid (no collision)
        _previewIsValid = GridLayoutService.IsSpaceEmpty(GridCells, x, y, colSpan, rowSpan, collisionLayer);

        // Update visual appearance based on validity
        previewBorder.BorderBrush = _previewIsValid
            ? Brushes.LightGreen
            : Brushes.Red;
        previewBorder.Background = _previewIsValid
            ? new SolidColorBrush(Color.FromArgb(48, 144, 238, 144))
            : new SolidColorBrush(Color.FromArgb(48, 255, 68, 68));

        Canvas.SetLeft(previewBorder, x);
        Canvas.SetTop(previewBorder, y);
        previewBorder.Width = colSpan * Constants.GridSize;
        previewBorder.Height = rowSpan * Constants.GridSize;
        previewBorder.IsVisible = true;
    }

    /// <summary>Hides the placement preview rectangle.</summary>
    private void HidePlacementPreview()
    {
        var previewBorder = this.FindControl<Border>("PlacementPreviewBorder");
        if (previewBorder == null)
            return;

        _isShowingPlacementPreview = false;
        previewBorder.IsVisible = false;
        _pendingBackdrop = null;
    }

    /// <summary>Updates the placement preview position based on pointer movement.</summary>
    private void UpdatePlacementPreview(Point canvasPoint)
    {
        if (!_isShowingPlacementPreview || _pendingBackdrop == null)
            return;

        // Snap to grid
        int gridX = (int)(Math.Floor(canvasPoint.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(canvasPoint.Y / Constants.GridSize) * Constants.GridSize);

        ShowPlacementPreview(gridX, gridY, _previewColSpan, _previewRowSpan, _pendingBackdrop.CollisionLayer);
    }

    /// <summary>Attempts to place the pending backdrop at the preview location if valid.</summary>
    private bool TryPlacePendingBackdrop()
    {
        if (!_isShowingPlacementPreview || _pendingBackdrop == null || !_previewIsValid)
            return false;

        _pendingBackdrop.CanvasX = _previewX;
        _pendingBackdrop.CanvasY = _previewY;
        GridCells.Add(_pendingBackdrop);
        MarkUnsaved();
        SaveBoardData();
        HidePlacementPreview();
        return true;
    }

    #endregion

    #region Edge Scroll Helpers

    /// <summary>Starts edge scrolling if the pointer is near the viewport edge.</summary>
    private void StartEdgeScrollIfNeeded(Point screenPoint)
    {
        if (CanvasBorder == null)
            return;

        var bounds = CanvasBorder.Bounds;
        bool nearEdge = screenPoint.X < EdgeScrollThreshold ||
                        screenPoint.Y < EdgeScrollThreshold ||
                        screenPoint.X > bounds.Width - EdgeScrollThreshold ||
                        screenPoint.Y > bounds.Height - EdgeScrollThreshold;

        if (nearEdge && !_isEdgeScrolling)
        {
            _isEdgeScrolling = true;
            if (_edgeScrollTimer == null)
            {
                _edgeScrollTimer = new System.Timers.Timer(16); // ~60fps
                _edgeScrollTimer.Elapsed += EdgeScrollTimer_Elapsed;
            }
            _edgeScrollTimer.Start();
        }
        else if (!nearEdge && _isEdgeScrolling)
        {
            StopEdgeScroll();
        }
    }

    /// <summary>Stops the edge scroll timer.</summary>
    private void StopEdgeScroll()
    {
        _isEdgeScrolling = false;
        _edgeScrollTimer?.Stop();
    }

    /// <summary>Timer callback that performs the actual edge scrolling.</summary>
    private void EdgeScrollTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!_isEdgeScrolling || CanvasBorder == null)
                return;

            var bounds = CanvasBorder.Bounds;
            double dx = 0, dy = 0;

            if (_lastPointerPosition.X < EdgeScrollThreshold)
                dx = EdgeScrollSpeed;
            else if (_lastPointerPosition.X > bounds.Width - EdgeScrollThreshold)
                dx = -EdgeScrollSpeed;

            if (_lastPointerPosition.Y < EdgeScrollThreshold)
                dy = EdgeScrollSpeed;
            else if (_lastPointerPosition.Y > bounds.Height - EdgeScrollThreshold)
                dy = -EdgeScrollSpeed;

            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
            {
                _translate.X += dx;
                _translate.Y += dy;
            }
        });
    }

    #endregion

    #region Toast Notification

    /// <summary>Shows a brief toast message at the bottom of the window.</summary>
    private async void ShowToast(string message)
    {
        var border = this.FindControl<Border>("ToastBorder");
        var text = this.FindControl<TextBlock>("ToastText");
        if (border == null || text == null)
            return;

        // Cancel any existing toast
        _toastCts?.Cancel();
        _toastCts = new System.Threading.CancellationTokenSource();
        var token = _toastCts.Token;

        text.Text = message;
        border.IsVisible = true;
        border.Opacity = 1;

        try
        {
            await Task.Delay(1500, token);
            border.Opacity = 0;
            await Task.Delay(250, token);
            border.IsVisible = false;
        }
        catch (TaskCanceledException)
        {
            // New toast replaced this one — that's fine
        }
    }

    #endregion

    #region Viewport LOD Management

    /// <summary>
    /// Initialises a <see cref="Avalonia.Threading.DispatcherTimer"/> that polls the
    /// current pan/zoom transform every 200 ms and triggers an LOD recalculation
    /// whenever the viewport changes. Polling avoids having to modify every
    /// zoom/pan code-path in Canvas.cs.
    /// </summary>
    private void InitViewportLodTimer()
    {
        _viewportLodTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _viewportLodTimer.Tick += ViewportLodTimer_Tick;
        _viewportLodTimer.Start();
    }

    /// <summary>Timer callback — fires on the UI thread.</summary>
    private void ViewportLodTimer_Tick(object? sender, EventArgs e)
    {
        double tx = _translate.X;
        double ty = _translate.Y;
        double sc = _scale.ScaleX;
        int count = GridCells.Count;
        double vw = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double vh = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        // Skip if nothing relevant changed since last tick.
        if (tx == _lastViewportTx && ty == _lastViewportTy
            && sc == _lastViewportScale && count == _lastViewportCellCount
            && vw == _lastViewportW && vh == _lastViewportH)
            return;

        _lastViewportTx = tx;
        _lastViewportTy = ty;
        _lastViewportScale = sc;
        _lastViewportCellCount = count;
        _lastViewportW = vw;
        _lastViewportH = vh;

        if (_lodUpdateInProgress)
            return;
        _ = UpdateViewportLodAsync();


    }

    /// <summary>
    /// Forces the next timer tick to recalculate LODs regardless of whether
    /// the cached transform values have changed.
    /// </summary>
    public void ScheduleViewportUpdate()
    {
        _lastViewportScale = double.NaN;
    }

    /// <summary>
    /// Iterates every image/video cell and transitions it to the LOD tier
    /// appropriate for its current on-screen size and visibility.
    /// Heavy I/O (bitmap decode) is performed on the thread-pool; only the
    /// final <c>Image</c> property assignment happens on the UI thread.
    /// </summary>
    private async Task UpdateViewportLodAsync()
    {
        _lodUpdateInProgress = true;
        try
        {
            double scale = _scale.ScaleX;
            double tx = _translate.X;
            double ty = _translate.Y;

            // Determine viewport size in screen pixels (same pattern as ShowAll_Click).
            double viewW = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
            double viewH = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;
            if (viewW <= 0 || viewH <= 0)
                return;

            // Convert viewport rect to canvas coordinates.
            //   screen = (canvas + translate) × scale
            //   canvas = screen / scale − translate
            double vpLeft = -tx;
            double vpTop = -ty;
            double vpRight = viewW / scale - tx;
            double vpBottom = viewH / scale - ty;

            // Generous margin (2 grid cells) to pre-load cells about to scroll into view.
            double margin = Constants.GridSize * 2;
            vpLeft -= margin;
            vpTop -= margin;
            vpRight += margin;
            vpBottom += margin;

            var tasks = new System.Collections.Generic.List<Task>();

            foreach (var cell in GridCells)
            {
                if (!cell.NeedsImage)
                    continue;

                // Cell bounding rectangle in canvas coordinates.
                double cellLeft = cell.CanvasX;
                double cellTop = cell.CanvasY;
                double cellRight = cellLeft + cell.PixelWidth;
                double cellBottom = cellTop + cell.PixelHeight;

                bool isVisible = cellRight > vpLeft && cellLeft < vpRight
                              && cellBottom > vpTop && cellTop < vpBottom;

                double cellScreenWidth = cell.PixelWidth * scale;

                var targetLod = ImageManager.DetermineLod(cellScreenWidth, isVisible);

                if (targetLod != cell.CurrentLod)
                    tasks.Add(cell.ApplyLodAsync(targetLod));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
                // Nudge the GC to reclaim large bitmap buffers from disposed images.
                // Optimized mode lets the GC decide whether collection is worthwhile.
                GC.Collect(2, GCCollectionMode.Optimized, false);
            }
        }
        finally
        {
            _lodUpdateInProgress = false;
        }
    }

    #endregion
}
