using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

public partial class MainWindow
{
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
            var emptySpace = GridLayoutService.FindEmptySpace(
                GridCells,
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

        var isLeftButton = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
        var isRightButton = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;

        if (isRightButton && sender is Control { DataContext: CellViewModel { HasContent: true } })
        {
            if (!cell.IsSelected)
            {
                ClearSelection();
                cell.IsSelected = true;
                _selectedCells.Add(cell);
                UpdateSelectionState();
            }
            return;
        }

        if (isLeftButton
            && sender is Control { DataContext: CellViewModel { HasContent: true } })
        {
            if (cell.IsBackdrop)
            {
                bool isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);

                if (isCtrlPressed)
                {
                    cell.IsSelected = !cell.IsSelected;
                    if (cell.IsSelected)
                        _selectedCells.Add(cell);
                    else
                        _selectedCells.Remove(cell);
                    UpdateSelectionState();
                }
                else if (!cell.IsSelected)
                {
                    ClearSelection();
                    cell.IsSelected = true;
                    _selectedCells.Add(cell);
                    UpdateSelectionState();
                }

                _isPointerDown = true;
                _pointerDownPos = e.GetPosition(this);
                _lastPressedEventArgs = e;
                e.Handled = true;
                return;
            }

            if (!cell.IsBackdrop && !cell.IsSelected && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ClearSelection();
                cell.IsSelected = true;
                _selectedCells.Add(cell);
                UpdateSelectionState();
            }

            bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (isCtrl)
            {
                cell.IsSelected = !cell.IsSelected;
                if (cell.IsSelected)
                    _selectedCells.Add(cell);
                else
                    _selectedCells.Remove(cell);
                UpdateSelectionState();
            }
            else
            {
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

        _lastPointerPosition = e.GetPosition(CanvasBorder);

        if (!_isDraggingCell)
        {
            var pt = e.GetPosition(this);
            if (Math.Abs(pt.X - _pointerDownPos.X) > Constants.DragThreshold
                || Math.Abs(pt.Y - _pointerDownPos.Y) > Constants.DragThreshold)
            {
                _isDraggingCell = true;
                _draggingCell = cell;

                var cellsToMove = new List<CellViewModel>(_selectedCells);
                var annotationsToMove = new List<AnnotationViewModel>();

                foreach (var backdrop in _selectedCells.Where(c => c.IsBackdrop).ToList())
                {
                    double left = backdrop.CanvasX;
                    double top = backdrop.CanvasY;
                    double right = left + backdrop.ColSpan * Constants.GridSize;
                    double bottom = top + backdrop.RowSpan * Constants.GridSize;

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

                foreach (var ann in _selectedAnnotations)
                {
                    if (!annotationsToMove.Contains(ann))
                        annotationsToMove.Add(ann);
                }

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

                // Set dragging flag for Z-index boost
                if (_groupDragStarts != null)
                {
                    foreach (var (c, _, _) in _groupDragStarts)
                        c.IsDragging = true;
                }
                else
                {
                    cell.IsDragging = true;
                }

                var canvasPt = e.GetPosition(CanvasGrid);
                _dragOffsetX = canvasPt.X - cell.CanvasX;
                _dragOffsetY = canvasPt.Y - cell.CanvasY;
                e.Pointer.Capture(sender as Control);
            }
        }
        else if (_groupDragStarts != null && (_groupDragStarts.Count + (_groupAnnotationDragStarts?.Count ?? 0)) > 1)
        {
            var canvasPt = e.GetPosition(CanvasGrid);
            double targetX = Math.Round((canvasPt.X - _dragOffsetX) / Constants.GridSize) * Constants.GridSize;
            double targetY = Math.Round((canvasPt.Y - _dragOffsetY) / Constants.GridSize) * Constants.GridSize;
            double currentX = _draggingCell?.CanvasX ?? _dragStartX;
            double currentY = _draggingCell?.CanvasY ?? _dragStartY;
            double dx = targetX - currentX;
            double dy = targetY - currentY;

            if (Math.Abs(dx) > Constants.GridSize)
                dx = Math.Sign(dx) * Constants.GridSize;
            if (Math.Abs(dy) > Constants.GridSize)
                dy = Math.Sign(dy) * Constants.GridSize;

            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
            {
                var cellsToMove = _groupDragStarts.Select(s => s.Cell).ToList();
                bool collision = GridLayoutService.HasGroupCollision(GridCells, cellsToMove, dx, dy);

                // Update visual state and allow movement
                foreach (var (c, _, _) in _groupDragStarts)
                {
                    c.IsDragInvalid = collision;
                    c.CanvasX += dx;
                    c.CanvasY += dy;
                }
                if (_groupAnnotationDragStarts != null)
                {
                    foreach (var (a, _, _) in _groupAnnotationDragStarts)
                    {
                        a.CanvasX += dx;
                        a.CanvasY += dy;
                    }
                }
            }

            StartEdgeScrollIfNeeded(_lastPointerPosition);
        }
        else
        {
            StartEdgeScrollIfNeeded(_lastPointerPosition);

            var canvasPt = e.GetPosition(CanvasGrid);
            double newX = Math.Round((canvasPt.X - _dragOffsetX) / Constants.GridSize) * Constants.GridSize;
            double newY = Math.Round((canvasPt.Y - _dragOffsetY) / Constants.GridSize) * Constants.GridSize;

            bool collision = GridLayoutService.HasLayerCollision(GridCells, cell.CollisionLayer, cell, newX, newY, cell.ColSpan, cell.RowSpan);
            cell.IsDragInvalid = collision;
            cell.CanvasX = newX;
            cell.CanvasY = newY;
        }
    }

    private void Cell_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        StopEdgeScroll();

        if (IsDrawMode)
            return;

        if (_isDraggingCell && sender is Control)
        {
            // Handle group drag
            if (_groupDragStarts != null)
            {
                var cellsToMove = _groupDragStarts.Select(s => s.Cell).ToList();
                bool hasCollision = false;

                foreach (var (c, startX, startY) in _groupDragStarts)
                {
                    if (GridLayoutService.HasLayerCollision(GridCells, c.CollisionLayer, c, c.CanvasX, c.CanvasY, c.ColSpan, c.RowSpan))
                    {
                        hasCollision = true;
                        break;
                    }
                }

                if (hasCollision)
                {
                    // Revert all cells to start positions
                    foreach (var (c, startX, startY) in _groupDragStarts)
                    {
                        c.CanvasX = startX;
                        c.CanvasY = startY;
                    }
                    if (_groupAnnotationDragStarts != null)
                    {
                        foreach (var (a, startX, startY) in _groupAnnotationDragStarts)
                        {
                            a.CanvasX = startX;
                            a.CanvasY = startY;
                        }
                    }
                }

                // Clear IsDragInvalid flag
                // Clear invalid state and dragging flag for all cells
                foreach (var (c, _, _) in _groupDragStarts)
                {
                    c.IsDragInvalid = false;
                    c.IsDragging = false;
                }
            }
            // Handle single cell drag
            else if (_draggingCell != null)
            {
                if (GridLayoutService.HasLayerCollision(GridCells, _draggingCell.CollisionLayer, _draggingCell,
                        _draggingCell.CanvasX, _draggingCell.CanvasY,
                        _draggingCell.ColSpan, _draggingCell.RowSpan))
                {
                    _draggingCell.CanvasX = _dragStartX;
                    _draggingCell.CanvasY = _dragStartY;
                }
                _draggingCell.IsDragInvalid = false;
                _draggingCell.IsDragging = false;
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
        UpdateSelectionState();
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
            _resizeStartColSpan = cell.ColSpan;
            _resizeStartRowSpan = cell.RowSpan;
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

        bool collision = GridLayoutService.HasLayerCollision(GridCells, _resizingCell.CollisionLayer, _resizingCell,
            _resizingCell.CanvasX, _resizingCell.CanvasY, newCols, newRows);

        _resizingCell.IsDragInvalid = collision;
        _resizingCell.ColSpan = newCols;
        _resizingCell.RowSpan = newRows;
    }

    private void ResizeThumb_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizing && sender is Control && _resizingCell != null)
        {
            // Check for collision and revert if needed
            bool collision = GridLayoutService.HasLayerCollision(GridCells, _resizingCell.CollisionLayer, _resizingCell,
                _resizingCell.CanvasX, _resizingCell.CanvasY, _resizingCell.ColSpan, _resizingCell.RowSpan);

            if (collision)
            {
                _resizingCell.ColSpan = _resizeStartColSpan;
                _resizingCell.RowSpan = _resizeStartRowSpan;
            }

            // Clear IsDragInvalid flag
            _resizingCell.IsDragInvalid = false;

            e.Pointer.Capture(null);
            _isResizing = false;
            _resizingCell = null;
            e.Handled = true;
            SaveBoardData();
        }
    }

    #endregion
}
