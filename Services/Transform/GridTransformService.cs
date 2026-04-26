using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Layers.Infrastructure;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services.Transform;

public static class GridTransformService
{
    public static IReadOnlyList<TransformItemSnapshot> CreateExpandedMoveSnapshots(
        IEnumerable<CellViewModel> selectedCells,
        IEnumerable<AnnotationViewModel> selectedAnnotations,
        IEnumerable<CellViewModel> allCells,
        IEnumerable<AnnotationViewModel> allAnnotations)
    {
        var cellsToMove = new List<CellViewModel>();
        var annotationsToMove = new List<AnnotationViewModel>();

        foreach (var cell in selectedCells)
        {
            if (cell.HasContent && !cellsToMove.Contains(cell))
            {
                cellsToMove.Add(cell);
            }
        }

        foreach (var annotation in selectedAnnotations)
        {
            if (annotation.Points.Count > 0 && !annotationsToMove.Contains(annotation))
            {
                annotationsToMove.Add(annotation);
            }
        }

        foreach (var backdrop in cellsToMove.Where(cell => cell.IsBackdrop).ToList())
        {
            double left = backdrop.VisualX;
            double top = backdrop.VisualY;
            double right = left + backdrop.PixelWidth;
            double bottom = top + backdrop.PixelHeight;

            foreach (var cell in allCells)
            {
                if (!cell.HasContent || cellsToMove.Contains(cell))
                {
                    continue;
                }

                double cx = cell.CanvasX;
                double cy = cell.CanvasY;
                double cw = cell.ColSpan * Constants.GridSize;
                double ch = cell.RowSpan * Constants.GridSize;

                bool intersects = cx < right && cx + cw > left && cy < bottom && cy + ch > top;
                if (intersects)
                {
                    cellsToMove.Add(cell);
                }
            }
        }

        foreach (var cell in cellsToMove)
        {
            var cellBounds = new Rect(cell.VisualX, cell.VisualY, cell.PixelWidth, cell.PixelHeight);

            foreach (var annotation in allAnnotations)
            {
                if (annotationsToMove.Contains(annotation))
                {
                    continue;
                }

                bool intersects = AnnotationBoundsHelper.IntersectsRenderedBounds(annotation, cellBounds);

                if (intersects)
                {
                    annotationsToMove.Add(annotation);
                }
            }
        }

        return TransformBoundsCalculator.CreateSnapshots(cellsToMove, annotationsToMove);
    }

    public static void ApplyMove(IReadOnlyList<TransformItemSnapshot> snapshots, Vector rawDelta)
    {
        var snappedDelta = TransformMath.SnapVectorToGrid(rawDelta);

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Cell is not null)
            {
                snapshot.Cell.CanvasX = snapshot.CanvasX + snappedDelta.X;
                snapshot.Cell.CanvasY = snapshot.CanvasY + snappedDelta.Y;
                continue;
            }

            if (snapshot.Annotation is not null)
            {
                snapshot.Annotation.CanvasX = snapshot.CanvasX + snappedDelta.X;
                snapshot.Annotation.CanvasY = snapshot.CanvasY + snappedDelta.Y;
            }
        }
    }

    public static void ApplyResize(IReadOnlyList<TransformItemSnapshot> snapshots, Rect originalSelectionBounds, Rect resizedSelectionBounds)
    {
        var originalCellBounds = GetCellSelectionBounds(snapshots);
        if (originalCellBounds is null)
        {
            return;
        }

        var mappedCellTopLeft = TransformMath.MapPointBetweenRects(originalCellBounds.Value.TopLeft, originalSelectionBounds, resizedSelectionBounds);
        var mappedCellBottomRight = TransformMath.MapPointBetweenRects(originalCellBounds.Value.BottomRight, originalSelectionBounds, resizedSelectionBounds);
        var cellDestinationBounds = TransformMath.SnapRectToGrid(CreateRect(mappedCellTopLeft, mappedCellBottomRight));

        foreach (var snapshot in snapshots)
        {
            var cell = snapshot.Cell;
            if (cell is null)
            {
                continue;
            }

            var mappedTopLeft = TransformMath.MapPointBetweenRects(snapshot.Bounds.TopLeft, originalCellBounds.Value, cellDestinationBounds);
            var mappedBottomRight = TransformMath.MapPointBetweenRects(snapshot.Bounds.BottomRight, originalCellBounds.Value, cellDestinationBounds);
            var mappedRect = CreateRect(mappedTopLeft, mappedBottomRight);
            var snappedCellRect = TransformMath.SnapRectToGrid(mappedRect);

            if (cell.IsBackdrop)
            {
                cell.CanvasX = snappedCellRect.X + Constants.BackdropPadding;
                cell.CanvasY = snappedCellRect.Y + Constants.BackdropPadding;
                cell.ColSpan = Math.Max(1, (int)Math.Round((snappedCellRect.Width - 2 * Constants.BackdropPadding) / Constants.GridSize));
                cell.RowSpan = Math.Max(1, (int)Math.Round((snappedCellRect.Height - 2 * Constants.BackdropPadding) / Constants.GridSize));
                continue;
            }

            cell.CanvasX = snappedCellRect.X;
            cell.CanvasY = snappedCellRect.Y;
            cell.ColSpan = Math.Max(1, (int)Math.Round(snappedCellRect.Width / Constants.GridSize));
            cell.RowSpan = Math.Max(1, (int)Math.Round(snappedCellRect.Height / Constants.GridSize));
        }
    }

    public static bool HasCollision(
        IReadOnlyList<TransformItemSnapshot> snapshots,
        TransformOperation operation,
        IReadOnlyList<CellViewModel> gridCells,
        LayerManager layerManager)
    {
        var activeCells = snapshots
            .Where(snapshot => snapshot.Cell is not null)
            .Select(snapshot => snapshot.Cell!)
            .ToList();

        if (activeCells.Count == 0)
        {
            return false;
        }

        if (operation == TransformOperation.Move)
        {
            var reference = snapshots.First(snapshot => snapshot.Cell is not null);
            double dx = reference.Cell!.CanvasX - reference.CanvasX;
            double dy = reference.Cell.CanvasY - reference.CanvasY;
            return GridLayoutService.HasGroupCollision(gridCells, activeCells, layerManager, dx, dy);
        }

        foreach (var cell in activeCells)
        {
            var owningLayer = layerManager.ResolveLayer(cell);
            if (owningLayer != null && GridLayoutService.HasLayerCollision(gridCells, owningLayer, activeCells, cell.CanvasX, cell.CanvasY, cell.ColSpan, cell.RowSpan))
            {
                return true;
            }
        }

        return false;
    }

    public static void RestoreSnapshots(IReadOnlyList<TransformItemSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Cell is not null)
            {
                snapshot.Cell.CanvasX = snapshot.CanvasX;
                snapshot.Cell.CanvasY = snapshot.CanvasY;
                snapshot.Cell.ColSpan = snapshot.ColSpan;
                snapshot.Cell.RowSpan = snapshot.RowSpan;
            }

            if (snapshot.Annotation is not null)
            {
                snapshot.Annotation.CanvasX = snapshot.CanvasX;
                snapshot.Annotation.CanvasY = snapshot.CanvasY;
                snapshot.Annotation.TextScale = snapshot.TextScale;

                for (int i = 0; i < snapshot.AnnotationPoints.Count; i++)
                {
                    snapshot.Annotation.Points[i] = snapshot.AnnotationPoints[i];
                }

                snapshot.Annotation.UpdateBoundsCache();
            }
        }
    }

    public static void SetInvalidState(IReadOnlyList<TransformItemSnapshot> snapshots, bool isInvalid)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Cell is not null)
            {
                snapshot.Cell.IsDragInvalid = isInvalid;
            }
        }
    }

    public static void ClearInvalidState(IReadOnlyList<TransformItemSnapshot> snapshots)
        => SetInvalidState(snapshots, isInvalid: false);

    private static Rect? GetCellSelectionBounds(IReadOnlyList<TransformItemSnapshot> snapshots)
    {
        Rect? bounds = null;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Cell is null)
            {
                continue;
            }

            bounds = bounds is null ? snapshot.Bounds : Union(bounds.Value, snapshot.Bounds);
        }

        return bounds;
    }

    private static Rect CreateRect(Point first, Point second)
        => new(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Abs(second.X - first.X),
            Math.Abs(second.Y - first.Y));

    private static Rect Union(Rect left, Rect right)
    {
        var x1 = Math.Min(left.X, right.X);
        var y1 = Math.Min(left.Y, right.Y);
        var x2 = Math.Max(left.Right, right.Right);
        var y2 = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }
}
