using System;
using System.Collections.Generic;
using Avalonia;
using CGReferenceBoard.Helpers;

namespace CGReferenceBoard.Services.Transform;

public static class GridTransformService
{
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
        _ = originalSelectionBounds;
        var snappedSelectionBounds = TransformMath.SnapRectToGrid(resizedSelectionBounds);
        var originalCellBounds = GetCellSelectionBounds(snapshots);
        if (originalCellBounds is null)
        {
            return;
        }

        foreach (var snapshot in snapshots)
        {
            var cell = snapshot.Cell;
            if (cell is null)
            {
                continue;
            }

            var mappedTopLeft = TransformMath.MapPointBetweenRects(snapshot.Bounds.TopLeft, originalCellBounds.Value, snappedSelectionBounds);
            var mappedBottomRight = TransformMath.MapPointBetweenRects(snapshot.Bounds.BottomRight, originalCellBounds.Value, snappedSelectionBounds);
            var mappedRect = new Rect(
                Math.Min(mappedTopLeft.X, mappedBottomRight.X),
                Math.Min(mappedTopLeft.Y, mappedBottomRight.Y),
                Math.Abs(mappedBottomRight.X - mappedTopLeft.X),
                Math.Abs(mappedBottomRight.Y - mappedTopLeft.Y));
            var snappedCellRect = TransformMath.SnapRectToGrid(mappedRect);

            cell.CanvasX = snappedCellRect.X;
            cell.CanvasY = snappedCellRect.Y;
            cell.ColSpan = Math.Max(1, (int)Math.Round(snappedCellRect.Width / Constants.GridSize));
            cell.RowSpan = Math.Max(1, (int)Math.Round(snappedCellRect.Height / Constants.GridSize));
        }
    }

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

    private static Rect Union(Rect left, Rect right)
    {
        var x1 = Math.Min(left.X, right.X);
        var y1 = Math.Min(left.Y, right.Y);
        var x2 = Math.Max(left.Right, right.Right);
        var y2 = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }
}
