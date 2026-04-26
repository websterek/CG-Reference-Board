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

    public static bool IntersectsRenderedGeometry(AnnotationViewModel annotation, Rect target)
    {
        if (annotation.Points.Count == 0 || target.Width <= 0 || target.Height <= 0)
        {
            return false;
        }

        if (!IntersectsRenderedBounds(annotation, target))
        {
            return false;
        }

        return annotation.Type switch
        {
            "Brush" or "Arrow" => StrokeIntersects(annotation, target),
            "Ellipse" => EllipseIntersects(annotation, target),
            "Rectangle" => RectangleIntersects(annotation, target),
            "Text" => Intersects(GetRenderedBounds(annotation), target),
            _ => Intersects(GetRenderedBounds(annotation), target)
        };
    }

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

    private static bool StrokeIntersects(AnnotationViewModel annotation, Rect target)
    {
        if (annotation.Points.Count == 1)
        {
            var p = ToAbsolute(annotation, annotation.Points[0]);
            return target.Inflate(GetGeometryTolerance(annotation)).Contains(p);
        }

        var inflated = target.Inflate(GetGeometryTolerance(annotation));
        for (int i = 0; i < annotation.Points.Count - 1; i++)
        {
            var a = ToAbsolute(annotation, annotation.Points[i]);
            var b = ToAbsolute(annotation, annotation.Points[i + 1]);
            if (LineIntersectsRect(a, b, inflated))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RectangleIntersects(AnnotationViewModel annotation, Rect target)
    {
        if (annotation.Points.Count < 2)
        {
            return StrokeIntersects(annotation, target);
        }

        var rect = GetShapeRect(annotation);
        var tolerance = GetGeometryTolerance(annotation);
        var top = new Rect(rect.Left - tolerance, rect.Top - tolerance, rect.Width + tolerance * 2, tolerance * 2);
        var bottom = new Rect(rect.Left - tolerance, rect.Bottom - tolerance, rect.Width + tolerance * 2, tolerance * 2);
        var left = new Rect(rect.Left - tolerance, rect.Top - tolerance, tolerance * 2, rect.Height + tolerance * 2);
        var right = new Rect(rect.Right - tolerance, rect.Top - tolerance, tolerance * 2, rect.Height + tolerance * 2);
        return top.Intersects(target) || bottom.Intersects(target) || left.Intersects(target) || right.Intersects(target);
    }

    private static bool EllipseIntersects(AnnotationViewModel annotation, Rect target)
    {
        if (annotation.Points.Count < 2)
        {
            return StrokeIntersects(annotation, target);
        }

        var rect = GetShapeRect(annotation);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return target.Inflate(GetGeometryTolerance(annotation)).Contains(rect.Position);
        }

        var center = rect.Center;
        double rx = rect.Width / 2.0;
        double ry = rect.Height / 2.0;
        double tolerance = GetGeometryTolerance(annotation);
        var samplePoints = new[]
        {
            target.TopLeft,
            target.TopRight,
            target.BottomLeft,
            target.BottomRight,
            new Point(target.Center.X, target.Top),
            new Point(target.Center.X, target.Bottom),
            new Point(target.Left, target.Center.Y),
            new Point(target.Right, target.Center.Y),
            target.Center
        };

        foreach (var point in samplePoints)
        {
            double normalized = Math.Pow((point.X - center.X) / rx, 2) + Math.Pow((point.Y - center.Y) / ry, 2);
            double expanded = Math.Pow((rx + tolerance) / rx, 2);
            double contracted = rx > tolerance && ry > tolerance
                ? Math.Pow((Math.Max(0, rx - tolerance)) / rx, 2)
                : 0;
            if (normalized <= expanded && normalized >= contracted)
            {
                return true;
            }
        }

        return rect.Contains(target.Center) && !new Rect(rect.X + tolerance, rect.Y + tolerance, Math.Max(0, rect.Width - tolerance * 2), Math.Max(0, rect.Height - tolerance * 2)).Contains(target.Center);
    }

    private static Rect GetShapeRect(AnnotationViewModel annotation)
    {
        var start = ToAbsolute(annotation, annotation.Points[0]);
        var end = ToAbsolute(annotation, annotation.Points[^1]);
        return new Rect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));
    }

    private static Point ToAbsolute(AnnotationViewModel annotation, Point point)
        => new(point.X + annotation.CanvasX, point.Y + annotation.CanvasY);

    private static double GetGeometryTolerance(AnnotationViewModel annotation)
        => Math.Max(2, annotation.Thickness / 2.0 + 2);

    private static bool LineIntersectsRect(Point a, Point b, Rect rect)
    {
        if (rect.Contains(a) || rect.Contains(b))
        {
            return true;
        }

        return SegmentsIntersect(a, b, rect.TopLeft, rect.TopRight)
            || SegmentsIntersect(a, b, rect.TopRight, rect.BottomRight)
            || SegmentsIntersect(a, b, rect.BottomRight, rect.BottomLeft)
            || SegmentsIntersect(a, b, rect.BottomLeft, rect.TopLeft);
    }

    private static bool SegmentsIntersect(Point a, Point b, Point c, Point d)
    {
        static double Cross(Point p, Point q, Point r)
            => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);

        static bool OnSegment(Point p, Point q, Point r)
            => q.X >= Math.Min(p.X, r.X) && q.X <= Math.Max(p.X, r.X)
               && q.Y >= Math.Min(p.Y, r.Y) && q.Y <= Math.Max(p.Y, r.Y);

        double c1 = Cross(a, b, c);
        double c2 = Cross(a, b, d);
        double c3 = Cross(c, d, a);
        double c4 = Cross(c, d, b);

        if (((c1 > 0 && c2 < 0) || (c1 < 0 && c2 > 0))
            && ((c3 > 0 && c4 < 0) || (c3 < 0 && c4 > 0)))
        {
            return true;
        }

        const double epsilon = 0.0001;
        return Math.Abs(c1) < epsilon && OnSegment(a, c, b)
            || Math.Abs(c2) < epsilon && OnSegment(a, d, b)
            || Math.Abs(c3) < epsilon && OnSegment(c, a, d)
            || Math.Abs(c4) < epsilon && OnSegment(c, b, d);
    }

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
