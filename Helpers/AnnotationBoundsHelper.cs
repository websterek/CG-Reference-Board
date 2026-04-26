using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Helpers;

internal static class AnnotationBoundsHelper
{
    public static Rect GetLocalContentBounds(AnnotationViewModel annotation)
    {
        if (annotation.Points.Count == 0)
        {
            return default;
        }

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (var pt in annotation.Points)
        {
            if (pt.X < minX)
                minX = pt.X;
            if (pt.X > maxX)
                maxX = pt.X;
            if (pt.Y < minY)
                minY = pt.Y;
            if (pt.Y > maxY)
                maxY = pt.Y;
        }

        if (annotation.Type == "Text")
        {
            var ft = new FormattedText(
                annotation.Text ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Arial"),
                Math.Max(12, annotation.Thickness * 4 + 10),
                Brushes.White);

            maxX = minX + Math.Max(40, ft.Width + 20);
            maxY = minY + Math.Max(20, ft.Height + 20);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public static Rect GetRenderedBounds(AnnotationViewModel annotation)
    {
        var localBounds = GetLocalContentBounds(annotation);
        var pad = GetRenderPadding(annotation);

        return new Rect(
            annotation.CanvasX + localBounds.X - pad,
            annotation.CanvasY + localBounds.Y - pad,
            localBounds.Width + pad * 2,
            localBounds.Height + pad * 2);
    }

    public static double GetRenderPadding(AnnotationViewModel annotation)
        => annotation.Thickness + Constants.AnnotationEffectPadding;
}
