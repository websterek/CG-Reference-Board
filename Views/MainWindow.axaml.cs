using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Controls;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Layers;
using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.Layers.Infrastructure;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

/// <summary>
/// Main application window — thin View that delegates all state to <see cref="MainWindowViewModel"/>.
///
/// UI event handlers are split across partial class files:
///   MainWindow.Canvas.cs      – canvas pointer handlers (pan, zoom, hover, marquee)
///   MainWindow.Cells.cs       – cell drag, resize, enter/exit
///   MainWindow.Annotations.cs – annotation drawing, editing, erasing
///   MainWindow.Commands.cs    – menu clicks, keyboard shortcuts, drag-drop, overlay
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ── ViewModel ─────────────────────────────────────────────────────────────

    /// <summary>The primary ViewModel. All application state lives here.</summary>
    public MainWindowViewModel Vm { get; }

    // ── Zoom-dependent View-only properties ───────────────────────────────────
    // These depend on _scale which stays in the View (pan/zoom is pure UI).

    /// <summary>Current zoom level as percentage string for the status bar.</summary>
    public string ZoomLevelText => $"{_scale.ScaleX * 100:F0}%";

    /// <summary>Inverse of current zoom scale for zoom-independent UI elements.</summary>
    public double ZoomInverseFactor => 1.0 / _scale.ScaleX;

    /// <summary>Border thickness that remains constant regardless of zoom level.</summary>
    public Thickness ZoomIndependentBorderThickness => new Thickness(2.0 / _scale.ScaleX);

    /// <summary>Corner radius that remains constant regardless of zoom level.</summary>
    public CornerRadius ZoomIndependentCornerRadius => new CornerRadius(0);

    /// <summary>Hide the dot/grid background below 25% zoom — VisualBrush tile count explodes at low scale.</summary>
    public bool IsCanvasBackgroundVisible => _scale.ScaleX >= 0.25;

    /// <summary>Delegates to <see cref="MainWindowViewModel.IsViewMode"/> for bindings inside DataTemplates that use RelativeSource AncestorType=views:MainWindow.</summary>
    public bool IsViewMode => Vm.IsViewMode;

    /// <summary>Number of currently selected items for the status bar.</summary>
    public string SelectionCountText
    {
        get
        {
            int count = _selectedCells.Count + _selectedAnnotations.Count;
            return count > 0 ? $"{count} selected" : "";
        }
    }

    /// <summary>Memory usage summary for the status bar (loaded image count + working set).</summary>
    public string MemoryUsageText
    {
        get
        {
            int loaded = Vm.GridCells.Count(c => c.NeedsImage && c.Image != null);
            int total = Vm.GridCells.Count(c => c.NeedsImage);
            _thisProcess.Refresh();
            long mb = _thisProcess.WorkingSet64 / (1024 * 1024);
            return total > 0 ? $"IMG {loaded}/{total} | {mb} MB" : $"{mb} MB";
        }
    }

    public bool HasMultipleSelection => (_selectedCells.Count + _selectedAnnotations.Count) > 1;
    public bool HasSingleSelection => (_selectedCells.Count + _selectedAnnotations.Count) == 1;

    // Cached process handle used by MemoryUsageText.
    private static readonly System.Diagnostics.Process _thisProcess =
        System.Diagnostics.Process.GetCurrentProcess();

    // ── INPC for View-only properties ─────────────────────────────────────────

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // ── Private State ─────────────────────────────────────────────────────────

    // Annotation drawing
    private AnnotationViewModel? _currentAnnotation;
    private readonly List<AnnotationViewModel> _selectedAnnotations = new();
    private bool _isDraggingAnnotations;
    private bool _isDraggingFromSystem;
    private bool _isSelectingAnnotations;
    private Point _annotationSelectionStart;
    private Point _annotationDragStart;
    private List<(CellViewModel Cell, double StartX, double StartY)>? _annotationDragCellOriginals;
    private AnnotationViewModel? _editingTextAnnotation;
    private string? _editingTextAnnotationOriginalText;

    // Cell interaction
    private CellViewModel? _hoveredCell;
    private CellViewModel? _editingTextCell;
    private CellViewModel? _draggingCell;

    // Pan/Zoom
    private bool _isPanning;
    private bool _isShiftPanPending;
    private Point _panStartPoint;
    private readonly TranslateTransform _translate = new(0, 0);
    private readonly ScaleTransform _scale = new(1, 1);

    // Zoom toggle state (PureRef-style)
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
    private bool _selectionAdditive;

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
    private AnnotationViewModel? _pendingAltDuplicateAnnotation;

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

    // Spatial index for O(1) grid-position lookups
    private readonly Dictionary<(int gridX, int gridY), CellViewModel> _cellSpatialIndex = new();

    // Auto-scroll when dragging near edges
    private const double EdgeScrollThreshold = 50.0;
    private const double EdgeScrollSpeed = 25.0;
    private System.Timers.Timer? _edgeScrollTimer;
    private Point _lastPointerPosition;
    private bool _isEdgeScrolling;

    // Toast notification
    private System.Threading.CancellationTokenSource? _toastCts;

    // Viewport-aware LOD management
    private Avalonia.Threading.DispatcherTimer? _viewportLodTimer;
    private Avalonia.Threading.DispatcherTimer? _lodDebounceTimer;
    private bool _lodUpdatePending;
    private bool _isLodUpdateScheduled;
    private double _lastViewportTx = double.NaN;
    private double _lastViewportTy = double.NaN;
    private double _lastViewportScale = double.NaN;
    private double _lastViewportW = double.NaN;
    private double _lastViewportH = double.NaN;
    private int _lastViewportCellCount = -1;
    private int _lastAnnotationCount = -1;

    // Batched zoom property notifications
    private bool _zoomNotificationPending;
    private Avalonia.Threading.DispatcherTimer? _zoomNotificationTimer;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Parameterless constructor required by Avalonia designer.</summary>
    public MainWindow() : this(false, null) { }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        ResetTransientPointerState(cancelActiveTransform: true);
        UpdateSelectionState();
    }

    private void CanvasBorder_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ResetTransientPointerState(cancelActiveTransform: true);
        UpdateSelectionState();
    }

    public MainWindow(MainWindowViewModel vm)
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
            System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

        Vm = vm;
        DataContext = Vm;

        InitializeComponent();

        // Wire ViewModel events to View callbacks
        Vm.AlwaysOnTopChanged += topmost => Topmost = topmost;
        Vm.ToastRequested += ShowToast;
        Vm.ViewportUpdateRequested += ScheduleViewportUpdate;
        Vm.SelectionResetRequested += ClearLocalSelectionState;
        Vm.TransformContextChanging += CancelActiveInteractionForContextChange;
        Vm.TransformService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Vm.TransformService.IsVisible) or nameof(Vm.TransformService.Bounds))
            {
                UpdateTransformOverlayLayout();
            }
        };
        Vm.StartupOverlayHideRequested += () =>
        {
            var overlay = this.FindControl<Border>("StartupOverlay");
            if (overlay != null) overlay.IsVisible = false;
        };

        CacheCanvasControls();
        UpdateTransformOverlayLayout();

        try
        {
            var canvasBorder = this.FindControl<Border>("CanvasBorder");
            if (canvasBorder != null)
            {
                canvasBorder.AddHandler(InputElement.PointerPressedEvent,
                    new EventHandler<PointerPressedEventArgs>(CanvasBorder_Tunneled_PointerPressed),
                    Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
        }
        catch { }

        Vm.LoadRecentBoards();
        Vm.LoadUserSettings();
        RecentBoardsList.ItemsSource = Vm.RecentBoards;

        if (!Directory.Exists(Vm.WorkspaceDir))
            Directory.CreateDirectory(Vm.WorkspaceDir);

        Vm.GridCells.CollectionChanged += GridCells_CollectionChanged;

        void GridCells_CollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateSelectionState();

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                _cellSpatialIndex.Clear();
                Vm.LayerManager.Clear();
                foreach (var cell in Vm.GridCells)
                {
                    AddCellToSpatialIndex(cell);
                    Vm.LayerManager.AddCell(cell);
                }
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (CellViewModel cell in e.OldItems)
                    {
                        RemoveCellFromSpatialIndex(cell);
                        Vm.LayerManager.RemoveCell(cell);
                        cell.PropertyChanged -= Cell_TypeChanged;
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (CellViewModel cell in e.NewItems)
                    {
                        AddCellToSpatialIndex(cell);
                        if (Vm.LayerManager.AddCell(cell) == null && cell.Type == CellType.None)
                        {
                            cell.PropertyChanged += Cell_TypeChanged;
                        }
                    }
                }
            }
        }

        Vm.Annotations.CollectionChanged += Annotations_CollectionChanged;

        void Annotations_CollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateSelectionState();

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                Vm.LayerManager.Annotations.Items.Clear();
                foreach (var ann in Vm.Annotations)
                    Vm.LayerManager.Annotations.Items.Add(ann);
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (AnnotationViewModel ann in e.OldItems)
                        Vm.LayerManager.Annotations.Items.Remove(ann);
                }
                if (e.NewItems != null)
                {
                    foreach (AnnotationViewModel ann in e.NewItems)
                        Vm.LayerManager.Annotations.Items.Add(ann);
                }
            }
        }

        // Wire layer visibility changes to cell visual state
        foreach (var layer in Vm.LayerManager.ContentLayers)
        {
            if (layer is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += (_, e) => OnLayerPropertyChanged(layer, e);
        }

        // Set up pan/zoom transform
        var tg = new TransformGroup();
        tg.Children.Add(_translate);
        tg.Children.Add(_scale);

        var mainCanvas = this.FindControl<Canvas>("MainCanvas");
        if (mainCanvas != null)
            mainCanvas.RenderTransform = tg;

        var cursorIcon = this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            Canvas.SetLeft(cursorIcon, -100);
            Canvas.SetTop(cursorIcon, -100);
        }

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        var canvasBorderDnd = this.FindControl<Border>("CanvasBorder");
        if (canvasBorderDnd != null)
        {
            DragDrop.SetAllowDrop(canvasBorderDnd, true);
            canvasBorderDnd.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            canvasBorderDnd.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            canvasBorderDnd.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            canvasBorderDnd.AddHandler(DragDrop.DropEvent, OnDrop);
        }

        InitViewportLodTimer();

        Closing += OnWindowClosing;
    }

    public MainWindow(bool isViewMode, string? startFile)
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
            System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

        Vm = new MainWindowViewModel(isViewMode);
        DataContext = Vm;

        InitializeComponent();

        // Wire ViewModel events to View callbacks
        Vm.AlwaysOnTopChanged += topmost => Topmost = topmost;
        Vm.ToastRequested += ShowToast;
        Vm.ViewportUpdateRequested += ScheduleViewportUpdate;
        Vm.SelectionResetRequested += ClearLocalSelectionState;
        Vm.TransformContextChanging += CancelActiveInteractionForContextChange;
        Vm.TransformService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Vm.TransformService.IsVisible) or nameof(Vm.TransformService.Bounds))
            {
                UpdateTransformOverlayLayout();
            }
        };
        Vm.StartupOverlayHideRequested += () =>
        {
            var overlay = this.FindControl<Border>("StartupOverlay");
            if (overlay != null) overlay.IsVisible = false;
        };

        CacheCanvasControls();
        UpdateTransformOverlayLayout();

        try
        {
            var canvasBorder = this.FindControl<Border>("CanvasBorder");
            if (canvasBorder != null)
            {
                canvasBorder.AddHandler(InputElement.PointerPressedEvent,
                    new EventHandler<PointerPressedEventArgs>(CanvasBorder_Tunneled_PointerPressed),
                    Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
        }
        catch { }

        Vm.LoadRecentBoards();
        Vm.LoadUserSettings();
        RecentBoardsList.ItemsSource = Vm.RecentBoards;

        if (!Directory.Exists(Vm.WorkspaceDir))
            Directory.CreateDirectory(Vm.WorkspaceDir);

        Vm.GridCells.CollectionChanged += GridCells_CollectionChanged;

        void GridCells_CollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateSelectionState();

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                _cellSpatialIndex.Clear();
                Vm.LayerManager.Clear();
                foreach (var cell in Vm.GridCells)
                {
                    AddCellToSpatialIndex(cell);
                    Vm.LayerManager.AddCell(cell);
                }
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (CellViewModel cell in e.OldItems)
                    {
                        RemoveCellFromSpatialIndex(cell);
                        Vm.LayerManager.RemoveCell(cell);
                        cell.PropertyChanged -= Cell_TypeChanged;
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (CellViewModel cell in e.NewItems)
                    {
                        AddCellToSpatialIndex(cell);
                        if (Vm.LayerManager.AddCell(cell) == null && cell.Type == CellType.None)
                        {
                            // Cell has no type yet (e.g. being set up before a download).
                            // Subscribe so we can route it into the correct layer once the type is set.
                            cell.PropertyChanged += Cell_TypeChanged;
                        }
                    }
                }
            }
        }

        Vm.Annotations.CollectionChanged += Annotations_CollectionChanged;

        void Annotations_CollectionChanged(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateSelectionState();

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                Vm.LayerManager.Annotations.Items.Clear();
                foreach (var ann in Vm.Annotations)
                    Vm.LayerManager.Annotations.Items.Add(ann);
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (AnnotationViewModel ann in e.OldItems)
                        Vm.LayerManager.Annotations.Items.Remove(ann);
                }
                if (e.NewItems != null)
                {
                    foreach (AnnotationViewModel ann in e.NewItems)
                        Vm.LayerManager.Annotations.Items.Add(ann);
                }
            }
        }

        // Wire layer visibility changes to cell visual state
        foreach (var layer in Vm.LayerManager.ContentLayers)
        {
            if (layer is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += (_, e) => OnLayerPropertyChanged(layer, e);
        }

        // Set up pan/zoom transform
        var tg = new TransformGroup();
        tg.Children.Add(_translate);
        tg.Children.Add(_scale);

        var mainCanvas = this.FindControl<Canvas>("MainCanvas");
        if (mainCanvas != null)
            mainCanvas.RenderTransform = tg;

        var cursorIcon = this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            Canvas.SetLeft(cursorIcon, -100);
            Canvas.SetTop(cursorIcon, -100);
        }

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        var canvasBorderDnd = this.FindControl<Border>("CanvasBorder");
        if (canvasBorderDnd != null)
        {
            DragDrop.SetAllowDrop(canvasBorderDnd, true);
            canvasBorderDnd.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            canvasBorderDnd.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            canvasBorderDnd.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            canvasBorderDnd.AddHandler(DragDrop.DropEvent, OnDrop);
        }

        InitViewportLodTimer();

        if (!string.IsNullOrEmpty(startFile) && File.Exists(startFile))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Vm.LoadBoardFromFile(startFile));
        }

        Closing += OnWindowClosing;
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (Vm.ClosingConfirmed || !Vm.HasUnsavedChanges || e.IsProgrammatic)
            return;

        e.Cancel = true;

        bool discard = await ConfirmDiscardChanges();
        if (discard)
        {
            Vm.ClosingConfirmed = true;
            Close();
        }
    }

    private void CancelActiveInteractionForContextChange()
    {
        CancelActiveTransform();
        CancelPendingAnnotationAltDuplicateDrag();
        CancelLegacyAltDuplicateDrag();
        UpdateSelectionState();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewportLodTimer?.Stop();
        _lodDebounceTimer?.Stop();
        _zoomNotificationTimer?.Stop();
        _edgeScrollTimer?.Stop();
        _edgeScrollTimer?.Dispose();
        Vm.Cleanup();
    }

    private async Task<bool> ConfirmDiscardChanges()
    {
        if (!Vm.HasUnsavedChanges)
            return true;

        bool result = false;

        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 380,
            Height = 145,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E")),
        };

        var msgText = new TextBlock
        {
            Text = "You have unsaved changes. Discard them and continue?",
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#EEEEEE")),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(24, 20, 24, 0),
        };

        var discardBtn = new Button { Content = "Discard Changes", Margin = new Thickness(0, 0, 8, 0) };
        var cancelBtn = new Button { Content = "Cancel" };

        discardBtn.Click += (_, _) => { result = true; dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = false; dialog.Close(); };

        var btnRow = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(24, 16, 24, 0),
            Spacing = 8,
        };
        btnRow.Children.Add(discardBtn);
        btnRow.Children.Add(cancelBtn);

        var layout = new Avalonia.Controls.StackPanel();
        layout.Children.Add(msgText);
        layout.Children.Add(btnRow);

        dialog.Content = layout;
        await dialog.ShowDialog(this);
        return result;
    }

    // ── Layer Management ──────────────────────────────────────────────────────

    private void OnLayerPropertyChanged(IContentLayer layer, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IContentLayer.IsVisible))
            return;
        foreach (var cell in layer.Cells)
            cell.IsLayerVisible = layer.IsVisible;
    }

    // ── Zoom notifications ────────────────────────────────────────────────────

    private void NotifyZoomChanged()
    {
        if (_zoomNotificationPending)
            return;

        _zoomNotificationPending = true;

        if (_zoomNotificationTimer == null)
        {
            _zoomNotificationTimer = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _zoomNotificationTimer.Tick += ZoomNotificationTimer_Tick;
        }

        _zoomNotificationTimer.Start();
    }

    private void ZoomNotificationTimer_Tick(object? sender, EventArgs e)
    {
        _zoomNotificationTimer?.Stop();
        _zoomNotificationPending = false;

        OnPropertyChanged(nameof(ZoomLevelText));
        OnPropertyChanged(nameof(ZoomInverseFactor));
        OnPropertyChanged(nameof(ZoomIndependentBorderThickness));
        OnPropertyChanged(nameof(ZoomIndependentCornerRadius));
        OnPropertyChanged(nameof(IsCanvasBackgroundVisible));
        UpdateTransformOverlayLayout();

        CGReferenceBoard.Controls.AnnotationShape.SetScale(_scale.ScaleX);

        var canvas = this.FindControl<Avalonia.Controls.Canvas>("MainCanvas");
        if (canvas != null)
        {
            var mode = _scale.ScaleX < 0.35
                ? Avalonia.Media.Imaging.BitmapInterpolationMode.LowQuality
                : _scale.ScaleX < 1.0
                    ? Avalonia.Media.Imaging.BitmapInterpolationMode.MediumQuality
                    : Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality;
            Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(canvas, mode);
        }
    }

    // ── Grid Cell Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Fires when a cell's property changes after it was added to GridCells with Type=None.
    /// Routes the cell into the correct layer once its Type is assigned.
    /// </summary>
    private void Cell_TypeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CellViewModel.Type))
            return;
        if (sender is not CellViewModel cell)
            return;
        if (cell.Type == CellType.None)
            return;

        // Unsubscribe — only need to route once.
        cell.PropertyChanged -= Cell_TypeChanged;
        Vm.LayerManager.AddCell(cell);
    }

    private void AddCellToSpatialIndex(CellViewModel cell)
    {
        int gridX = (int)cell.CanvasX;
        int gridY = (int)cell.CanvasY;
        _cellSpatialIndex[(gridX, gridY)] = cell;
    }

    private void RemoveCellFromSpatialIndex(CellViewModel cell)
    {
        int gridX = (int)cell.CanvasX;
        int gridY = (int)cell.CanvasY;
        _cellSpatialIndex.Remove((gridX, gridY));
    }

    private CellViewModel GetOrCreateCellAt(Point canvasPoint)
    {
        int gridX = (int)(Math.Floor(canvasPoint.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(canvasPoint.Y / Constants.GridSize) * Constants.GridSize);

        if (_cellSpatialIndex.TryGetValue((gridX, gridY), out var existing))
            return existing;

        var newCell = new CellViewModel { CanvasX = gridX, CanvasY = gridY };
        Vm.GridCells.Add(newCell);
        Vm.MarkUnsaved();
        return newCell;
    }

    private CellViewModel GetOrCreateContentCellAt(Point canvasPoint)
    {
        int gridX = (int)(Math.Floor(canvasPoint.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(canvasPoint.Y / Constants.GridSize) * Constants.GridSize);

        if (_cellSpatialIndex.TryGetValue((gridX, gridY), out var existing) && !existing.IsBoardElement)
            return existing;

        var newCell = new CellViewModel { CanvasX = gridX, CanvasY = gridY };
        Vm.GridCells.Add(newCell);
        Vm.MarkUnsaved();
        return newCell;
    }

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

        var bounds = CanvasBorder.Bounds;
        var centerPos = new Point(
            bounds.Width / 2 / _scale.ScaleX - _translate.X,
            bounds.Height / 2 / _scale.ScaleY - _translate.Y);
        return GetOrCreateCellAt(centerPos);
    }

    // ── Image / Video Loading ─────────────────────────────────────────────────

    private async void LoadImageToCell(CellViewModel cell, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return;

        string destDir = Path.Combine(Vm.WorkspaceDir, "images");
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
        Vm.MarkUnsaved();
        Vm.SaveBoardData();
    }

    private async Task DownloadMediaToCell(CellViewModel cell, string url)
    {
        cell.SetText($"Checking availability...\n{url}");

        if (!await YtDlpService.IsVideoAvailableAsync(url))
        {
            cell.SetText(url);
            return;
        }

        cell.SetText($"Downloading...\n{url}");
        cell.IsDownloading = true;
        cell.DownloadProgress = 0f;
        cell.DownloadStatusText = "Starting...";

        string mediaDir = Path.Combine(Vm.WorkspaceDir, "videos");

        void OnProgress(float percent, string status)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                cell.DownloadProgress = percent;
                cell.DownloadStatusText = status;
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        var result = await YtDlpService.DownloadMediaAsync(url, mediaDir, onProgress: OnProgress);

        cell.IsDownloading = false;
        cell.DownloadProgress = 0f;
        cell.DownloadStatusText = "Downloading...";

        if (result.Success)
        {
            if (result.IsVideo)
            {
                cell.SetVideo(result.MediaPath!, result.ThumbnailPath!);
            }
            else
            {
                if (result.MediaPath == null)
                {
                    cell.SetText(url);
                    return;
                }
                var imgDir = Path.Combine(Vm.WorkspaceDir, "images");
                Directory.CreateDirectory(imgDir);
                string destPath = Path.Combine(imgDir, Path.GetFileName(result.MediaPath));
                if (result.MediaPath != destPath && !File.Exists(destPath))
                    File.Move(result.MediaPath, destPath);
                cell.SetImage(destPath);
            }
            Vm.MarkUnsaved();
            Vm.SaveBoardData();
        }
        else
        {
            cell.SetText(url);
        }
    }

    // ── Performance helpers ───────────────────────────────────────────────────

    private void DisableCellHitTesting()
    {
        foreach (var cell in Vm.GridCells)
            cell.IsHitTestEnabled = false;
        foreach (var ann in Vm.Annotations)
            ann.IsHitTestEnabled = false;
    }

    private void EnableCellHitTesting()
    {
        foreach (var cell in Vm.GridCells)
            cell.IsHitTestEnabled = true;
        foreach (var ann in Vm.Annotations)
            ann.IsHitTestEnabled = true;
    }

    // ── Selection Helpers ─────────────────────────────────────────────────────

    private void ClearSelection()
    {
        ClearLocalSelectionState();
        Vm.SelectionService.ClearSelection();
        UpdateSelectionState();
    }

    private void ClearLocalSelectionState()
    {
        foreach (var c in _selectedCells)
            c.IsSelected = false;
        _selectedCells.Clear();
        foreach (var a in _selectedAnnotations)
            a.IsSelected = false;
        _selectedAnnotations.Clear();
    }

    private void CanvasBorder_Tunneled_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled)
            return;

        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsMiddleButtonPressed)
            return;

        _isPanning = true;
        _panStartPoint = e.GetPosition(this);
        _middleZoomStartY = e.GetPosition(this).Y;

        try
        {
            var canvasBorder = this.FindControl<Border>("CanvasBorder");
            if (canvasBorder != null)
            {
                ApplyPanCursor(canvasBorder);
                e.Pointer.Capture(canvasBorder);
            }
        }
        catch { }

        e.Handled = true;
    }

    public void UpdateSelectionState()
    {
        // Sync to SelectionService
        Vm.SelectionService.SelectRange(_selectedCells, _selectedAnnotations);
        UpdateTransformOverlayLayout();

        OnPropertyChanged(nameof(SelectionCountText));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(HasSingleSelection));

        bool multi = HasMultipleSelection;
        bool single = HasSingleSelection;
        foreach (var cell in Vm.GridCells)
        {
            cell.HasMultipleSelection = multi;
            cell.HasSingleSelection = single;
        }
    }

    // ── Highlight Helpers ─────────────────────────────────────────────────────

    private async void HighlightCell(CellViewModel cell)
    {
        cell.IsHighlighted = true;
        await Task.Delay(800);
        cell.IsHighlighted = false;
    }

    private void SelectAndPanToCell(CellViewModel cell)
    {
        ClearSelection();
        cell.IsSelected = true;
        _selectedCells.Add(cell);
        UpdateSelectionState();

        double centerX = cell.CanvasX + cell.ColSpan * Constants.GridSize / 2.0;
        double centerY = cell.CanvasY + cell.RowSpan * Constants.GridSize / 2.0;
        PanToPosition(centerX, centerY);
    }

    // ── Placement Preview Helpers ─────────────────────────────────────────────

    private void ShowPlacementPreview(double x, double y, int colSpan, int rowSpan, IContentLayer owningLayer)
    {
        var previewBorder = this.FindControl<Border>("PlacementPreviewBorder");
        if (previewBorder == null)
            return;

        _isShowingPlacementPreview = true;
        _previewX = x;
        _previewY = y;
        _previewColSpan = colSpan;
        _previewRowSpan = rowSpan;

        _previewIsValid = GridLayoutService.IsSpaceEmpty(Vm.GridCells, x, y, colSpan, rowSpan, owningLayer);

        previewBorder.BorderBrush = _previewIsValid ? Brushes.LightGreen : Brushes.Red;
        previewBorder.Background = _previewIsValid
            ? new SolidColorBrush(Color.FromArgb(48, 144, 238, 144))
            : new SolidColorBrush(Color.FromArgb(48, 255, 68, 68));

        Canvas.SetLeft(previewBorder, x);
        Canvas.SetTop(previewBorder, y);
        previewBorder.Width = colSpan * Constants.GridSize;
        previewBorder.Height = rowSpan * Constants.GridSize;
        previewBorder.IsVisible = true;
    }

    private void HidePlacementPreview()
    {
        var previewBorder = this.FindControl<Border>("PlacementPreviewBorder");
        if (previewBorder == null)
            return;

        _isShowingPlacementPreview = false;
        previewBorder.IsVisible = false;
        _pendingBackdrop = null;
    }

    private void UpdatePlacementPreview(Point canvasPoint)
    {
        if (!_isShowingPlacementPreview || _pendingBackdrop == null)
            return;

        int gridX = (int)(Math.Floor(canvasPoint.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(canvasPoint.Y / Constants.GridSize) * Constants.GridSize);

        ShowPlacementPreview(gridX, gridY, _previewColSpan, _previewRowSpan, Vm.LayerManager.Backdrops);
    }

    private bool TryPlacePendingBackdrop()
    {
        if (!_isShowingPlacementPreview || _pendingBackdrop == null || !_previewIsValid)
            return false;

        _pendingBackdrop.CanvasX = _previewX;
        _pendingBackdrop.CanvasY = _previewY;
        Vm.GridCells.Add(_pendingBackdrop);
        Vm.MarkUnsaved();
        Vm.SaveBoardData();
        HidePlacementPreview();
        return true;
    }

    // ── Edge Scroll Helpers ───────────────────────────────────────────────────

    private void StartEdgeScrollIfNeeded(Point screenPoint)
    {
        var canvasBorder = _cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder");
        if (canvasBorder == null)
            return;

        var bounds = canvasBorder.Bounds;
        bool nearEdge = screenPoint.X < EdgeScrollThreshold ||
                        screenPoint.Y < EdgeScrollThreshold ||
                        screenPoint.X > bounds.Width - EdgeScrollThreshold ||
                        screenPoint.Y > bounds.Height - EdgeScrollThreshold;

        if (nearEdge && !_isEdgeScrolling)
        {
            _isEdgeScrolling = true;
            if (_edgeScrollTimer == null)
            {
                _edgeScrollTimer = new System.Timers.Timer(16);
                _edgeScrollTimer.Elapsed += EdgeScrollTimer_Elapsed;
            }
            _edgeScrollTimer.Start();
        }
        else if (!nearEdge && _isEdgeScrolling)
        {
            StopEdgeScroll();
        }
    }

    private void StopEdgeScroll()
    {
        _isEdgeScrolling = false;
        _edgeScrollTimer?.Stop();
    }

    private void EdgeScrollTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var canvasBorder = _cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder");
            if (!_isEdgeScrolling || canvasBorder == null)
                return;

            var bounds = canvasBorder.Bounds;
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
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    // ── Toast Notification ────────────────────────────────────────────────────

    private async void ShowToast(string message)
    {
        var border = this.FindControl<Border>("ToastBorder");
        var text = this.FindControl<TextBlock>("ToastText");
        if (border == null || text == null)
            return;

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
        catch (TaskCanceledException) { }
    }

    // ── Viewport LOD Management ───────────────────────────────────────────────

    private void InitViewportLodTimer()
    {
        _viewportLodTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _viewportLodTimer.Tick += ViewportLodTimer_Tick;
        _viewportLodTimer.Start();

        _lodDebounceTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _lodDebounceTimer.Tick += LodDebounceTimer_Tick;
    }

    private void LodDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _lodDebounceTimer?.Stop();
        _isLodUpdateScheduled = false;

        if (_lodUpdatePending)
        {
            _lodUpdatePending = false;
            _ = UpdateViewportLodAsync();
        }
    }

    private void ViewportLodTimer_Tick(object? sender, EventArgs e)
    {
        double tx = _translate.X;
        double ty = _translate.Y;
        double sc = _scale.ScaleX;
        int count = Vm.GridCells.Count;
        double vw = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double vh = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;
        int annCount = Vm.Annotations.Count;

        if (tx == _lastViewportTx && ty == _lastViewportTy
            && sc == _lastViewportScale && count == _lastViewportCellCount
            && vw == _lastViewportW && vh == _lastViewportH
            && annCount == _lastAnnotationCount)
            return;

        _lastViewportTx = tx;
        _lastViewportTy = ty;
        _lastViewportScale = sc;
        _lastViewportCellCount = count;
        _lastViewportW = vw;
        _lastViewportH = vh;
        _lastAnnotationCount = annCount;

        _lodUpdatePending = true;

        if (!_isLodUpdateScheduled)
        {
            _isLodUpdateScheduled = true;
            _lodDebounceTimer?.Start();
        }
    }

    public void ScheduleViewportUpdate()
    {
        _lastViewportScale = double.NaN;
    }

    private async Task UpdateViewportLodAsync()
    {
        try
        {
            double scale = _scale.ScaleX;
            double tx = _translate.X;
            double ty = _translate.Y;

            double viewW = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
            double viewH = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;
            if (viewW <= 0 || viewH <= 0)
                return;

            double vpLeft = -tx;
            double vpTop = -ty;
            double vpRight = viewW / scale - tx;
            double vpBottom = viewH / scale - ty;

            double margin = Constants.GridSize * 2;
            vpLeft -= margin;
            vpTop -= margin;
            vpRight += margin;
            vpBottom += margin;

            double vpCenterX = (vpLeft + vpRight) / 2.0;
            double vpCenterY = (vpTop + vpBottom) / 2.0;

            var draggedCells = new System.Collections.Generic.HashSet<CellViewModel>();
            if (_draggingCell != null)
                draggedCells.Add(_draggingCell);
            if (_groupDragStarts != null)
                foreach (var g in _groupDragStarts)
                    draggedCells.Add(g.Cell);

            var unloads = new System.Collections.Generic.List<CellViewModel>();
            var loads = new System.Collections.Generic.List<(CellViewModel Cell, ImageLod Target, double Distance)>();

            foreach (var cell in Vm.GridCells)
            {
                double cellLeft = cell.CanvasX;
                double cellTop = cell.CanvasY;
                double cellRight = cellLeft + cell.PixelWidth;
                double cellBottom = cellTop + cell.PixelHeight;

                bool isInViewport = cellRight > vpLeft && cellLeft < vpRight
                                 && cellBottom > vpTop && cellTop < vpBottom;

                if (draggedCells.Contains(cell))
                    isInViewport = true;

                double cellScreenWidth = cell.PixelWidth * scale;
                bool showDetail = isInViewport && cellScreenWidth >= 50.0;

                if (cell.IsInViewport == isInViewport && cell.IsDetailVisible == showDetail)
                {
                    if (!cell.NeedsImage)
                        continue;
                    var currentTargetLod = ImageManager.DetermineLod(cellScreenWidth, isInViewport);
                    if (currentTargetLod == cell.CurrentLod)
                        continue;
                }

                cell.IsInViewport = isInViewport;
                cell.IsDetailVisible = showDetail;

                if (!cell.NeedsImage)
                    continue;

                var targetLod = ImageManager.DetermineLod(cellScreenWidth, isInViewport);

                if (targetLod == cell.CurrentLod)
                    continue;

                if (targetLod == ImageLod.Placeholder)
                {
                    unloads.Add(cell);
                }
                else
                {
                    double cx = cell.CanvasX + cell.PixelWidth / 2.0;
                    double cy = cell.CanvasY + cell.PixelHeight / 2.0;
                    double dist = Math.Abs(cx - vpCenterX) + Math.Abs(cy - vpCenterY);
                    loads.Add((cell, targetLod, dist));
                }
            }

            foreach (var cell in unloads)
                cell.UnloadImage();

            if (loads.Count > 0)
            {
                loads.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));

                var sem = new System.Threading.SemaphoreSlim(4, 4);

                async Task LoadThrottled(CellViewModel cell, ImageLod lod)
                {
                    await sem.WaitAsync();
                    try { await cell.ApplyLodAsync(lod); }
                    finally { sem.Release(); }
                }

                var tasks = new System.Collections.Generic.List<Task>(loads.Count);
                foreach (var (cell, lod, _) in loads)
                    tasks.Add(LoadThrottled(cell, lod));

                await Task.WhenAll(tasks);
            }

            if (unloads.Count > 0 || loads.Count > 0)
                GC.Collect(2, GCCollectionMode.Optimized, false);

            double annMargin = Constants.GridSize * 3;
            double annVpLeft = vpLeft - annMargin;
            double annVpTop = vpTop - annMargin;
            double annVpRight = vpRight + annMargin;
            double annVpBottom = vpBottom + annMargin;

            foreach (var ann in Vm.Annotations)
            {
                if (ann.Points.Count == 0)
                {
                    ann.IsInViewport = true;
                    continue;
                }

                bool inVp = Helpers.AnnotationBoundsHelper.IntersectsRenderedBounds(
                    ann,
                    new Rect(annVpLeft, annVpTop, annVpRight - annVpLeft, annVpBottom - annVpTop));

                if (ann.IsInViewport == inVp)
                    continue;

                ann.IsInViewport = inVp;
            }
        }
        finally { }
    }
}
