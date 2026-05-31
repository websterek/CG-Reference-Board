using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable VSTHRD100 // XAML event handlers must be async void; see Tasks C2-C3

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
    // These delegate to _viewport (ViewportService) — single source of truth.

    /// <summary>Current zoom level as percentage string for the status bar.</summary>
    public string ZoomLevelText => $"{_viewport.Zoom * 100:F0}%";

    /// <summary>Inverse of current zoom scale for zoom-independent UI elements.</summary>
    public double ZoomInverseFactor => 1.0 / _viewport.Zoom;

    /// <summary>Border thickness that remains constant regardless of zoom level.</summary>
    public Thickness ZoomIndependentBorderThickness => new Thickness(2.0 / _viewport.Zoom);

    /// <summary>Corner radius that remains constant regardless of zoom level.</summary>
    public CornerRadius ZoomIndependentCornerRadius => new CornerRadius(0);

    /// <summary>Hide the dot/grid background below 25% zoom — VisualBrush tile count explodes at low scale.</summary>
    public bool IsCanvasBackgroundVisible => _viewport.Zoom >= 0.25;

    /// <summary>Delegates to <see cref="MainWindowViewModel.IsViewMode"/> for bindings inside DataTemplates that use RelativeSource AncestorType=views:MainWindow.</summary>
    public bool IsViewMode => Vm.IsViewMode;

    /// <summary>Number of currently selected items for the status bar.</summary>
    public string SelectionCountText
    {
        get
        {
            int count = Vm.SelectionService.SelectionCount;
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

    public bool HasMultipleSelection => Vm.SelectionService.HasMultipleSelection;
    public bool HasSingleSelection => Vm.SelectionService.HasSingleSelection;

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
    private bool _isDraggingAnnotations;
    private bool _isDraggingFromSystem;
    private Point _annotationSelectionStart;
    private Point _annotationDragStart;
    private List<(CellViewModel Cell, double StartX, double StartY)>? _annotationDragCellOriginals;
    private AnnotationViewModel? _editingTextAnnotation;
    private string? _editingTextAnnotationOriginalText;

    // Cell interaction
    private CellViewModel? _hoveredCell;
    private CellViewModel? _editingTextCell;
    private CellViewModel? _draggingCell;

    // Pan/Zoom — owned by ViewportService; _scale/_translate are the render objects kept in sync
    private IViewportService _viewport = new ViewportService();
    private readonly TranslateTransform _translate = new(0, 0);
    private readonly ScaleTransform _scale = new(1, 1);

    // Zoom toggle state (PureRef-style)
    private double _savedTranslateX;
    private double _savedTranslateY;
    private double _savedScale;
    private CellViewModel? _zoomedToCell;
    private bool _canRestoreView;

    // Multi-selection
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
    private ToastNotification? _toast;

    // Interaction controller (wired in constructor; does not yet route events)
    private CGReferenceBoard.Interaction.IInteractionController? _interactionController;

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

    // High-performance background pattern control (replaces VisualBrush tiling)
    private BackgroundPatternControl? _backgroundPattern;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Parameterless constructor required by Avalonia designer.</summary>
    public MainWindow()
        : this(MainWindowViewModel.CreateWithDI(false))
    { }

    public MainWindow(bool isViewMode, string? startFile)
        : this(MainWindowViewModel.CreateWithDI(isViewMode))
    {
        if (!string.IsNullOrEmpty(startFile) && File.Exists(startFile))
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = Vm.LoadBoardFromFileAsync(startFile));
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        ResetTransientPointerState(cancelActiveTransform: true);
        UpdateSelectionState();
    }

    private void CanvasBorder_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _interactionController?.OnPointerCaptureLost(e);
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
        _toast = this.FindControl<ToastNotification>("Toast");
        _backgroundPattern = this.FindControl<BackgroundPatternControl>("BackgroundPattern");

        // Wire fullscreen overlay events
        FullMediaOverlay.TextChanged += (_, _) =>
        {
            if (_editingTextCell != null)
            {
                _editingTextCell.TextContent = FullMediaOverlay.TextContent;
                Vm.MarkUnsaved();
            }
        };
        FullMediaOverlay.Closed += (_, _) => _editingTextCell = null;

        // Wire ViewModel events to View callbacks
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Vm.IsAlwaysOnTop))
                Topmost = Vm.IsAlwaysOnTop;
            if (e.PropertyName == nameof(Vm.TransformContextVersion))
                CancelActiveInteractionForContextChange();
            if (e.PropertyName == nameof(Vm.GridBackgroundMode))
                SyncBackgroundPatternType();
        };
        // Wire ViewportService refresh → LOD invalidation + sync render transforms
        if (App.Services?.GetService<IViewportService>() is { } vs)
        {
            _viewport = vs;
            vs.RefreshRequested += ScheduleViewportUpdate;
            vs.PropertyChanged += OnViewportPropertyChanged;
        }
        Vm.TransformService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Vm.TransformService.IsVisible) or nameof(Vm.TransformService.Bounds))
            {
                UpdateTransformOverlayLayout();
            }
        };

        // Wire services into View
        if (App.Services?.GetService<INotificationService>() is NotificationService ns)
            ns.ToastNotified += ToastEventHandler;
        if (App.Services?.GetService<IDialogService>() is DialogService ds)
            ds.SetOwnerProvider(() => this);
        if (App.Services?.GetService<IClipboardService>() is ClipboardService cs)
            cs.SetTopLevelProvider(() => TopLevel.GetTopLevel(this));

        // Wire async event handlers that cannot use XAML wiring
        KeyDown += Window_KeyDown;

        CanvasBorder.SizeChanged += (_, _) => SyncBackgroundPatternViewport();

        CacheCanvasControls();
        UpdateTransformOverlayLayout();

        // Initial background sync after layout
        SyncBackgroundPatternType();
        SyncBackgroundPatternViewport();

        _ = Vm.LoadRecentBoardsAsync();
        _ = Vm.LoadUserSettingsAsync();
        // RecentBoards is now bound via ItemsSource="{Binding RecentBoards}" in XAML

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

        // Wire up interaction controller (preparatory — old handlers still fire)
        var viewportSvc = App.Services?.GetService<IViewportService>()
            ?? new CGReferenceBoard.Services.ViewportService();
        var interactionCtx = new CGReferenceBoard.Interaction.MainWindowInteractionContext(this, viewportSvc);
        _interactionController = new CGReferenceBoard.Interaction.InteractionController(
            interactionCtx, new CGReferenceBoard.Interaction.States.IdleState());

        Closing += WindowClosingHandler;
    }

    private async void ToastEventHandler(string message)
    {
        try { await ShowToastAsync(message); }
        catch (Exception ex) { Debug.WriteLine($"ToastEventHandler: {ex.Message}"); }
    }

    private async void WindowClosingHandler(object? sender, WindowClosingEventArgs e)
    {
        try { await OnWindowClosing(sender, e); }
        catch (Exception ex) { Debug.WriteLine($"WindowClosingHandler: {ex.Message}"); }
    }

    private async Task OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (Vm.ClosingConfirmed || !Vm.HasUnsavedChanges || e.IsProgrammatic)
        {
            if (Vm.HasUnsavedChanges && !e.IsProgrammatic)
            {
                e.Cancel = true;
                try { await Vm.FlushPendingSavesAsync(); } catch { }
                Vm.ClosingConfirmed = true;
                Close();
            }
            return;
        }

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

    /// <summary>
    /// Keeps the render transforms (_scale, _translate) in sync whenever the
    /// ViewportService state changes (from any source: user gesture, service call, etc.).
    /// </summary>
    private void OnViewportPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IViewportService.Zoom))
        {
            _scale.ScaleX = _viewport.Zoom;
            _scale.ScaleY = _viewport.Zoom;
            SyncBackgroundPatternViewport();
            NotifyZoomChanged();
        }
        else if (e.PropertyName is nameof(IViewportService.OffsetX) or nameof(IViewportService.OffsetY))
        {
            _translate.X = _viewport.OffsetX;
            _translate.Y = _viewport.OffsetY;
            SyncBackgroundPatternViewport();
        }
    }

    /// <summary>
    /// Updates the BackgroundPatternControl's visible viewport bounds so it only
    /// renders dots/grid lines within the actual visible area — one DrawGeometry call
    /// instead of VisualBrush instantiating thousands of mini-control-trees.
    /// </summary>
    private void SyncBackgroundPatternViewport()
    {
        if (_backgroundPattern is null) return;
        try
        {
            double zoom = _viewport.Zoom;
            double screenW = CanvasBorder.Bounds.Width;
            double screenH = CanvasBorder.Bounds.Height;
            if (screenW <= 0 || screenH <= 0 || zoom <= 0) return;

            // Canvas-world viewport bounds.
            // Transform chain: screen = (world + offset) × zoom
            //   → world = screen / zoom - offset
            // At screen origin (0,0): world = -offset
            double worldLeft = -_viewport.OffsetX;
            double worldTop = -_viewport.OffsetY;
            double worldW = screenW / zoom;
            double worldH = screenH / zoom;

            // Convert to control-local: the control is at (-500000, -500000) on the canvas
            double controlX = Canvas.GetLeft(_backgroundPattern);
            double controlY = Canvas.GetTop(_backgroundPattern);
            double localX = double.IsNaN(controlX) ? 500000.0 : -controlX;
            double localY = double.IsNaN(controlY) ? 500000.0 : -controlY;
            _backgroundPattern.ViewportLeft = worldLeft + localX;
            _backgroundPattern.ViewportTop = worldTop + localY;
            _backgroundPattern.ViewportWidth = worldW;
            _backgroundPattern.ViewportHeight = worldH;
        }
        catch
        {
            // Ignore during layout transitions
        }
    }

    /// <summary>
    /// Syncs the background pattern type when the ViewModel's GridBackgroundMode changes.
    /// </summary>
    private void SyncBackgroundPatternType()
    {
        if (_backgroundPattern is null) return;
        _backgroundPattern.Pattern = Vm.GridBackgroundMode switch
        {
            "Grid" => BackgroundPatternControl.PatternType.Grid,
            "None" => BackgroundPatternControl.PatternType.None,
            _ => BackgroundPatternControl.PatternType.Dots
        };
    }

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

        // Push zoom-dependent values into the ViewModel for compiled DataTemplate bindings.
        Vm.ZoomIndependentBorderThickness = ZoomIndependentBorderThickness;
        Vm.ZoomIndependentCornerRadius = ZoomIndependentCornerRadius;
        Vm.IsCanvasBackgroundVisible = IsCanvasBackgroundVisible;
        Vm.ZoomLevelText = ZoomLevelText;

        CGReferenceBoard.Controls.AnnotationShape.SetScale(_viewport.Zoom);

        var canvas = this.FindControl<Avalonia.Controls.Canvas>("MainCanvas");
        if (canvas != null)
        {
            var mode = _viewport.Zoom < 0.35
                ? Avalonia.Media.Imaging.BitmapInterpolationMode.LowQuality
                : _viewport.Zoom < 1.0
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
            bounds.Width / 2 / _viewport.Zoom - _viewport.OffsetX,
            bounds.Height / 2 / _viewport.Zoom - _viewport.OffsetY);
        return GetOrCreateCellAt(centerPos);
    }

    // ── Image / Video Loading ─────────────────────────────────────────────────

    private async Task LoadImageToCellAsync(CellViewModel cell, string sourcePath)
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
        Vm.SelectionService.ClearSelection();
    }

    public void UpdateSelectionState()
    {
        UpdateTransformOverlayLayout();

        OnPropertyChanged(nameof(SelectionCountText));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        Vm.SelectionCountText = SelectionCountText;

        bool multi = HasMultipleSelection;
        bool single = HasSingleSelection;
        foreach (var cell in Vm.GridCells)
        {
            cell.HasMultipleSelection = multi;
            cell.HasSingleSelection = single;
        }
    }

    // ── Highlight Helpers ─────────────────────────────────────────────────────

    private async Task HighlightCellAsync(CellViewModel cell)
    {
        cell.IsHighlighted = true;
        await Task.Delay(800);
        cell.IsHighlighted = false;
    }

    private void SelectAndPanToCell(CellViewModel cell)
    {
        ClearSelection();
        Vm.SelectionService.SelectCell(cell);
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

    internal void HidePlacementPreview()
    {
        var previewBorder = this.FindControl<Border>("PlacementPreviewBorder");
        if (previewBorder == null)
            return;

        _isShowingPlacementPreview = false;
        previewBorder.IsVisible = false;
        _pendingBackdrop = null;
    }

    internal void UpdatePlacementPreview(Point canvasPoint)
    {
        if (!_isShowingPlacementPreview || _pendingBackdrop == null)
            return;

        int gridX = (int)(Math.Floor(canvasPoint.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(canvasPoint.Y / Constants.GridSize) * Constants.GridSize);

        ShowPlacementPreview(gridX, gridY, _previewColSpan, _previewRowSpan, Vm.LayerManager.Backdrops);
    }

    internal bool TryPlacePendingBackdrop()
    {
        if (!_isShowingPlacementPreview || _pendingBackdrop == null || !_previewIsValid)
            return false;

        _pendingBackdrop.CanvasX = _previewX;
        _pendingBackdrop.CanvasY = _previewY;
        Vm.GridCells.Add(_pendingBackdrop);
        Vm.MarkUnsaved();
        HidePlacementPreview();
        return true;
    }

    internal bool IsShowingPlacementPreview => _isShowingPlacementPreview;


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
                _viewport.OffsetX += dx;
                _viewport.OffsetY += dy;
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    // ── Toast Notification ────────────────────────────────────────────────────

    private async Task ShowToastAsync(string message)
    {
        if (_toast != null)
            await _toast.Show(message);
    }

    // ── Viewport LOD Management ───────────────────────────────────────────────

    private void InitViewportLodTimer()
    {
        _viewportLodTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
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
        double tx = _viewport.OffsetX;
        double ty = _viewport.OffsetY;
        double sc = _viewport.Zoom;
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
            double scale = _viewport.Zoom;
            double tx = _viewport.OffsetX;
            double ty = _viewport.OffsetY;

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
