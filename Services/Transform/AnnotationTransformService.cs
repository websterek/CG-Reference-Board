using System.Collections.Generic;
using Avalonia;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services.Transform;

public static class AnnotationTransformService
{
    public static void ApplyMove(IReadOnlyList<TransformItemSnapshot> snapshots, Vector delta)
    {
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Annotation is null)
            {
                continue;
            }

            snapshot.Annotation.CanvasX = snapshot.CanvasX + delta.X;
            snapshot.Annotation.CanvasY = snapshot.CanvasY + delta.Y;
        }
    }

    public static void ApplyResize(IReadOnlyList<TransformItemSnapshot> snapshots, Rect originalSelectionBounds, Rect resizedSelectionBounds)
    {
        foreach (var snapshot in snapshots)
        {
            var annotation = snapshot.Annotation;
            if (annotation is null || annotation.Points.Count == 0)
            {
                continue;
            }

            var mappedPoints = new Point[annotation.Points.Count];
            double minX = double.MaxValue;
            double minY = double.MaxValue;

            for (int i = 0; i < annotation.Points.Count; i++)
            {
                var absolutePoint = new Point(annotation.CanvasX + annotation.Points[i].X, annotation.CanvasY + annotation.Points[i].Y);
                var mappedPoint = TransformMath.MapPointBetweenRects(absolutePoint, originalSelectionBounds, resizedSelectionBounds);
                mappedPoints[i] = mappedPoint;

                if (mappedPoint.X < minX)
                {
                    minX = mappedPoint.X;
                }

                if (mappedPoint.Y < minY)
                {
                    minY = mappedPoint.Y;
                }
            }

            annotation.CanvasX = minX;
            annotation.CanvasY = minY;

            for (int i = 0; i < mappedPoints.Length; i++)
            {
                annotation.Points[i] = new Point(mappedPoints[i].X - minX, mappedPoints[i].Y - minY);
            }

            annotation.UpdateBoundsCache();
        }
    }
}
