using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

public partial class MainWindow
{
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

        // Handle placement preview click (backdrop positioning)
        if (_isShowingPlacementPreview && props.IsLeftButtonPressed)
        {
            if (TryPlacePendingBackdrop())
            {
                e.Handled = true;
                return;
            }
            else
            {
                // Invalid position - show shake feedback
                ShakeScreen();
                e.Handled = true;
                return;
            }
        }

        // Right-click or Escape cancels placement preview
        if (_isShowingPlacementPreview && (props.IsRightButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            HidePlacementPreview();
            e.Handled = true;
            return;
        }

        // Annotation mode: Eraser
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
            }

            Annotations.Add(_currentAnnotation);
            e.Pointer.Capture(sender as IInputElement);
            return;
        }

        // Grid mode: Middle button starts pan
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
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var mainCanvas = this.FindControl<Canvas>("MainCanvas");
        var pt = e.GetPosition(mainCanvas);

        // Store pointer position for edge scrolling
        _lastPointerPosition = e.GetPosition(CanvasBorder);

        // Update custom cursor icon position
        var cursorIcon = this.FindControl<Border>("CursorIconContainer");
        if (cursorIcon != null)
        {
            Canvas.SetLeft(cursorIcon, pt.X + 15);
            Canvas.SetTop(cursorIcon, pt.Y + 15);
        }

        // Update placement preview for backdrop positioning
        if (_isShowingPlacementPreview)
        {
            UpdatePlacementPreview(pt);
            StartEdgeScrollIfNeeded(_lastPointerPosition);
        }

        // Eraser drag
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
            StartEdgeScrollIfNeeded(_lastPointerPosition);

            if (IsDrawMode)
            {
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
                        collision = GridLayoutService.HasGroupCollision(GridCells, cellsToMove, dx, dy);
                        foreach (var (c, _, _) in _annotationDragCellOriginals)
                        {
                            c.IsDragInvalid = collision;
                            c.CanvasX += dx;
                            c.CanvasY += dy;
                        }
                    }

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
            StartEdgeScrollIfNeeded(_lastPointerPosition);

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
            return;
        }

        // Hover highlight for grid cells
        var gridPt = e.GetPosition(CanvasGrid);
        int gridX = (int)(Math.Floor(gridPt.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(gridPt.Y / Constants.GridSize) * Constants.GridSize);

        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight != null)
        {
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

        // Nuke-style drag-to-zoom
        var currentProps = e.GetCurrentPoint(this).Properties;
        bool bothButtons = currentProps.IsMiddleButtonPressed && currentProps.IsLeftButtonPressed;

        if (bothButtons)
        {
            if (!_middleZoomAnchorSet)
            {
                _middleZoomAnchor = sender is Visual v ? e.GetPosition(v) : e.GetPosition(this);
                _middleZoomOriginY = e.GetPosition(this).Y;
                _middleZoomStartY = _middleZoomOriginY;
                _middleZoomActive = false;
                _middleZoomAnchorSet = true;
            }

            var screenPt = e.GetPosition(this);

            if (!_middleZoomActive)
            {
                if (Math.Abs(screenPt.Y - _middleZoomOriginY) < Constants.MiddleZoomDeadZone)
                {
                    _panStartPoint = screenPt;
                    return;
                }
                _middleZoomActive = true;
                _middleZoomStartY = screenPt.Y;
            }

            double deltaY = _middleZoomStartY - screenPt.Y;

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
        // Stop edge scrolling when mouse is released
        StopEdgeScroll();

        if (IsEraserMode)
            e.Pointer.Capture(null);

        // Finish annotation marquee selection
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

            if (!IsDrawMode && _annotationDragCellOriginals != null && _annotationDragCellOriginals.Count > 0)
            {
                bool hasCollision = false;

                foreach (var (c, startX, startY) in _annotationDragCellOriginals)
                {
                    if (GridLayoutService.HasLayerCollision(GridCells, c.CollisionLayer, c, c.CanvasX, c.CanvasY, c.ColSpan, c.RowSpan))
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

                    foreach (var ann in _selectedAnnotations)
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

                if (cellMarquee.Width > 4 || cellMarquee.Height > 4)
                {
                    _selectedCells.Clear();
                    foreach (var cell in GridCells)
                    {
                        cell.IsSelected = false;
                        if (!cell.HasContent)
                            continue;

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

                    // Also select annotations in grid mode
                    _selectedAnnotations.Clear();
                    foreach (var ann in Annotations)
                    {
                        ann.IsSelected = false;

                        if (ann.Points.Count == 0)
                            continue;

                        double px = ann.Points[0].X + ann.CanvasX;
                        double py = ann.Points[0].Y + ann.CanvasY;

                        if (px >= left && px <= right && py >= top && py <= bottom)
                        {
                            ann.IsSelected = true;
                            _selectedAnnotations.Add(ann);
                        }
                    }
                }
            }
            UpdateSelectionState();
            return;
        }

        _isPanning = false;
        _middleZoomAnchorSet = false;
        _middleZoomActive = false;
        UpdateSelectionState();
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

    #region View Navigation

    private void ShowAll_Click(object? sender, RoutedEventArgs e)
    {
        if (GridCells.Count == 0)
        {
            _translate.X = 0;
            _translate.Y = 0;
            _scale.ScaleX = 1;
            _scale.ScaleY = 1;
            OnPropertyChanged(nameof(ZoomLevelText));
            return;
        }

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
        _translate.X = viewportWidth / 2 / scale - (minX + maxX) / 2;
        _translate.Y = viewportHeight / 2 / scale - (minY + maxY) / 2;
        OnPropertyChanged(nameof(ZoomLevelText));
    }

    private void ShowSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCells.Count == 0 && _selectedAnnotations.Count == 0)
        { ShowAll_Click(sender, e); return; }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var cell in _selectedCells)
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

        foreach (var ann in _selectedAnnotations)
        {
            if (ann != null)
            {
                foreach (var pt in ann.Points)
                {
                    if (pt.X < minX)
                        minX = pt.X;
                    if (pt.Y < minY)
                        minY = pt.Y;
                    if (pt.X > maxX)
                        maxX = pt.X;
                    if (pt.Y > maxY)
                        maxY = pt.Y;
                }
            }
        }

        double contentWidth = Math.Max(0, maxX - minX);
        double contentHeight = Math.Max(0, maxY - minY);
        double viewportWidth = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double viewportHeight = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        const double padding = 100;
        double scaleX = contentWidth > 0 ? viewportWidth / (contentWidth + padding) : 2.0;
        double scaleY = contentHeight > 0 ? viewportHeight / (contentHeight + padding) : 2.0;
        double scale = Math.Clamp(Math.Min(scaleX, scaleY), Constants.MinZoom, 2.0);

        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        _translate.X = viewportWidth / 2 / scale - (minX + maxX) / 2;
        _translate.Y = viewportHeight / 2 / scale - (minY + maxY) / 2;
        OnPropertyChanged(nameof(ZoomLevelText));
    }

    /// <summary>
    /// Pans the view to center on a specific canvas position without changing zoom.
    /// </summary>
    private void PanToPosition(double canvasX, double canvasY)
    {
        double viewportWidth = MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width;
        double viewportHeight = MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height;

        _translate.X = viewportWidth / 2 / _scale.ScaleX - canvasX;
        _translate.Y = viewportHeight / 2 / _scale.ScaleY - canvasY;
    }

    #endregion

    #region Visual Feedback

    private async void ShakeScreen()
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
