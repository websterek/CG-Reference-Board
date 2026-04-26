using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Helpers;

internal static class AnnotationBoundsHelper
{
    public static bool Intersects(Rect bounds, Rect target)
        => bounds.Right > target.X
           && bounds.X < target.Right
           && bounds.Bottom > target.Y
           && bounds.Y < target.Bottom;

    public static bool IntersectsRenderedBounds(AnnotationViewModel annotation, Rect target)
        => Intersects(GetRenderedBounds(annotation), target);

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
            var textSize = MeasureText(annotation);

            maxX = minX + Math.Max(40, textSize.Width + 20);
            maxY = minY + Math.Max(20, textSize.Height + 20);
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

    public static Rect? GetRenderedBoundsUnion(IEnumerable<AnnotationViewModel> annotations)
    {
        Rect? bounds = null;

        foreach (var annotation in annotations)
        {
            if (annotation.Points.Count == 0)
            {
                continue;
            }

            var renderedBounds = GetRenderedBounds(annotation);
            bounds = bounds is null ? renderedBounds : Union(bounds.Value, renderedBounds);
        }

        return bounds;
    }

    public static double GetRenderPadding(AnnotationViewModel annotation)
        => annotation.Thickness + Constants.AnnotationEffectPadding;

    private static Rect Union(Rect left, Rect right)
    {
        var x1 = Math.Min(left.X, right.X);
        var y1 = Math.Min(left.Y, right.Y);
        var x2 = Math.Max(left.Right, right.Right);
        var y2 = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }

    private static Size MeasureText(AnnotationViewModel annotation)
    {
        double fontSize = AnnotationViewModel.GetTextFontSize(annotation);

        try
        {
            var ft = new FormattedText(
                annotation.Text ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Arial"),
                fontSize,
                Brushes.White);

            return new Size(ft.Width, ft.Height);
        }
        catch (InvalidOperationException)
        {
            return EstimateText(annotation.Text ?? string.Empty, fontSize);
        }
    }

    private static Size EstimateText(string text, double fontSize)
    {
        var lines = text.Split('\n');
        int longestLine = 0;
        foreach (var line in lines)
        {
            longestLine = Math.Max(longestLine, line.Length);
        }

        double width = longestLine * fontSize * 0.6;
        double height = Math.Max(1, lines.Length) * fontSize * 1.2;
        return new Size(width, height);
    }
}
