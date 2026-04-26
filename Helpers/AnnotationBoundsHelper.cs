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

    public static double GetRenderPadding(AnnotationViewModel annotation)
        => annotation.Thickness + Constants.AnnotationEffectPadding;

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
