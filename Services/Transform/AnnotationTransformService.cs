using System;
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
            if (annotation is null || snapshot.AnnotationPoints.Count == 0)
            {
                continue;
            }

            if (annotation.Type == "Text")
            {
                ApplyTextResize(snapshot, originalSelectionBounds, resizedSelectionBounds);
                continue;
            }

            var mappedPoints = new Point[snapshot.AnnotationPoints.Count];
            double minX = double.MaxValue;
            double minY = double.MaxValue;

            for (int i = 0; i < snapshot.AnnotationPoints.Count; i++)
            {
                var absolutePoint = new Point(snapshot.CanvasX + snapshot.AnnotationPoints[i].X, snapshot.CanvasY + snapshot.AnnotationPoints[i].Y);
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

    private static void ApplyTextResize(TransformItemSnapshot snapshot, Rect originalSelectionBounds, Rect resizedSelectionBounds)
    {
        var annotation = snapshot.Annotation!;
        var anchor = new Point(snapshot.CanvasX + snapshot.AnnotationPoints[0].X, snapshot.CanvasY + snapshot.AnnotationPoints[0].Y);
        var mappedAnchor = TransformMath.MapPointBetweenRects(anchor, originalSelectionBounds, resizedSelectionBounds);

        annotation.CanvasX = mappedAnchor.X - snapshot.AnnotationPoints[0].X;
        annotation.CanvasY = mappedAnchor.Y - snapshot.AnnotationPoints[0].Y;

        double widthScale = GetScale(resizedSelectionBounds.Width, originalSelectionBounds.Width);
        double heightScale = GetScale(resizedSelectionBounds.Height, originalSelectionBounds.Height);
        double uniformScale = Math.Max(0.25, GetTextScale(widthScale, heightScale));
        annotation.TextScale = snapshot.TextScale * uniformScale;
        annotation.UpdateBoundsCache();
    }

    private static double GetTextScale(double widthScale, double heightScale)
        => Math.Abs(widthScale - 1.0) >= Math.Abs(heightScale - 1.0)
            ? widthScale
            : heightScale;

    private static double GetScale(double resized, double original)
        => original <= 0 ? 1.0 : resized / original;
}
