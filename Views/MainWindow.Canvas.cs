#pragma warning disable VSTHRD100 // XAML event handlers must be async void; see Tasks C2-C3

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Layers.Infrastructure;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

public partial class MainWindow
{
    #region Canvas Pointer Handlers (Pan, Draw, Hover)
    // Indicates whether we've applied a pan cursor override on the CanvasBorder
    private bool _cursorApplied;
    // Helper saved cursor for restoring
    private Cursor? _savedCanvasCursor;

    // Cached controls for hot paths (PointerMoved, etc.)
    private Border? _cachedCanvasBorder;
    private Border? _cachedHoverHighlight;
    private Border? _cachedSelectionMarquee;
    private Border? _cachedCellSelectionMarquee;
    private Canvas? _cachedTransformOverlay;
    private Border? _cachedTransformBody;
    private Border? _cachedTransformTopLeft;
    private Border? _cachedTransformTop;
    private Border? _cachedTransformTopRight;
    private Border? _cachedTransformRight;
    private Border? _cachedTransformBottomRight;
    private Border? _cachedTransformBottom;
    private Border? _cachedTransformBottomLeft;
    private Border? _cachedTransformLeft;
    private Border? _cachedCursorIconContainer;
    private Ellipse? _cachedBrushCursorCircle;
    private Canvas? _cachedMainCanvas;

    private void CacheCanvasControls()
    {
        _cachedCanvasBorder = this.FindControl<Border>("CanvasBorder");
        _cachedHoverHighlight = this.FindControl<Border>("HoverHighlight");
        _cachedSelectionMarquee = this.FindControl<Border>("SelectionMarquee");
        _cachedCellSelectionMarquee = this.FindControl<Border>("CellSelectionMarquee");
        _cachedTransformOverlay = this.FindControl<Canvas>("TransformOverlay");
        _cachedTransformBody = this.FindControl<Border>("TransformBody");
        _cachedTransformTopLeft = this.FindControl<Border>("TransformTopLeft");
        _cachedTransformTop = this.FindControl<Border>("TransformTop");
        _cachedTransformTopRight = this.FindControl<Border>("TransformTopRight");
        _cachedTransformRight = this.FindControl<Border>("TransformRight");
        _cachedTransformBottomRight = this.FindControl<Border>("TransformBottomRight");
        _cachedTransformBottom = this.FindControl<Border>("TransformBottom");
        _cachedTransformBottomLeft = this.FindControl<Border>("TransformBottomLeft");
        _cachedTransformLeft = this.FindControl<Border>("TransformLeft");
        _cachedCursorIconContainer = this.FindControl<Border>("CursorIconContainer");
        _cachedBrushCursorCircle = this.FindControl<Ellipse>("BrushCursorCircle");
        _cachedMainCanvas = this.FindControl<Canvas>("MainCanvas");
    }

    private void UpdateTransformOverlayLayout()
    {
        var overlay = _cachedTransformOverlay ?? this.FindControl<Canvas>("TransformOverlay");
        var body = _cachedTransformBody ?? this.FindControl<Border>("TransformBody");
        if (overlay == null || body == null)
        {
            return;
        }

        var bounds = Vm.TransformService.Bounds;
        if (!Vm.TransformService.IsVisible || bounds.Width <= 0 || bounds.Height <= 0)
        {
            body.IsVisible = false;
            SetHandleVisibility(false);
            return;
        }

        var handleSize = 10.0 * ZoomInverseFactor;
        var halfHandle = handleSize / 2.0;
        var midX = bounds.X + (bounds.Width / 2.0);
        var midY = bounds.Y + (bounds.Height / 2.0);

        body.IsVisible = Vm.TransformService.Capabilities.CanMove;
        Canvas.SetLeft(body, bounds.X);
        Canvas.SetTop(body, bounds.Y);
        body.Width = bounds.Width;
        body.Height = bounds.Height;

        if (!Vm.TransformService.Capabilities.CanResize)
        {
            SetHandleVisibility(false);
            return;
        }

        SetHandle(_cachedTransformTopLeft, bounds.X - halfHandle, bounds.Y - halfHandle, handleSize);
        SetHandle(_cachedTransformTop, midX - halfHandle, bounds.Y - halfHandle, handleSize);
        SetHandle(_cachedTransformTopRight, bounds.Right - halfHandle, bounds.Y - halfHandle, handleSize);
        SetHandle(_cachedTransformRight, bounds.Right - halfHandle, midY - halfHandle, handleSize);
        SetHandle(_cachedTransformBottomRight, bounds.Right - halfHandle, bounds.Bottom - halfHandle, handleSize);
        SetHandle(_cachedTransformBottom, midX - halfHandle, bounds.Bottom - halfHandle, handleSize);
        SetHandle(_cachedTransformBottomLeft, bounds.X - halfHandle, bounds.Bottom - halfHandle, handleSize);
        SetHandle(_cachedTransformLeft, bounds.X - halfHandle, midY - halfHandle, handleSize);
    }

    private void SetHandleVisibility(bool isVisible)
    {
        foreach (var handle in new[]
                 {
                     _cachedTransformTopLeft,
                     _cachedTransformTop,
                     _cachedTransformTopRight,
                     _cachedTransformRight,
                     _cachedTransformBottomRight,
                     _cachedTransformBottom,
                     _cachedTransformBottomLeft,
                     _cachedTransformLeft
                 })
        {
            if (handle != null)
            {
                handle.IsVisible = isVisible;
            }
        }
    }

    private static void SetHandle(Border? handle, double left, double top, double size)
    {
        if (handle == null)
        {
            return;
        }

        handle.IsVisible = true;
        handle.Width = size;
        handle.Height = size;
        Canvas.SetLeft(handle, left);
        Canvas.SetTop(handle, top);
    }

    private void ApplyPanCursor(Border? canvasBorder)
    {
        if (canvasBorder == null)
            return;
        try
        {
            if (!_cursorApplied)
            {
                // Save the existing cursor if not already saved
                if (_savedCanvasCursor == null)
                    _savedCanvasCursor = canvasBorder.Cursor;
                canvasBorder.Cursor = new Cursor(StandardCursorType.Hand);
                _cursorApplied = true;
            }
        }
        catch
        {
            // Non-critical; ignore failures
        }
    }

    private void TransformBody_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var mainCanvas = _cachedMainCanvas ?? this.FindControl<Canvas>("MainCanvas");
        if (mainCanvas == null)
        {
            return;
        }

        if (TryStartTransformBodyMove(e.GetPosition(mainCanvas), e.GetCurrentPoint(this).Properties.IsLeftButtonPressed))
        {
            e.Pointer.Capture(_cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder"));
            e.Handled = true;
        }
    }

    internal bool TryStartTransformBodyMoveInternal(Point pointer) =>
        TryStartTransformBodyMove(pointer, isLeftButtonPressed: true);

    private bool TryStartTransformBodyMove(Point pointer, bool isLeftButtonPressed)
    {
        if (!isLeftButtonPressed || Vm.IsViewMode || !Vm.TransformService.Capabilities.CanMove)
        {
            return false;
        }

        var bounds = Vm.TransformService.Bounds;
        var handleHalfSize = 5.0 * ZoomInverseFactor;
        var bodyBounds = bounds.Deflate(handleHalfSize);
        if (bodyBounds.Width <= 0 || bodyBounds.Height <= 0 || !bodyBounds.Contains(pointer))
        {
            return false;
        }

        return StartTransformMoveFromCurrentSelection(pointer);
    }

    private void TransformHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm.IsViewMode || !Vm.TransformService.Capabilities.CanResize || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || sender is not Control { Tag: string tag })
        {
            return;
        }

        if (!Enum.TryParse<TransformHandle>(tag, out var handle) || handle == TransformHandle.None)
        {
            return;
        }

        var mainCanvas = _cachedMainCanvas ?? this.FindControl<Canvas>("MainCanvas");
        if (mainCanvas == null)
        {
            return;
        }

        Vm.TransformService.BeginResize(handle, e.GetPosition(mainCanvas), Vm.SelectionService);
        e.Pointer.Capture(_cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder"));
        e.Handled = true;
    }

    internal bool UpdateActiveTransform(Point pointer)
    {
        if (!Vm.TransformService.HasActiveOperation)
        {
            return false;
        }

        bool annotationMode = Vm.IsDrawMode;
        var transformService = Vm.TransformService;
        var delta = transformService.UpdatePreview(pointer, annotationMode);

        if (transformService.Operation == TransformOperation.Move)
        {
            if (annotationMode)
            {
                AnnotationTransformService.ApplyMove(transformService.ActiveSnapshots, delta);
            }
            else
            {
                GridTransformService.ApplyMove(transformService.ActiveSnapshots, delta);
            }
        }
        else if (transformService.Operation == TransformOperation.Resize)
        {
            if (annotationMode)
            {
                AnnotationTransformService.ApplyResize(transformService.ActiveSnapshots, transformService.StartBounds, transformService.Bounds);
            }
            else
            {
                GridTransformService.ApplyResize(transformService.ActiveSnapshots, transformService.StartBounds, transformService.Bounds);
            }
        }

        UpdateGridTransformState(annotationMode);
        UpdateTransformOverlayLayout();
        return true;
    }

    private bool FinishActiveTransform(PointerReleasedEventArgs e)
    {
        if (!Vm.TransformService.HasActiveOperation)
        {
            return false;
        }

        var transformService = Vm.TransformService;
        bool annotationMode = Vm.IsDrawMode;
        bool hasCollision = !annotationMode && GridTransformService.HasCollision(transformService.ActiveSnapshots, transformService.Operation, Vm.GridCells, Vm.LayerManager);

        if (hasCollision)
        {
            GridTransformService.RestoreSnapshots(transformService.ActiveSnapshots);
            ShakeScreen();
        }

        GridTransformService.ClearInvalidState(transformService.ActiveSnapshots);
        GridTransformService.SetDraggingState(transformService.ActiveSnapshots, isDragging: false);

        // Detect whether the transform actually moved/resized before End() resets StartBounds.
        bool actuallyMoved = !hasCollision && transformService.Bounds != transformService.StartBounds;

        transformService.End();
        transformService.ClearSnapshots();

        // Only invalidate the zoom-toggle state when the object actually changed position.
        // A plain click (pointer-down then up without dragging) also calls FinishActiveTransform
        // but must NOT clear the zoom-toggle state, otherwise double-click zoom can never restore.
        if (actuallyMoved)
        {
            InvalidateZoomRestore();
        }

        Vm.RefreshTransformState();
        UpdateTransformOverlayLayout();
        e.Pointer.Capture(null);

        if (!hasCollision)
        {
            Vm.MarkUnsaved();
        }

        return true;
    }

    /// <summary>
    /// Parameterless variant for use by the interaction state machine
    /// (pointer capture is managed externally by the controller).
    /// </summary>
    internal bool FinishActiveTransformFromState()
    {
        if (!Vm.TransformService.HasActiveOperation)
            return false;

        var transformService = Vm.TransformService;
        bool annotationMode = Vm.IsDrawMode;
        bool hasCollision = !annotationMode && GridTransformService.HasCollision(transformService.ActiveSnapshots, transformService.Operation, Vm.GridCells, Vm.LayerManager);

        if (hasCollision)
        {
            GridTransformService.RestoreSnapshots(transformService.ActiveSnapshots);
            ShakeScreen();
        }

        GridTransformService.ClearInvalidState(transformService.ActiveSnapshots);
        GridTransformService.SetDraggingState(transformService.ActiveSnapshots, isDragging: false);

        bool actuallyMoved = !hasCollision && transformService.Bounds != transformService.StartBounds;

        transformService.End();
        transformService.ClearSnapshots();

        if (actuallyMoved)
            InvalidateZoomRestore();

        Vm.RefreshTransformState();
        UpdateTransformOverlayLayout();

        if (!hasCollision)
            Vm.MarkUnsaved();

        return true;
    }

    private bool CancelActiveTransform()
    {
        var transformService = Vm.TransformService;
        if (!transformService.HasActiveOperation)
        {
            return false;
        }

        GridTransformService.RestoreSnapshots(transformService.ActiveSnapshots);
        GridTransformService.ClearInvalidState(transformService.ActiveSnapshots);
        GridTransformService.SetDraggingState(transformService.ActiveSnapshots, isDragging: false);
        transformService.Cancel();
        Vm.RefreshTransformState();
        UpdateTransformOverlayLayout();
        return true;
    }

    private bool HandleEscapeShortcut()
    {
        if (CancelActiveTransform())
        {
            UpdateSelectionState();
            return true;
        }

        return false;
    }

    internal bool StartTransformMoveFromCurrentSelection(Point pointer)
    {
        UpdateSelectionState();

        // Capture the pre-drag bounds (computed by Refresh from the directly-selected items)
        // so we can pass them as an override. This prevents the selection rect from jumping
        // to a larger union when CreateExpandedMoveSnapshots adds extra items such as
        // annotations that overlap the selected cell.
        var preDragBounds = Vm.TransformService.IsVisible ? Vm.TransformService.Bounds : (Rect?)null;

        IReadOnlyList<TransformItemSnapshot>? snapshots = null;
        if (!Vm.IsDrawMode)
        {
            snapshots = GridTransformService.CreateExpandedMoveSnapshots(
                Vm.SelectionService.SelectedCells,
                Vm.SelectionService.SelectedAnnotations,
                Vm.GridCells,
                Vm.Annotations);
        }

        Vm.TransformService.BeginMove(pointer, Vm.SelectionService, snapshots, preDragBounds);
        GridTransformService.SetDraggingState(Vm.TransformService.ActiveSnapshots, isDragging: Vm.TransformService.HasActiveOperation && !Vm.IsDrawMode);
        return Vm.TransformService.HasActiveOperation;
    }

    private void ResetTransientPointerState(bool cancelActiveTransform)
    {
        StopEdgeScroll();
        _isPanning = false;
        EnableCellHitTesting();

        if (cancelActiveTransform)
        {
            CancelActiveTransform();
        }

        try
        {
            RestorePanCursor(_cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder"));
        }
        catch
        {
            // Non-critical; ignore failures
        }
    }

    private void UpdateGridTransformState(bool annotationMode)
    {
        if (annotationMode)
        {
            return;
        }

        bool hasCollision = GridTransformService.HasCollision(Vm.TransformService.ActiveSnapshots, Vm.TransformService.Operation, Vm.GridCells, Vm.LayerManager);
        if (hasCollision)
        {
            GridTransformService.SetInvalidState(Vm.TransformService.ActiveSnapshots, isInvalid: true);
            return;
        }

        GridTransformService.ClearInvalidState(Vm.TransformService.ActiveSnapshots);
    }

    private void RestorePanCursor(Border? canvasBorder)
    {
        if (canvasBorder == null)
            return;
        try
        {
            if (_cursorApplied)
            {
                canvasBorder.Cursor = _savedCanvasCursor ?? Cursor.Default;
                _savedCanvasCursor = null;
                _cursorApplied = false;
            }
        }
        catch
        {
            // Non-critical; ignore failures
        }
    }

    // ─── Draw-mode helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new AnnotationViewModel for non-Text tools, adds the initial point,
    /// sets _currentAnnotation, and adds it to Vm.Annotations.
    /// Returns null for the Text tool (text editing is handled separately via overlay).
    /// </summary>
    internal AnnotationViewModel? BeginDrawAnnotationInternal(Point canvasPt)
    {
        if (Vm.CurrentTool == "Text")
            return null; // text path handled by legacy overlay code

        _currentAnnotation = new AnnotationViewModel
        {
            Type = Vm.CurrentTool,
            Color = Vm.CurrentBrushColor,
            Thickness = Vm.CurrentBrushThickness,
            IsInDrawMode = true
        };
        _currentAnnotation.Points.Add(canvasPt);
        Vm.Annotations.Add(_currentAnnotation);
        return _currentAnnotation;
    }

    internal void FinishDrawAnnotationInternal()
    {
        _currentAnnotation = null;
        Vm.MarkUnsaved();
    }

    private void MainCanvas_PointerEntered(object? sender, PointerEventArgs e)
    {
        Vm.IsPointerOverCanvas = true;
    }

    // ── Marquee selection wrappers (called from IInteractionContext) ───────────

    internal void BeginAnnotationMarquee(Point canvasPt, bool additive)
    {
        _selectionAdditive = additive;
        _annotationSelectionStart = canvasPt;

        var marquee = _cachedSelectionMarquee ?? this.FindControl<Border>("SelectionMarquee");
        if (marquee != null)
        {
            Canvas.SetLeft(marquee, canvasPt.X);
            Canvas.SetTop(marquee, canvasPt.Y);
            marquee.Width = 0;
            marquee.Height = 0;
            marquee.IsVisible = true;
        }

        if (!additive)
            Vm.SelectionService.ClearSelection();
    }

    internal void UpdateAnnotationMarqueeFromState(Point canvasPt)
    {
        var marquee = _cachedSelectionMarquee ?? this.FindControl<Border>("SelectionMarquee");
        if (marquee == null) return;
        double left = Math.Min(_annotationSelectionStart.X, canvasPt.X);
        double top  = Math.Min(_annotationSelectionStart.Y, canvasPt.Y);
        Canvas.SetLeft(marquee, left);
        Canvas.SetTop(marquee, top);
        marquee.Width  = Math.Abs(canvasPt.X - _annotationSelectionStart.X);
        marquee.Height = Math.Abs(canvasPt.Y - _annotationSelectionStart.Y);
    }

    internal void FinishAnnotationMarqueeFromState()
    {
        var marquee = _cachedSelectionMarquee ?? this.FindControl<Border>("SelectionMarquee");
        if (marquee != null)
        {
            marquee.IsVisible = false;
            double left   = Canvas.GetLeft(marquee);
            double top    = Canvas.GetTop(marquee);
            var selRect   = new Rect(left, top, marquee.Width, marquee.Height);

            if (!_selectionAdditive)
                Vm.SelectionService.ClearSelection();

            foreach (var ann in Vm.Annotations)
            {
                if (!ann.IsSelected && Helpers.AnnotationBoundsHelper.IntersectsRenderedGeometry(ann, selRect))
                    Vm.SelectionService.SelectAnnotation(ann, additive: true);
            }
        }
        UpdateSelectionState();
    }

    internal void BeginCellMarquee(Point canvasPt, bool additive)
    {
        _selectionAdditive = additive;
        if (!additive)
            ClearSelection();
        _cellSelectionStart = canvasPt;

        var cellMarquee = _cachedCellSelectionMarquee ?? this.FindControl<Border>("CellSelectionMarquee");
        if (cellMarquee != null)
        {
            Canvas.SetLeft(cellMarquee, canvasPt.X);
            Canvas.SetTop(cellMarquee, canvasPt.Y);
            cellMarquee.Width = 0;
            cellMarquee.Height = 0;
            cellMarquee.IsVisible = true;
        }
    }

    internal void UpdateCellMarqueeFromState(Point canvasPt)
    {
        var cellMarquee = _cachedCellSelectionMarquee ?? this.FindControl<Border>("CellSelectionMarquee");
        if (cellMarquee == null) return;
        double left = Math.Min(_cellSelectionStart.X, canvasPt.X);
        double top  = Math.Min(_cellSelectionStart.Y, canvasPt.Y);
        Canvas.SetLeft(cellMarquee, left);
        Canvas.SetTop(cellMarquee, top);
        cellMarquee.Width  = Math.Abs(canvasPt.X - _cellSelectionStart.X);
        cellMarquee.Height = Math.Abs(canvasPt.Y - _cellSelectionStart.Y);
    }

    internal void FinishCellMarqueeFromState()
    {
        var cellMarquee = _cachedCellSelectionMarquee ?? this.FindControl<Border>("CellSelectionMarquee");
        if (cellMarquee != null)
        {
            cellMarquee.IsVisible = false;
            double left   = Canvas.GetLeft(cellMarquee);
            double top    = Canvas.GetTop(cellMarquee);
            var selRect   = new Rect(left, top, cellMarquee.Width, cellMarquee.Height);

            var hits = HitTestCellsInRect(selRect);
            foreach (var cell in hits)
                Vm.SelectionService.SelectCell(cell, additive: true);
        }
        UpdateSelectionState();
    }

    // Helper used by FinishCellMarqueeFromState
    private IReadOnlyList<CellViewModel> HitTestCellsInRect(Rect canvasRect)
    {
        var result = new System.Collections.Generic.List<CellViewModel>();
        foreach (var cell in Vm.GridCells)
        {
            var bounds = new Rect(cell.CanvasX, cell.CanvasY, cell.ColSpan * Constants.GridSize, cell.RowSpan * Constants.GridSize);
            var intersection = bounds.Intersect(canvasRect);
            if (intersection.Width > 0 && intersection.Height > 0)
                result.Add(cell);
        }
        return result;
    }

    private void MainCanvas_PointerExited(object? sender, PointerEventArgs e)
    {
        Vm.IsPointerOverCanvas = false;
        var brushCircle = this.FindControl<Ellipse>("BrushCursorCircle");
        if (brushCircle != null)
            brushCircle.IsVisible = false;
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _interactionController?.OnPointerPressed(e);

        // Update custom cursor icon position (cosmetic, not handled by state machine)
        var cursorIcon = _cachedCursorIconContainer ?? this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            var ptScreen = e.GetPosition(_cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder"));
            Canvas.SetLeft(cursorIcon, ptScreen.X + 15);
            Canvas.SetTop(cursorIcon, ptScreen.Y + 15);
        }

        if (e.Handled)
            return;

        // Text annotation: the state machine returns Stay for Text tool to allow
        // this legacy overlay code to handle the text editor.
        var props = e.GetCurrentPoint(this).Properties;
        var mainCanvas = _cachedMainCanvas ?? this.FindControl<Canvas>("MainCanvas");
        if (Vm.IsDrawMode && !Vm.IsEraserMode && !Vm.IsMoveMode && props.IsLeftButtonPressed
            && Vm.CurrentTool == "Text" && mainCanvas != null)
        {
            _currentAnnotation = new AnnotationViewModel
            {
                Type = "Text",
                Color = Vm.CurrentBrushColor,
                Thickness = Vm.CurrentBrushThickness,
                IsInDrawMode = true,
                Text = ""
            };

            var pt = e.GetPosition(mainCanvas);
            _currentAnnotation.Points.Add(pt);
            _editingTextAnnotation = _currentAnnotation;
            _editingTextAnnotationOriginalText = null;

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

            Vm.Annotations.Add(_currentAnnotation);
            e.Pointer.Capture(sender as IInputElement);
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        _interactionController?.OnPointerMoved(e);

        var mainCanvas = this.FindControl<Canvas>("MainCanvas");
        var pt = e.GetPosition(mainCanvas);

        // If a transform operation is active and was started by a cell drag (not via
        // TransformBodyMoveState, which handles its own UpdateActiveTransform call),
        // update the transform here so cell dragging moves items.
        if (Vm.TransformService.HasActiveOperation
            && _interactionController?.CurrentState is not Interaction.States.TransformBodyMoveState)
            UpdateActiveTransform(pt);

        // Store pointer position for edge scrolling
        _lastPointerPosition = e.GetPosition(CanvasBorder);

        // Update custom cursor icon position
        var cursorIcon = _cachedCursorIconContainer ?? this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            Canvas.SetLeft(cursorIcon, _lastPointerPosition.X + 15);
            Canvas.SetTop(cursorIcon, _lastPointerPosition.Y + 15);
        }

        // Update brush size cursor circle (screen-space size = brush thickness × zoom)
        var brushCircle = _cachedBrushCursorCircle ?? this.FindControl<Ellipse>("BrushCursorCircle");
        if (brushCircle != null)
        {
            bool showCircle = Vm.IsDrawMode && (Vm.CurrentTool == "Brush" || Vm.CurrentTool == "Arrow" || Vm.CurrentTool == "Rectangle" || Vm.CurrentTool == "Ellipse");
            brushCircle.IsVisible = showCircle;
            if (showCircle)
            {
                double sizeInScreen = Vm.CurrentBrushThickness * _viewport.Zoom;
                brushCircle.Width = sizeInScreen;
                brushCircle.Height = sizeInScreen;
                Canvas.SetLeft(brushCircle, _lastPointerPosition.X - sizeInScreen / 2.0);
                Canvas.SetTop(brushCircle, _lastPointerPosition.Y - sizeInScreen / 2.0);
            }
        }

        // Edge scroll for placement preview (state machine handles the preview update itself)
        if (_isShowingPlacementPreview)
            StartEdgeScrollIfNeeded(_lastPointerPosition);

        // Annotation drag (not yet migrated to state machine)
        if (_isDraggingAnnotations && Vm.SelectionService.SelectedAnnotations.Count > 0)
        {
            StartEdgeScrollIfNeeded(_lastPointerPosition);

            if (Vm.IsDrawMode)
            {
                double dx = pt.X - _annotationDragStart.X;
                double dy = pt.Y - _annotationDragStart.Y;
                foreach (var ann in Vm.SelectionService.SelectedAnnotations)
                {
                    ann.CanvasX += dx;
                    ann.CanvasY += dy;
                }
                _annotationDragStart = pt;
            }
            else
            {
                double targetX = Math.Round(pt.X / Constants.GridSize) * Constants.GridSize;
                double targetY = Math.Round(pt.Y / Constants.GridSize) * Constants.GridSize;
                double startX = Math.Round(_annotationDragStart.X / Constants.GridSize) * Constants.GridSize;
                double startY = Math.Round(_annotationDragStart.Y / Constants.GridSize) * Constants.GridSize;

                double dx = targetX - startX;
                double dy = targetY - startY;

                if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
                {
                    bool collision = false;
                    if (_annotationDragCellOriginals != null && _annotationDragCellOriginals.Count > 0)
                    {
                        var cellsToMove = _annotationDragCellOriginals.Select(x => x.Cell).ToList();
                        collision = GridLayoutService.HasGroupCollision(Vm.GridCells, cellsToMove, Vm.LayerManager, dx, dy);
                        foreach (var (c, _, _) in _annotationDragCellOriginals)
                        {
                            c.IsDragInvalid = collision;
                            c.CanvasX += dx;
                            c.CanvasY += dy;
                        }
                    }

                    foreach (var ann in Vm.SelectionService.SelectedAnnotations)
                    {
                        ann.CanvasX += dx;
                        ann.CanvasY += dy;
                    }
                    _annotationDragStart = new Point(targetX, targetY);
                }
            }
            return;
        }

        // Hover highlight for grid cells
        var gridPt = e.GetPosition(MainCanvas);
        int gridX = (int)(Math.Floor(gridPt.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(gridPt.Y / Constants.GridSize) * Constants.GridSize);

        var hoverHighlight = _cachedHoverHighlight ?? this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight != null)
        {
            CellViewModel? existingContent = null;
            if (_cellSpatialIndex.TryGetValue((gridX, gridY), out var cell) && cell != null)
            {
                if (!cell.IsBoardElement && cell.HasContent
                    && cell.CanvasX <= gridPt.X && cell.CanvasX + cell.PixelWidth > gridPt.X
                    && cell.CanvasY <= gridPt.Y && cell.CanvasY + cell.PixelHeight > gridPt.Y)
                {
                    existingContent = cell;
                }
            }

            Canvas.SetLeft(hoverHighlight, gridX);
            Canvas.SetTop(hoverHighlight, gridY);
            hoverHighlight.Width = Constants.GridSize;
            hoverHighlight.Height = Constants.GridSize;
            hoverHighlight.IsVisible = !(_isPanning || _isDraggingCell || _isResizing
                                         || _isPointerDown || existingContent != null || Vm.IsDrawMode);
        }

        // Pan cursor (cosmetic)
        var currentProps = e.GetCurrentPoint(this).Properties;
        bool wantsPanCursor = currentProps.IsMiddleButtonPressed || (currentProps.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        var canvasBorder = this.FindControl<Border>("CanvasBorder");
        if (wantsPanCursor)
            ApplyPanCursor(canvasBorder);
        else
            RestorePanCursor(canvasBorder);
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _interactionController?.OnPointerReleased(e);

        // Finish a transform operation started by cell drag (not TransformBodyMoveState).
        if (Vm.TransformService.HasActiveOperation
            && _interactionController?.CurrentState is not Interaction.States.TransformBodyMoveState)
            FinishActiveTransform(e);

        // Stop edge scrolling when mouse is released
        StopEdgeScroll();

        if (Vm.IsEraserMode)
            e.Pointer.Capture(null);

        // Finish annotation drag (not yet migrated to state machine)
        if (_isDraggingAnnotations)
        {
            _isDraggingAnnotations = false;

            if (!Vm.IsDrawMode && _annotationDragCellOriginals != null && _annotationDragCellOriginals.Count > 0)
            {
                bool hasCollision = false;

                foreach (var (c, startX, startY) in _annotationDragCellOriginals)
                {
                    if (GridLayoutService.HasLayerCollision(Vm.GridCells, Vm.LayerManager.ResolveLayer(c)!, c, c.CanvasX, c.CanvasY, c.ColSpan, c.RowSpan))
                    {
                        hasCollision = true;
                        break;
                    }
                }

                if (hasCollision)
                {
                    double revertDx = _annotationDragCellOriginals[0].StartX - _annotationDragCellOriginals[0].Cell.CanvasX;
                    double revertDy = _annotationDragCellOriginals[0].StartY - _annotationDragCellOriginals[0].Cell.CanvasY;

                    foreach (var (c, startX, startY) in _annotationDragCellOriginals)
                    {
                        c.CanvasX = startX;
                        c.CanvasY = startY;
                    }

                    foreach (var ann in Vm.SelectionService.SelectedAnnotations)
                    {
                        ann.CanvasX += revertDx;
                        ann.CanvasY += revertDy;
                    }
                    ShakeScreen();
                }

                foreach (var (c, _, _) in _annotationDragCellOriginals)
                {
                    c.IsDragging = false;
                    c.IsDragInvalid = false;
                }
                _annotationDragCellOriginals = null;
            }

            e.Pointer.Capture(null);
            Vm.MarkUnsaved();
            return;
        }

        EnableCellHitTesting();

        // Restore previous cursor on the CanvasBorder when panning stops
        try
        {
            var canvasBorder = this.FindControl<Border>("CanvasBorder");
            RestorePanCursor(canvasBorder);
        }
        catch
        {
            // ignore cursor restore failures
        }

        UpdateSelectionState();
    }

    private void CanvasBorder_PointerExited(object? sender, PointerEventArgs e)
    {
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight != null)
            hoverHighlight.IsVisible = false;

        // Restore previous cursor when leaving canvas border (if we changed it for panning)
        try
        {
            var canvasBorder = sender as Border ?? this.FindControl<Border>("CanvasBorder");
            RestorePanCursor(canvasBorder);
        }
        catch
        {
            // ignore failures
        }
    }

    private void Canvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled)
            return;

        // Treat wheel deltas multiplicatively in log-space so zoom feels linear across scales.
        double oldScale = _viewport.Zoom;

        // The wheel delta is typically ±1 per step on most platforms, but can be larger.
        // sensitivity controls how aggressive each wheel step is; tune if needed.
        const double wheelSensitivity = 0.09; // smaller => slower zoom per wheel step
        double deltaSteps = e.Delta.Y;
        if (Math.Abs(deltaSteps) < 1e-9)
            return;

        // Compute a log-space delta and clamp to avoid huge jumps
        double deltaLog = Math.Clamp(deltaSteps * wheelSensitivity, -0.5, 0.5);
        double factor = Math.Exp(deltaLog);
        double newScale = Math.Clamp(oldScale * factor, Constants.MinZoom, Constants.MaxZoom);

        if (Math.Abs(newScale - oldScale) < 0.001)
            return;

        if (sender is Visual visual)
        {
            var pointerPos = e.GetPosition(visual);
            _viewport.ZoomAt(pointerPos, factor);
        }
        else
        {
            _viewport.Zoom = newScale;
        }

        InvalidateZoomRestore();
    }

    #endregion

    #region Viewport Navigation

    private void ShowAll_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm.GridCells.Count == 0 && Vm.Annotations.Count == 0)
        {
            _viewport.ResetView();
            InvalidateZoomRestore();
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var cell in Vm.GridCells)
        {
            var cellBounds = TransformBoundsCalculator.GetCellBounds(cell);
            if (cellBounds.X < minX)
                minX = cellBounds.X;
            if (cellBounds.Y < minY)
                minY = cellBounds.Y;
            if (cellBounds.Right > maxX)
                maxX = cellBounds.Right;
            if (cellBounds.Bottom > maxY)
                maxY = cellBounds.Bottom;
        }

        var annotationBounds = AnnotationBoundsHelper.GetRenderedBoundsUnion(Vm.Annotations);
        if (annotationBounds is { } allAnnotationBounds)
        {
            if (allAnnotationBounds.X < minX)
                minX = allAnnotationBounds.X;
            if (allAnnotationBounds.Y < minY)
                minY = allAnnotationBounds.Y;
            if (allAnnotationBounds.Right > maxX)
                maxX = allAnnotationBounds.Right;
            if (allAnnotationBounds.Bottom > maxY)
                maxY = allAnnotationBounds.Bottom;
        }

        double contentWidth = maxX - minX;
        double contentHeight = maxY - minY;
        double viewportWidth = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double viewportHeight = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        const double padding = 100;
        double scaleX = viewportWidth / (contentWidth + padding);
        double scaleY = viewportHeight / (contentHeight + padding);
        double scale = Math.Clamp(Math.Min(scaleX, scaleY), Constants.MinZoom, 2.0);

        _viewport.Zoom = scale;
        _viewport.OffsetX = viewportWidth / 2 / scale - (minX + maxX) / 2;
        _viewport.OffsetY = viewportHeight / 2 / scale - (minY + maxY) / 2;
        InvalidateZoomRestore();
        NotifyZoomChanged();
    }

    private void ShowSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm.SelectionService.SelectedCells.Count == 0 && Vm.SelectionService.SelectedAnnotations.Count == 0)
        { ShowAll_Click(sender, e); return; }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var cell in Vm.SelectionService.SelectedCells)
        {
            var cellBounds = TransformBoundsCalculator.GetCellBounds(cell);
            if (cellBounds.X < minX)
                minX = cellBounds.X;
            if (cellBounds.Y < minY)
                minY = cellBounds.Y;
            if (cellBounds.Right > maxX)
                maxX = cellBounds.Right;
            if (cellBounds.Bottom > maxY)
                maxY = cellBounds.Bottom;
        }

        var selectedAnnotationBounds = AnnotationBoundsHelper.GetRenderedBoundsUnion(Vm.SelectionService.SelectedAnnotations);
        if (selectedAnnotationBounds is { } annotationSelectionBounds)
        {
            if (annotationSelectionBounds.X < minX)
                minX = annotationSelectionBounds.X;
            if (annotationSelectionBounds.Y < minY)
                minY = annotationSelectionBounds.Y;
            if (annotationSelectionBounds.Right > maxX)
                maxX = annotationSelectionBounds.Right;
            if (annotationSelectionBounds.Bottom > maxY)
                maxY = annotationSelectionBounds.Bottom;
        }

        double contentWidth = Math.Max(0, maxX - minX);
        double contentHeight = Math.Max(0, maxY - minY);
        double viewportWidth = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double viewportHeight = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        const double padding = 100;
        double scaleX = contentWidth > 0 ? viewportWidth / (contentWidth + padding) : 2.0;
        double scaleY = contentHeight > 0 ? viewportHeight / (contentHeight + padding) : 2.0;
        double scale = Math.Clamp(Math.Min(scaleX, scaleY), Constants.MinZoom, 2.0);

        _viewport.Zoom = scale;
        _viewport.OffsetX = viewportWidth / 2 / scale - (minX + maxX) / 2;
        _viewport.OffsetY = viewportHeight / 2 / scale - (minY + maxY) / 2;
        InvalidateZoomRestore();
        NotifyZoomChanged();
    }

    /// <summary>
    /// Zooms to a specific cell, filling the screen completely (fit to longest edge, no padding).
    /// Centers the cell in the viewport.
    /// PureRef-style toggle: double-clicking the same cell again restores the previous view,
    /// but only if the view hasn't been manually modified since the zoom.
    /// </summary>
    private void ZoomToCell(CellViewModel cell)
    {
        // Check if we can restore the previous view (same cell, view not manually modified)
        if (_canRestoreView && _zoomedToCell == cell)
        {
            // Restore previous view state
            _viewport.OffsetX = _savedTranslateX;
            _viewport.OffsetY = _savedTranslateY;
            _viewport.Zoom = _savedScale;

            // Clear zoom toggle state
            _canRestoreView = false;
            _zoomedToCell = null;

            NotifyZoomChanged();
            return;
        }

        // Save current view state before zooming
        _savedTranslateX = _viewport.OffsetX;
        _savedTranslateY = _viewport.OffsetY;
        _savedScale = _viewport.Zoom;

        double contentWidth = cell.PixelWidth;
        double contentHeight = cell.PixelHeight;
        double viewportWidth = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double viewportHeight = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        // No padding - fill screen completely
        double scaleX = contentWidth > 0 ? viewportWidth / contentWidth : 2.0;
        double scaleY = contentHeight > 0 ? viewportHeight / contentHeight : 2.0;
        double scale = Math.Clamp(Math.Min(scaleX, scaleY), Constants.MinZoom, 2.0);

        _viewport.Zoom = scale;

        // Center the cell in the viewport (use VisualX/VisualY for correct backdrop positioning)
        double cellCenterX = cell.VisualX + contentWidth / 2;
        double cellCenterY = cell.VisualY + contentHeight / 2;
        _viewport.OffsetX = viewportWidth / 2 / scale - cellCenterX;
        _viewport.OffsetY = viewportHeight / 2 / scale - cellCenterY;

        // Mark that we can restore this view on next double-click
        _canRestoreView = true;
        _zoomedToCell = cell;

        NotifyZoomChanged();
    }

    private void ZoomReset_Click(object? sender, RoutedEventArgs e)
    {
        _viewport.Zoom = 1.0;
        InvalidateZoomRestore();
        ScheduleViewportUpdate();
    }

    /// <summary>
    /// Pans the view to center on a specific canvas position without changing zoom.
    /// </summary>
    private void PanToPosition(double canvasX, double canvasY)
    {
        double viewportWidth = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double viewportHeight = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        _viewport.OffsetX = viewportWidth / 2 / _viewport.Zoom - canvasX;
        _viewport.OffsetY = viewportHeight / 2 / _viewport.Zoom - canvasY;
    }

    /// <summary>
    /// Invalidates the zoom toggle state when the user manually pans or zooms.
    /// Called from all manual view manipulation operations (wheel zoom, drag pan, etc.)
    /// </summary>
    private void InvalidateZoomRestore()
    {
        _canRestoreView = false;
        _zoomedToCell = null;
    }

    #endregion

    #region Visual Feedback

    internal async void ShakeScreen()
    {
        var startPos = Position;
        for (int i = 0; i < 5; i++)
        {
            Position = new PixelPoint(startPos.X + 10, startPos.Y);
            await System.Threading.Tasks.Task.Delay(30);
            Position = new PixelPoint(startPos.X - 10, startPos.Y);
            await System.Threading.Tasks.Task.Delay(30);
        }
        Position = startPos;
    }

    #endregion
}
