using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CGReferenceBoard.Helpers;

namespace CGReferenceBoard.Controls;

/// <summary>
/// High-performance canvas background pattern renderer.
/// Replaces VisualBrush tiling (which instantiates full Avalonia visual trees per tile)
/// with a single StreamGeometry batch draw — one DrawGeometry call for all dots/lines.
/// </summary>
public class BackgroundPatternControl : Control
{
    /// <summary>Background pattern type.</summary>
    public enum PatternType
    {
        None,
        Dots,
        Grid
    }

    public static readonly StyledProperty<PatternType> PatternProperty =
        AvaloniaProperty.Register<BackgroundPatternControl, PatternType>(nameof(Pattern), defaultValue: PatternType.Dots);

    /// <summary>Left edge of the visible viewport in control-local coordinates.</summary>
    public static readonly StyledProperty<double> ViewportLeftProperty =
        AvaloniaProperty.Register<BackgroundPatternControl, double>(nameof(ViewportLeft));

    /// <summary>Top edge of the visible viewport in control-local coordinates.</summary>
    public static readonly StyledProperty<double> ViewportTopProperty =
        AvaloniaProperty.Register<BackgroundPatternControl, double>(nameof(ViewportTop));

    /// <summary>Width of the visible viewport in control-local coordinates.</summary>
    public static readonly StyledProperty<double> ViewportWidthProperty =
        AvaloniaProperty.Register<BackgroundPatternControl, double>(nameof(ViewportWidth));

    /// <summary>Height of the visible viewport in control-local coordinates.</summary>
    public static readonly StyledProperty<double> ViewportHeightProperty =
        AvaloniaProperty.Register<BackgroundPatternControl, double>(nameof(ViewportHeight));

    public PatternType Pattern
    {
        get => GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    public double ViewportLeft
    {
        get => GetValue(ViewportLeftProperty);
        set => SetValue(ViewportLeftProperty, value);
    }

    public double ViewportTop
    {
        get => GetValue(ViewportTopProperty);
        set => SetValue(ViewportTopProperty, value);
    }

    public double ViewportWidth
    {
        get => GetValue(ViewportWidthProperty);
        set => SetValue(ViewportWidthProperty, value);
    }

    public double ViewportHeight
    {
        get => GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    // ── Cached brushes and pens ────────────────────────────────────────────

    private static readonly SolidColorBrush s_dotBrush = SolidColorBrush.Parse("#3D3D3D");
    private static readonly SolidColorBrush s_gridLineBrush = SolidColorBrush.Parse("#282828");
    private static readonly Pen s_gridLinePen = new(s_gridLineBrush, 0.5);

    static BackgroundPatternControl()
    {
        AffectsRender<BackgroundPatternControl>(
            PatternProperty,
            ViewportLeftProperty,
            ViewportTopProperty,
            ViewportWidthProperty,
            ViewportHeightProperty);
    }

    public override void Render(DrawingContext context)
    {
        if (Pattern == PatternType.None)
            return;

        double left = ViewportLeft;
        double top = ViewportTop;
        double width = ViewportWidth;
        double height = ViewportHeight;

        if (width <= 0 || height <= 0)
            return;

        double gridSize = Constants.GridSize;
        double right = left + width;
        double bottom = top + height;

        int startX = (int)Math.Floor(left / gridSize) - 1;
        int endX = (int)Math.Ceiling(right / gridSize) + 1;
        int startY = (int)Math.Floor(top / gridSize) - 1;
        int endY = (int)Math.Ceiling(bottom / gridSize) + 1;

        if (endX - startX <= 0 || endY - startY <= 0)
            return;

        if (Pattern == PatternType.Dots)
            RenderDots(context, gridSize, startX, endX, startY, endY);
        else
            RenderGrid(context, gridSize, startX, endX, startY, endY);
    }

    private static void RenderDots(DrawingContext context, double gridSize,
        int startX, int endX, int startY, int endY)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int y = startY; y <= endY; y++)
            {
                double cy = y * gridSize + gridSize / 2.0;
                for (int x = startX; x <= endX; x++)
                {
                    double cx = x * gridSize + gridSize / 2.0;
                    ctx.BeginFigure(new Point(cx - 3, cy - 3), true);
                    ctx.LineTo(new Point(cx + 3, cy - 3));
                    ctx.LineTo(new Point(cx + 3, cy + 3));
                    ctx.LineTo(new Point(cx - 3, cy + 3));
                    ctx.EndFigure(true);
                }
            }
        }

        context.DrawGeometry(s_dotBrush, null, geometry);
    }

    private static void RenderGrid(DrawingContext context, double gridSize,
        int startX, int endX, int startY, int endY)
    {
        double lineStartX = startX * gridSize;
        double lineEndX = (endX + 1) * gridSize;
        double lineStartY = startY * gridSize;
        double lineEndY = (endY + 1) * gridSize;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int y = startY; y <= endY + 1; y++)
            {
                double cy = y * gridSize;
                ctx.BeginFigure(new Point(lineStartX, cy), false);
                ctx.LineTo(new Point(lineEndX, cy));
                ctx.EndFigure(false);
            }

            for (int x = startX; x <= endX + 1; x++)
            {
                double cx = x * gridSize;
                ctx.BeginFigure(new Point(cx, lineStartY), false);
                ctx.LineTo(new Point(cx, lineEndY));
                ctx.EndFigure(false);
            }
        }

        context.DrawGeometry(null, s_gridLinePen, geometry);
    }
}
