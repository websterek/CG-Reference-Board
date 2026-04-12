using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models;
using CGReferenceBoard.ViewModels;
using SkiaSharp;

namespace CGReferenceBoard.Services;

/// <summary>
/// Stateless service for grid layout calculations: collision detection, empty-space search,
/// image sizing, and backdrop containment queries. All methods are pure — they take the
/// current board state as parameters rather than holding a reference to it.
/// </summary>
public static class GridLayoutService
{
    /// <summary>
    /// Checks whether a rectangle on a given collision layer overlaps any existing cell
    /// on that same layer, optionally excluding one cell (the one being moved/resized).
    /// </summary>
    public static bool HasLayerCollision(IEnumerable<CellViewModel> cells, int layer, CellViewModel? exclude,
        double x, double y, int cols, int rows)
    {
        double right = x + cols * Constants.GridSize;
        double bottom = y + rows * Constants.GridSize;

        return cells.Any(c =>
        {
            if (c == exclude || !c.HasContent || c.CollisionLayer != layer)
                return false;

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
    public static bool HasLayerCollision(IEnumerable<CellViewModel> cells, int layer, IEnumerable<CellViewModel> excludeSet,
        double x, double y, int cols, int rows)
    {
        double right = x + cols * Constants.GridSize;
        double bottom = y + rows * Constants.GridSize;

        return cells.Any(c =>
        {
            if (excludeSet.Contains(c) || !c.HasContent || c.CollisionLayer != layer)
                return false;

            double margin = c.IsBackdrop ? Constants.GridSize / 2.0 : 0;
            double cellLeft = c.CanvasX - margin;
            double cellRight = c.CanvasX + c.ColSpan * Constants.GridSize + margin;
            double cellTop = c.CanvasY - margin;
            double cellBottom = c.CanvasY + c.RowSpan * Constants.GridSize + margin;

            return cellLeft < right && cellRight > x && cellTop < bottom && cellBottom > y;
        });
    }

    /// <summary>Checks if moving an entire group by (dx, dy) grid-pixels causes any same-layer collision.</summary>
    public static bool HasGroupCollision(IEnumerable<CellViewModel> allCells, IReadOnlyList<CellViewModel> group, double dx, double dy)
    {
        foreach (var cell in group)
        {
            double newX = cell.CanvasX + dx;
            double newY = cell.CanvasY + dy;
            if (HasLayerCollision(allCells, cell.CollisionLayer, group, newX, newY, cell.ColSpan, cell.RowSpan))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a rectangular area is free (no collision with existing cells on the same layer).
    /// </summary>
    public static bool IsSpaceEmpty(IEnumerable<CellViewModel> cells, double x, double y,
        int colSpan, int rowSpan, int collisionLayer, CellViewModel? excludeCell = null)
    {
        var rect = new Rect(x, y, colSpan * Constants.GridSize, rowSpan * Constants.GridSize);

        foreach (var cell in cells)
        {
            if (cell == excludeCell)
                continue;
            if (cell.CollisionLayer != collisionLayer)
                continue;

            Rect cellRect;
            if (cell.IsBackdrop)
            {
                double margin = Constants.GridSize / 2.0;
                cellRect = new Rect(
                    cell.CanvasX - margin,
                    cell.CanvasY - margin,
                    cell.ColSpan * Constants.GridSize + 2 * margin,
                    cell.RowSpan * Constants.GridSize + 2 * margin);
            }
            else
            {
                cellRect = new Rect(cell.CanvasX, cell.CanvasY,
                    cell.ColSpan * Constants.GridSize, cell.RowSpan * Constants.GridSize);
            }

            if (rect.Intersects(cellRect))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the nearest empty grid position that can fit the specified size.
    /// Uses spiral search outward from the preferred position.
    /// </summary>
    public static Point? FindEmptySpace(IEnumerable<CellViewModel> cells,
        double preferredX, double preferredY, int colSpan, int rowSpan,
        int collisionLayer, CellViewModel? excludeCell = null)
    {
        int gridX = (int)(Math.Floor(preferredX / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(preferredY / Constants.GridSize) * Constants.GridSize);

        if (IsSpaceEmpty(cells, gridX, gridY, colSpan, rowSpan, collisionLayer, excludeCell))
            return new Point(gridX, gridY);

        int maxDistance = 20;
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            for (int dx = -distance; dx <= distance; dx++)
            {
                for (int dy = -distance; dy <= distance; dy++)
                {
                    if (Math.Abs(dx) != distance && Math.Abs(dy) != distance)
                        continue;

                    int testX = gridX + dx * (int)Constants.GridSize;
                    int testY = gridY + dy * (int)Constants.GridSize;

                    if (IsSpaceEmpty(cells, testX, testY, colSpan, rowSpan, collisionLayer, excludeCell))
                        return new Point(testX, testY);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the actual pixel dimensions of an image file.
    /// Returns null if the file cannot be read.
    /// </summary>
    public static Size? GetImageDimensions(string imagePath)
    {
        try
        {
            // Use SKCodec to read only the file header — avoids decoding the full bitmap.
            using var codec = SKCodec.Create(imagePath);
            if (codec == null)
                return null;
            var info = codec.Info;
            return new Size(info.Width, info.Height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Calculates optimal ColSpan and RowSpan for an image based on its aspect ratio.
    /// Default is 2×2; wide images get wider spans, tall images get taller spans.
    /// </summary>
    public static (int colSpan, int rowSpan) CalculateOptimalCellSize(double imageWidth, double imageHeight)
    {
        int colSpan = 2;
        int rowSpan = 2;

        if (imageWidth == 0 || imageHeight == 0)
            return (colSpan, rowSpan);

        double aspectRatio = imageWidth / imageHeight;

        if (aspectRatio >= 3.0)
        { colSpan = 4; rowSpan = 1; }
        else if (aspectRatio >= 2.0)
        { colSpan = 3; rowSpan = 1; }
        else if (aspectRatio >= 1.5)
        { colSpan = 3; rowSpan = 2; }
        else if (aspectRatio <= 0.33)
        { colSpan = 1; rowSpan = 4; }
        else if (aspectRatio <= 0.5)
        { colSpan = 1; rowSpan = 3; }
        else if (aspectRatio <= 0.66)
        { colSpan = 2; rowSpan = 3; }

        return (colSpan, rowSpan);
    }

    /// <summary>
    /// Finds all cells that are visually contained within a backdrop.
    /// </summary>
    public static List<CellViewModel> GetBackdropChildren(IEnumerable<CellViewModel> cells, CellViewModel backdrop)
    {
        if (!backdrop.IsBackdrop)
            return new List<CellViewModel>();

        var children = new List<CellViewModel>();
        var backdropRect = new Rect(backdrop.CanvasX, backdrop.CanvasY, backdrop.PixelWidth, backdrop.PixelHeight);

        foreach (var cell in cells)
        {
            if (cell == backdrop || cell.IsBackdrop)
                continue;

            var cellRect = new Rect(cell.CanvasX, cell.CanvasY, cell.PixelWidth, cell.PixelHeight);
            if (backdropRect.Contains(cellRect))
                children.Add(cell);
        }

        return children;
    }

    /// <summary>
    /// Finds all annotations that are visually contained within a backdrop.
    /// </summary>
    public static List<AnnotationViewModel> GetBackdropAnnotations(
        IEnumerable<AnnotationViewModel> annotations, CellViewModel backdrop)
    {
        if (!backdrop.IsBackdrop)
            return new List<AnnotationViewModel>();

        var result = new List<AnnotationViewModel>();
        var backdropRect = new Rect(backdrop.CanvasX, backdrop.CanvasY, backdrop.PixelWidth, backdrop.PixelHeight);

        foreach (var annotation in annotations)
        {
            bool inRect = annotation.Points.Any(p =>
            {
                double px = p.X + annotation.CanvasX;
                double py = p.Y + annotation.CanvasY;
                return backdropRect.Contains(new Point(px, py));
            });

            if (inRect)
                result.Add(annotation);
        }

        return result;
    }

    /// <summary>
    /// Moves annotations that were inside cells after the cells moved.
    /// </summary>
    public static void MoveAnnotationsWithCells(
        IEnumerable<AnnotationViewModel> annotations,
        Dictionary<CellViewModel, Point> oldPositions)
    {
        var movedAnnotations = new HashSet<AnnotationViewModel>();

        foreach (var (cell, oldPos) in oldPositions)
        {
            double deltaX = cell.CanvasX - oldPos.X;
            double deltaY = cell.CanvasY - oldPos.Y;

            if (Math.Abs(deltaX) < 0.1 && Math.Abs(deltaY) < 0.1)
                continue;

            var cellRect = new Rect(oldPos.X, oldPos.Y, cell.PixelWidth, cell.PixelHeight);

            foreach (var annotation in annotations.ToList())
            {
                if (movedAnnotations.Contains(annotation))
                    continue;

                bool inRect = annotation.Points.Any(p =>
                {
                    double px = p.X + annotation.CanvasX;
                    double py = p.Y + annotation.CanvasY;
                    return cellRect.Contains(new Point(px, py));
                });

                if (inRect)
                {
                    annotation.CanvasX += deltaX;
                    annotation.CanvasY += deltaY;
                    movedAnnotations.Add(annotation);
                }
            }
        }
    }
}
