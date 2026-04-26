using System;
using System.Collections.Generic;
using Avalonia;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services.Transform;

public static class TransformBoundsCalculator
{
    public static Rect GetCellBounds(CellViewModel cell)
        => new(
            cell.VisualX,
            cell.VisualY,
            cell.PixelWidth,
            cell.PixelHeight);

    public static Rect GetAnnotationBounds(AnnotationViewModel annotation)
        => AnnotationBoundsHelper.GetRenderedBounds(annotation);

    public static Rect? GetSelectionBounds(IEnumerable<CellViewModel> cells, IEnumerable<AnnotationViewModel> annotations)
    {
        var snapshots = CreateSnapshots(cells, annotations);
        if (snapshots.Count == 0)
        {
            return null;
        }

        var bounds = snapshots[0].Bounds;
        for (int i = 1; i < snapshots.Count; i++)
        {
            bounds = Union(bounds, snapshots[i].Bounds);
        }

        return bounds;
    }

    public static IReadOnlyList<TransformItemSnapshot> CreateSnapshots(IEnumerable<CellViewModel> cells, IEnumerable<AnnotationViewModel> annotations)
    {
        var snapshots = new List<TransformItemSnapshot>();

        foreach (var cell in cells)
        {
            if (!cell.HasContent)
            {
                continue;
            }

            snapshots.Add(TransformItemSnapshot.FromCell(cell, GetCellBounds(cell)));
        }

        foreach (var annotation in annotations)
        {
            if (annotation.Points.Count == 0)
            {
                continue;
            }

            snapshots.Add(TransformItemSnapshot.FromAnnotation(annotation, GetAnnotationBounds(annotation)));
        }

        return snapshots;
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
