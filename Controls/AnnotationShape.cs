using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Controls;

/// <summary>
/// Annotation effect modes.
/// </summary>
public enum AnnotationEffect
{
    None,
    Shadow,
    Outline
}

/// <summary>
/// Custom control that renders an <see cref="AnnotationViewModel"/> as a drawn shape
/// (brush stroke, rectangle, ellipse, arrow, or text) on the annotation layer.
/// Supports optional shadow or outline effects for readability.
/// </summary>
public class AnnotationShape : Control
{
    // ───────── Static effect state ─────────

    private static AnnotationEffect _currentEffect = AnnotationEffect.None;

    /// <summary>Current global effect mode for all annotation shapes.</summary>
    public static AnnotationEffect CurrentEffect => _currentEffect;

    /// <summary>Raised when the effect mode changes so all instances can re-render.</summary>
    public static event Action? EffectModeChanged;

    /// <summary>
    /// Sets the global effect mode and notifies all instances to re-render.
    /// </summary>
    public static void SetEffectMode(AnnotationEffect mode)
    {
        if (_currentEffect == mode)
            return;
        _currentEffect = mode;
        EffectModeChanged?.Invoke();
    }

    // ───────── Static scale state ─────────

    private static double _currentScale = 1.0;

    /// <summary>Raised when the scale changes so instances can invalidate their geometry cache.</summary>
    public static event Action? ScaleChanged;

    /// <summary>
    /// Sets the global scale and notifies all instances.
    /// </summary>
    public static void SetScale(double scale)
    {
        _currentScale = scale;
        ScaleChanged?.Invoke();
    }

    // ───────── Instance ─────────

    public static readonly StyledProperty<AnnotationViewModel?> AnnotationProperty =
        AvaloniaProperty.Register<AnnotationShape, AnnotationViewModel?>(nameof(Annotation));

    public AnnotationViewModel? Annotation
    {
        get => GetValue(AnnotationProperty);
        set => SetValue(AnnotationProperty, value);
    }

    // ───────── Per-instance geometry cache ─────────

    private StreamGeometry? _cachedGeometry;
    private double _cachedGeometryOx = double.NaN;
    private double _cachedGeometryOy = double.NaN;
    private int _cachedStep = -1;
    private bool _geometryDirty = true;

    // ───────── Per-instance brush/pen cache ─────────

    private IBrush? _cachedBrush;
    private string? _cachedBrushColor;
    private Pen? _cachedPen;
    private double _cachedPenThickness = -1;

    public AnnotationShape()
    {
        ClipToBounds = false;
        EffectModeChanged += OnEffectModeChanged;
        ScaleChanged += OnScaleChanged;
    }

    private void OnEffectModeChanged() => InvalidateVisual();

    private void OnScaleChanged()
    {
        var vm = Annotation;
        // Mark brush geometry dirty so point-decimation LOD rebuilds at new scale
        if (vm?.Type == "Brush" && vm.Points.Count > 10)
            _geometryDirty = true;
        // Always redraw — effect threshold (50 %) applies to every annotation type
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EffectModeChanged -= OnEffectModeChanged;
        ScaleChanged -= OnScaleChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != AnnotationProperty)
            return;

        if (change.OldValue is AnnotationViewModel oldVm)
        {
            oldVm.Points.CollectionChanged -= OnPointsCollectionChanged;
            oldVm.PropertyChanged -= OnAnnotationPropertyChanged;
        }

        if (change.NewValue is AnnotationViewModel newVm)
        {
            newVm.Points.CollectionChanged += OnPointsCollectionChanged;
            newVm.PropertyChanged += OnAnnotationPropertyChanged;
            // Seed the bounding-box cache so viewport culling works correctly
            // for annotations that already have all their points (e.g. loaded from file).
            newVm.UpdateBoundsCache();
        }

        UpdateBounds();
        InvalidateAll();
    }

    private void OnAnnotationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AnnotationViewModel.IsSelected):
                // selection only changes the outline pen — no layout change
                InvalidateVisual();
                break;
            case nameof(AnnotationViewModel.Color):
                _cachedBrush = null;
                _cachedBrushColor = null;
                _cachedPen = null;
                InvalidateVisual();
                break;
            case nameof(AnnotationViewModel.Thickness):
            case nameof(AnnotationViewModel.Type):
                _cachedPen = null;
                _geometryDirty = true;
                InvalidateAll();
                break;
            case nameof(AnnotationViewModel.Text):
                InvalidateAll();
                break;
        }
    }

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _geometryDirty = true;
        Annotation?.UpdateBoundsCache();

        UpdateBounds();
        InvalidateAll();
    }

    private void InvalidateAll()
    {
        InvalidateVisual();
        InvalidateMeasure();
        InvalidateArrange();
    }

    private double _minX, _minY;

    private void UpdateBounds()
    {
        var vm = Annotation;
        if (vm == null || vm.Points.Count == 0)
            return;

        _minX = double.MaxValue;
        _minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (var pt in vm.Points)
        {
            if (pt.X < _minX)
                _minX = pt.X;
            if (pt.X > maxX)
                maxX = pt.X;
            if (pt.Y < _minY)
                _minY = pt.Y;
            if (pt.Y > maxY)
                maxY = pt.Y;
        }

        if (vm.Type == "Text")
        {
            // Reserve rough space for text to avoid clipping
            maxX = _minX + 300;
            maxY = _minY + 100;
        }

        double pad = vm.Thickness + Constants.AnnotationEffectPadding;
        Width = (maxX - _minX) + pad * 2;
        Height = (maxY - _minY) + pad * 2;
        Margin = new Thickness(_minX - pad, _minY - pad, 0, 0);
    }

    // ───────── Effect helpers ─────────

    private static readonly IBrush ShadowBrush = SolidColorBrush.Parse(Constants.AnnotationShadowColor);
    private static readonly IBrush OutlineBrush = SolidColorBrush.Parse(Constants.AnnotationOutlineColor);

    private static Pen MakeShadowPen(double thickness)
        => new(ShadowBrush, thickness + Constants.AnnotationShadowExtraThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    private static Pen MakeOutlinePen(double thickness)
        => new(OutlineBrush, thickness + Constants.AnnotationOutlineExtraThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    // ───────── Brush/pen cache helpers ─────────

    private IBrush GetBrush(string color)
    {
        if (color == _cachedBrushColor && _cachedBrush != null)
            return _cachedBrush;
        _cachedBrush = SolidColorBrush.Parse(color);
        _cachedBrushColor = color;
        return _cachedBrush;
    }

    private Pen GetPen(IBrush brush, double thickness)
    {
        if (_cachedPen != null && _cachedPenThickness == thickness && _cachedBrushColor == (brush as SolidColorBrush)?.Color.ToString())
            return _cachedPen;
        _cachedPen = new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _cachedPenThickness = thickness;
        return _cachedPen;
    }

    // ───────── Geometry cache helpers ─────────

    private static int GetPointStep(double scale) => scale switch
    {
        < 0.1 => 16,
        < 0.2 => 8,
        < 0.4 => 4,
        < 0.7 => 2,
        _ => 1
    };

    private StreamGeometry GetOrBuildGeometry(AnnotationViewModel vm, double ox, double oy)
    {
        int step = GetPointStep(_currentScale);
        if (!_geometryDirty && _cachedGeometry != null &&
            _cachedGeometryOx == ox && _cachedGeometryOy == oy && _cachedStep == step)
        {
            return _cachedGeometry;
        }
        _cachedGeometry = BuildBrushGeometry(vm, ox, oy, step);
        _cachedGeometryOx = ox;
        _cachedGeometryOy = oy;
        _cachedStep = step;
        _geometryDirty = false;
        return _cachedGeometry;
    }

    // ───────── Hit pen helpers ─────────

    /// <summary>
    /// Returns a transparent pen whose stroke thickness — when multiplied by the current
    /// canvas scale — always covers roughly <paramref name="targetScreenRadius"/> pixels on
    /// each side of the drawn path.  The result is clamped to be at least as wide as the
    /// visible stroke so clicking right on the line always works.
    ///
    /// Avalonia's rendering-based hit testing checks whether a pointer position falls
    /// inside the recorded drawing operations.  A pen drawn with <see cref="Brushes.Transparent"/>
    /// is invisible but still creates a hit-testable surface via StrokeContains.
    /// </summary>
    private static Pen MakeScaledHitPen(double strokeThickness, double targetScreenRadius = 15.0)
    {
        // canvas units needed so that (units * scale) == targetScreenRadius
        double canvasRadius = targetScreenRadius / Math.Max(_currentScale, 0.01);
        double hitThickness = Math.Max(strokeThickness + 10.0, canvasRadius * 2.0);
        return new Pen(Brushes.Transparent, hitThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    }

    /// <summary>
    /// Standard hit pen used for shapes that already have a transparent fill covering
    /// their interior (Rectangle, Ellipse).  A moderately-wide stroke is enough here
    /// because clicking anywhere in the interior already hits via the fill.
    /// </summary>
    private static Pen MakeStandardHitPen(double strokeThickness)
        => new(Brushes.Transparent, Math.Max(strokeThickness + 10.0, 20.0), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    // ───────── Render ─────────

    public override void Render(DrawingContext context)
    {
        var vm = Annotation;
        if (vm == null || vm.Points.Count == 0)
            return;

        var brush = GetBrush(vm.Color);
        var pen = GetPen(brush, vm.Thickness);
        var selectPen = vm.IsSelected
            ? new Pen(SolidColorBrush.Parse(Constants.AccentColor), vm.Thickness + 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
            : null;

        // Skip expensive effects when zoomed out past 50% — visually irrelevant at that scale
        var effect = _currentScale <= 0.51 ? AnnotationEffect.None : _currentEffect;

        // Map absolute points to local coordinate space
        double offsetX = Bounds.X;
        double offsetY = Bounds.Y;

        switch (vm.Type)
        {
            case "Brush":
                RenderBrush(context, vm, pen, selectPen, effect, offsetX, offsetY);
                break;
            case "Rectangle":
                RenderRectangle(context, vm, pen, selectPen, effect, offsetX, offsetY);
                break;
            case "Ellipse":
                RenderEllipse(context, vm, pen, selectPen, effect, offsetX, offsetY);
                break;
            case "Arrow":
                RenderArrow(context, vm, pen, selectPen, effect, offsetX, offsetY);
                break;
            case "Text":
                RenderText(context, vm, brush, selectPen, effect, offsetX, offsetY);
                break;
        }
    }

    // ───────── Shape renderers ─────────

    private void RenderBrush(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;

        var geometry = GetOrBuildGeometry(vm, ox, oy);

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            using (ctx.PushTransform(Matrix.CreateTranslation(Constants.AnnotationShadowOffsetX, Constants.AnnotationShadowOffsetY)))
                ctx.DrawGeometry(null, MakeShadowPen(vm.Thickness), geometry);
        }
        else if (effect == AnnotationEffect.Outline)
        {
            ctx.DrawGeometry(null, MakeOutlinePen(vm.Thickness), geometry);
        }

        // Scale-aware transparent stroke: keeps ~15 screen-pixel grab radius at any zoom.
        // DrawGeometry with null fill + transparent pen → Avalonia uses StrokeContains for
        // hit testing, so the effective hit radius equals hitPen.Thickness / 2.
        ctx.DrawGeometry(null, MakeScaledHitPen(vm.Thickness), geometry);

        if (selectPen != null)
            ctx.DrawGeometry(null, selectPen, geometry);
        ctx.DrawGeometry(null, pen, geometry);
    }

    private static StreamGeometry BuildBrushGeometry(AnnotationViewModel vm, double ox, double oy, int step = 1)
    {
        var geometry = new StreamGeometry();
        using (var gc = geometry.Open())
        {
            if (step > 1 && vm.Points.Count > 2)
            {
                gc.BeginFigure(new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy), isFilled: false);
                for (int i = step; i < vm.Points.Count; i += step)
                    gc.LineTo(new Point(vm.Points[i].X - ox, vm.Points[i].Y - oy));
                // always include last point
                if (vm.Points.Count > 1)
                    gc.LineTo(new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy));
                gc.EndFigure(isClosed: false);
            }
            else
            {
                var p0 = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
                gc.BeginFigure(p0, isFilled: false);

                if (vm.Points.Count == 2)
                {
                    var p1 = new Point(vm.Points[1].X - ox, vm.Points[1].Y - oy);
                    gc.LineTo(p1);
                }
                else
                {
                    for (int i = 1; i < vm.Points.Count - 1; i++)
                    {
                        var curr = new Point(vm.Points[i].X - ox, vm.Points[i].Y - oy);
                        var next = new Point(vm.Points[i + 1].X - ox, vm.Points[i + 1].Y - oy);
                        var mid = new Point((curr.X + next.X) / 2, (curr.Y + next.Y) / 2);

                        gc.QuadraticBezierTo(curr, mid);
                    }

                    var last = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
                    gc.LineTo(last);
                }
                gc.EndFigure(isClosed: false);
            }
        }
        return geometry;
    }

    private static void RenderRectangle(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var shadowRect = rect.Translate(new Vector(Constants.AnnotationShadowOffsetX, Constants.AnnotationShadowOffsetY));
            ctx.DrawRectangle(null, MakeShadowPen(vm.Thickness), shadowRect);
        }
        else if (effect == AnnotationEffect.Outline)
        {
            ctx.DrawRectangle(null, MakeOutlinePen(vm.Thickness), rect);
        }

        // Transparent fill makes the entire interior hit-testable (not just the stroke).
        ctx.DrawRectangle(Brushes.Transparent, MakeStandardHitPen(vm.Thickness), rect);
        if (selectPen != null)
            ctx.DrawRectangle(null, selectPen, rect);
        ctx.DrawRectangle(null, pen, rect);
    }

    private static void RenderEllipse(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        var center = rect.Center;

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var sc = new Point(center.X + Constants.AnnotationShadowOffsetX, center.Y + Constants.AnnotationShadowOffsetY);
            ctx.DrawEllipse(null, MakeShadowPen(vm.Thickness), sc, rect.Width / 2, rect.Height / 2);
        }
        else if (effect == AnnotationEffect.Outline)
        {
            ctx.DrawEllipse(null, MakeOutlinePen(vm.Thickness), center, rect.Width / 2, rect.Height / 2);
        }

        // Transparent fill makes the entire interior hit-testable (not just the stroke).
        ctx.DrawEllipse(Brushes.Transparent, MakeStandardHitPen(vm.Thickness), center, rect.Width / 2, rect.Height / 2);
        if (selectPen != null)
            ctx.DrawEllipse(null, selectPen, center, rect.Width / 2, rect.Height / 2);
        ctx.DrawEllipse(null, pen, center, rect.Width / 2, rect.Height / 2);
    }

    private static void RenderArrow(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);

        // Arrowhead points
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double headLen = vm.Thickness * 3 + 5;
        var h1 = new Point(end.X - headLen * Math.Cos(angle - Math.PI / 6), end.Y - headLen * Math.Sin(angle - Math.PI / 6));
        var h2 = new Point(end.X - headLen * Math.Cos(angle + Math.PI / 6), end.Y - headLen * Math.Sin(angle + Math.PI / 6));

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var sp = MakeShadowPen(vm.Thickness);
            using (ctx.PushTransform(Matrix.CreateTranslation(Constants.AnnotationShadowOffsetX, Constants.AnnotationShadowOffsetY)))
            {
                ctx.DrawLine(sp, start, end);
                ctx.DrawLine(sp, end, h1);
                ctx.DrawLine(sp, end, h2);
            }
        }
        else if (effect == AnnotationEffect.Outline)
        {
            var op = MakeOutlinePen(vm.Thickness);
            ctx.DrawLine(op, start, end);
            ctx.DrawLine(op, end, h1);
            ctx.DrawLine(op, end, h2);
        }

        // Scale-aware transparent hit lines: keeps ~15 screen-pixel grab radius at any zoom.
        var hitPen = MakeScaledHitPen(vm.Thickness);
        ctx.DrawLine(hitPen, start, end);
        ctx.DrawLine(hitPen, end, h1);
        ctx.DrawLine(hitPen, end, h2);

        if (selectPen != null)
        {
            ctx.DrawLine(selectPen, start, end);
            ctx.DrawLine(selectPen, end, h1);
            ctx.DrawLine(selectPen, end, h2);
        }
        ctx.DrawLine(pen, start, end);
        ctx.DrawLine(pen, end, h1);
        ctx.DrawLine(pen, end, h2);
    }

    private static void RenderText(DrawingContext ctx, AnnotationViewModel vm, IBrush brush, Pen? selectPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count == 0)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var typeface = new Typeface("Inter, Arial");
        double fontSize = Math.Max(12, vm.Thickness * 4 + 10);
        var ft = new FormattedText(
            vm.Text ?? "",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush);

        var textRect = new Rect(start, new Size(ft.Width, ft.Height));

        // Transparent filled rect makes the entire text bounding box hit-testable.
        ctx.DrawRectangle(Brushes.Transparent, null, textRect);

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var shadowFt = new FormattedText(
                vm.Text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                ShadowBrush);
            ctx.DrawText(shadowFt, new Point(start.X + Constants.AnnotationShadowOffsetX, start.Y + Constants.AnnotationShadowOffsetY));
        }
        else if (effect == AnnotationEffect.Outline)
        {
            var outlineFt = new FormattedText(
                vm.Text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                OutlineBrush);
            // Draw at 4 cardinal + 4 diagonal offsets for a smooth outline
            double d = Constants.AnnotationTextOutlineOffset;
            ctx.DrawText(outlineFt, new Point(start.X - d, start.Y));
            ctx.DrawText(outlineFt, new Point(start.X + d, start.Y));
            ctx.DrawText(outlineFt, new Point(start.X, start.Y - d));
            ctx.DrawText(outlineFt, new Point(start.X, start.Y + d));
            ctx.DrawText(outlineFt, new Point(start.X - d, start.Y - d));
            ctx.DrawText(outlineFt, new Point(start.X + d, start.Y - d));
            ctx.DrawText(outlineFt, new Point(start.X - d, start.Y + d));
            ctx.DrawText(outlineFt, new Point(start.X + d, start.Y + d));
        }

        if (selectPen != null)
        {
            ctx.DrawRectangle(null, new Pen(SolidColorBrush.Parse(Constants.AccentColor), 2), textRect);
        }
        ctx.DrawText(ft, start);
    }
}
